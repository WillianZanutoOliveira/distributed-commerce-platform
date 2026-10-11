#!/usr/bin/env python3
"""Fail-closed automated review and merge for low-risk AI documentation changes.

The code is checked out from the trusted main commit, never the proposed PR.
This cannot grant approval or bypass GitHub branch/ruleset requirements.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import PurePosixPath
import subprocess
import sys
import time
from urllib.parse import urlencode

ALLOWED_WORKFLOWS = ("ci.yml", "security.yml", "dast.yml")
REJECTED_DOC_DIRECTORIES = frozenset(
    {"adr", "governance", "security", "deploy", "internal", "architecture"}
)
BLOCKING_LABELS = frozenset(
    {"do-not-merge", "manual-review", "security-review", "risk-high", "blocked"}
)
MAX_FILES = 4
MAX_ADDED_LINES = 250
MAX_WAIT_SECONDS = 3000


class PolicyFailure(RuntimeError):
    """A violation prevents any merge, without an unsafe fallback."""


def check_document_path(path: str) -> bool:
    if not isinstance(path, str) or not path.isascii() or len(path) > 160:
        return False
    parts = path.split("/")
    if len(parts) < 2 or len(parts) > 3 or parts[0] != "docs":
        return False
    if any(not p or p in {".", ".."} or p.startswith(".") for p in parts):
        return False
    if len(parts) == 3 and parts[1] in REJECTED_DOC_DIRECTORIES:
        return False
    if len(parts) == 2 and parts[1].startswith(("governance-", "security-", "architecture-")):
        return False
    if not path.endswith(".md") or PurePosixPath(path).as_posix() != path:
        return False
    return all(all(c.isalnum() or c in "-_." for c in part) for part in parts[1:])


def evaluate_files(files: list[dict], tree: dict) -> None:
    if not 1 <= len(files) <= MAX_FILES:
        raise PolicyFailure("Only 1-4 newly added Markdown documents qualify for auto-merge.")
    if tree.get("truncated"):
        raise PolicyFailure("Git tree response truncated; cannot prove file types.")
    modes = {entry["path"]: entry.get("mode") for entry in tree.get("tree", [])
             if entry.get("type") == "blob"}
    total_lines = 0
    seen: set[str] = set()
    for item in files:
        path = item.get("filename", "")
        if path in seen or not check_document_path(path):
            raise PolicyFailure("An unsafe or duplicate path was found.")
        seen.add(path)
        if item.get("status") != "added" or item.get("deletions") != 0:
            raise PolicyFailure("Only new documentation files are eligible.")
        added = item.get("additions")
        if not isinstance(added, int) or added <= 0:
            raise PolicyFailure("Documentation must have nonempty textual additions.")
        total_lines += added
        if modes.get(path) != "100644":
            raise PolicyFailure("Symlinks and executable files must never auto-merge.")
    if total_lines > MAX_ADDED_LINES:
        raise PolicyFailure("Documentation change exceeds the auto-merge size budget.")


def choose_run(runs: list[dict], *, branch: str, head_sha: str,
               minimum_created_at: str) -> dict | None:
    matched = [
        run for run in runs
        if run.get("event") == "workflow_dispatch"
        and run.get("head_sha") == head_sha
        and run.get("head_branch") == branch
        and run.get("created_at", "") >= minimum_created_at
    ]
    return max(matched, key=lambda run: (run.get("created_at", ""), run.get("id", 0)),
               default=None)


def github(method: str, path: str, fields: dict[str, str] | None = None) -> dict:
    command = ["gh", "api", "--method", method, path, "-H",
               "Accept: application/vnd.github+json"]
    for key, value in (fields or {}).items():
        command.extend(["--raw-field", f"{key}={value}"])
    output = subprocess.run(command, text=True, capture_output=True, check=False)
    if output.returncode:
        # Do not print full external text from failed API requests.
        raise PolicyFailure(f"GitHub API {method} request failed; exit={output.returncode}.")
    return json.loads(output.stdout)


def api(repo: str, path: str) -> dict:
    return github("GET", f"repos/{repo}/{path}")


def policy_check(repo: str, number: int, head_sha: str, branch: str,
                 trusted_base: str, harness_run_id: str) -> tuple[dict, str]:
    pr = api(repo, f"pulls/{number}")
    if pr.get("state") != "open" or pr.get("draft") or pr.get("merged_at"):
        raise PolicyFailure("PR must be open, not draft or merged.")
    if pr.get("user", {}).get("login") != "github-actions[bot]":
        raise PolicyFailure("PR not created by the trusted GitHub Actions bot.")
    if pr.get("base", {}).get("ref") != "main" or pr.get("base", {}).get("sha") != trusted_base:
        raise PolicyFailure("PR base does not match original trusted main revision.")
    head = pr.get("head", {})
    if head.get("ref") != branch or head.get("sha") != head_sha:
        raise PolicyFailure("Proposed branch or commit changed since validation.")
    if head.get("repo", {}).get("full_name", "").lower() != repo.lower():
        raise PolicyFailure("Forked PR is never auto-merged.")
    if pr.get("commits") != 1 or not branch.startswith(f"ai/evolution-{harness_run_id}-"):
        raise PolicyFailure("PR must contain exactly one commit from this harness run.")
    if pr.get("requested_reviewers") or pr.get("requested_teams"):
        raise PolicyFailure("A human review has been explicitly requested.")
    if {x.get("name", "").lower() for x in pr.get("labels", [])} & BLOCKING_LABELS:
        raise PolicyFailure("A safety or manual-review hold is present.")

    changed = pr.get("changed_files")
    if not isinstance(changed, int) or changed > MAX_FILES or changed <= 0:
        raise PolicyFailure("Changed-file count is outside the approved policy.")
    files = api(repo, f"pulls/{number}/files?per_page=100")
    if not isinstance(files, list) or len(files) != changed:
        raise PolicyFailure("Could not inspect all changed PR files.")
    commit_tree = api(repo, f"git/commits/{head_sha}")
    tree_sha = commit_tree.get("tree", {}).get("sha")
    if not tree_sha:
        raise PolicyFailure("Cannot verify the immutable Git tree.")
    tree = api(repo, f"git/trees/{tree_sha}?recursive=1")
    evaluate_files(files, tree)
    reviews = api(repo, f"pulls/{number}/reviews?per_page=100")
    if not isinstance(reviews, list) or len(reviews) >= 100 or any(
        x.get("state") in {"CHANGES_REQUESTED", "DISMISSED"} for x in reviews
    ):
        raise PolicyFailure("A blocking or dismissed human review exists.")

    main = api(repo, "branches/main")
    if main.get("commit", {}).get("sha") != trusted_base:
        raise PolicyFailure("Main moved since independent validation; rebase/retest required.")
    origin = api(repo, f"actions/runs/{harness_run_id}")
    if origin.get("event") != "workflow_dispatch" or origin.get("head_sha") != trusted_base:
        raise PolicyFailure("Could not attest the originating trusted harness run.")
    return pr, origin["created_at"]


def gate_status(repo: str, branch: str, head_sha: str,
                since: str) -> tuple[dict[str, str], dict[str, int]]:
    conclusions: dict[str, str] = {}
    run_ids: dict[str, int] = {}
    for filename in ALLOWED_WORKFLOWS:
        params = urlencode({"branch": branch, "event": "workflow_dispatch",
                            "head_sha": head_sha, "per_page": 100})
        runs = api(repo, f"actions/workflows/{filename}/runs?{params}")
        run = choose_run(runs.get("workflow_runs", []),
                         branch=branch, head_sha=head_sha, minimum_created_at=since)
        if run is None:
            conclusions[filename] = "pending"
        elif run.get("status") != "completed":
            conclusions[filename] = "pending"
        else:
            conclusions[filename] = str(run.get("conclusion") or "unknown")
        if run is not None:
            run_ids[filename] = int(run["id"])
    return conclusions, run_ids


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pr-number", required=True, type=int)
    parser.add_argument("--head-sha", required=True)
    parser.add_argument("--branch", required=True)
    parser.add_argument("--trusted-base", required=True)
    parser.add_argument("--harness-run-id", required=True)
    args = parser.parse_args()
    repo = os.environ["GITHUB_REPOSITORY"]
    if not os.environ.get("GH_TOKEN"):
        raise PolicyFailure("No short-lived GitHub token available.")

    try:
        _, since = policy_check(repo, args.pr_number, args.head_sha, args.branch,
                                args.trusted_base, args.harness_run_id)
    except PolicyFailure as error:
        print(f"Auto-review declined: {error}", file=sys.stderr)
        return 0  # PR stays open for the appropriate separate review path.

    deadline = time.monotonic() + MAX_WAIT_SECONDS
    while time.monotonic() < deadline:
        results, run_ids = gate_status(repo, args.branch, args.head_sha, since)
        if any(value not in {"success", "pending"} for value in results.values()):
            raise PolicyFailure("An independent CI/Security/DAST gate failed: "
                                + json.dumps(results, sort_keys=True))
        if all(value == "success" for value in results.values()):
            # Re-check immutable references and scope immediately before merging.
            policy_check(repo, args.pr_number, args.head_sha, args.branch,
                         args.trusted_base, args.harness_run_id)
            evidence = "Automatic independent review passed: new Markdown docs only, " \
                "unchanged validated SHA, trusted governance, and green dispatch runs: " \
                + ", ".join(f"{key} #{run_ids[key]}" for key in ALLOWED_WORKFLOWS) \
                + ". No human review was required by this low-risk policy."
            github("POST", f"repos/{repo}/issues/{args.pr_number}/comments", {"body": evidence})
            result = github("PUT", f"repos/{repo}/pulls/{args.pr_number}/merge",
                            {"sha": args.head_sha, "merge_method": "merge",
                             "commit_title": "AI-First verified docs-only autonomous merge"})
            if result.get("merged") is not True:
                raise PolicyFailure("GitHub did not confirm the protected merge.")
            print(f"Automatic merge verified for PR #{args.pr_number}.")
            return 0
        time.sleep(20)
    raise PolicyFailure("Timeout waiting for all three independent checks; PR remains open.")


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (PolicyFailure, KeyError, ValueError, OSError) as error:
        print(f"Auto-review failed closed: {error}", file=sys.stderr)
        raise SystemExit(1)
