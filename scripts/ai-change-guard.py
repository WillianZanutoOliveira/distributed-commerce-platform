#!/usr/bin/env python3
"""Fail-closed Git change guard for AI engineering tasks.

Run inside an agent worktree before staging, committing or pushing. The
governing GitHub Actions workflow must be updated separately by a human.
"""

from __future__ import annotations

import argparse
import os
import re
from pathlib import Path
import subprocess
import sys

PROTECTED_FILES = frozenset(
    {
        "AGENTS.md",
        "SECURITY.md",
        ".github/CODEOWNERS",
        ".ai/engineering-constitution.md",
        ".ai/prompts/engineer.md",
        ".github/workflows/ai-evolution.yml",
        ".github/workflows/security.yml",
        ".github/workflows/scorecard.yml",
        ".github/workflows/release.yml",
        ".github/workflows/performance.yml",
        ".github/workflows/gitops-promote.yml",
        # A future agent must not be able to rewrite its own guard or CI checks.
        ".github/workflows/ci.yml",
        "scripts/ai-change-guard.py",
        "scripts/ai-first-automerge.py",
        "tests/ai_harness/test_ai_automerge.py",
        "tests/ai_harness/test_ai_change_guard.py",
    }
)

CHANGE_COMMANDS = (
    ("diff", "--cached", "--name-only", "--no-renames", "-z", "--"),
    ("diff", "--name-only", "--no-renames", "-z", "--"),
    ("ls-files", "--others", "--exclude-standard", "-z", "--"),
)


def git(repo: Path, *arguments: str) -> bytes:
    result = subprocess.run(
        ("git", "-C", str(repo), *arguments),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode:
        message = result.stderr.decode("utf-8", errors="replace").strip()
        raise RuntimeError(message or f"git exited with {result.returncode}")
    return result.stdout


def inspect_changes(repo: Path, base_sha: str) -> set[str]:
    """Include committed history, staged, unstaged, untracked, renames/deletes."""
    root = Path(os.fsdecode(git(repo, "rev-parse", "--show-toplevel").strip()))
    if not re.fullmatch(r"[0-9a-fA-F]{40}|[0-9a-fA-F]{64}", base_sha):
        raise RuntimeError("base SHA must be an immutable full Git commit hash")

    # A trusted pre-agent base cannot be forged by editing mutable branch refs.
    git(root, "cat-file", "-e", f"{base_sha}^{{commit}}")
    git(root, "merge-base", "--is-ancestor", base_sha, "HEAD")

    paths: set[str] = set()
    commands = (
        ("diff", "--name-only", "--no-renames", "-z", base_sha, "HEAD", "--"),
        *CHANGE_COMMANDS,
    )
    for command in commands:
        output = git(root, *command)
        paths.update(os.fsdecode(path) for path in output.split(b"\0") if path)
    return paths


def is_protected_path(path: str) -> bool:
    """Protect agent policy, governance proposals, harness tests and workflows."""
    return path in PROTECTED_FILES or path.startswith(
        (
            ".github/workflows/",
            ".ai/",
            "docs/governance/",
            "tests/ai_harness/",
        )
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo",
        type=Path,
        default=Path.cwd(),
        help="Working tree to inspect (default: current directory)",
    )
    parser.add_argument(
        "--base-sha",
        required=True,
        help="Full, immutable Git commit SHA captured by the trusted runner before the agent runs",
    )
    args = parser.parse_args()

    try:
        paths = inspect_changes(args.repo, args.base_sha)
    except (OSError, RuntimeError) as error:
        print(f"AI change guard failed closed: {error}", file=sys.stderr)
        return 2

    blocked = sorted(path for path in paths if is_protected_path(path))
    if blocked:
        print("AI change guard: protected governance files were changed:", file=sys.stderr)
        for path in blocked:
            print(f"  {path!r}", file=sys.stderr)
        return 1

    if not paths:
        print("AI change guard: task produced no eligible Git changes since the trusted base.", file=sys.stderr)
        return 2

    print(f"AI change guard: {len(paths)} changed path(s), governance intact.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
