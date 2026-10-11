"""Regression cases for the low-risk, fail-closed AI automatic review policy."""
from __future__ import annotations

import importlib.util
from pathlib import Path
import unittest

FILE = Path(__file__).resolve().parents[2] / "scripts" / "ai-first-automerge.py"
SPEC = importlib.util.spec_from_file_location("ai_automerge", FILE)
assert SPEC is not None and SPEC.loader is not None
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)


def tree(*files: str) -> dict:
    return {"truncated": False,
            "tree": [{"path": path, "mode": "100644", "type": "blob"} for path in files]}


def added(path: str, lines: int = 12) -> dict:
    return {"filename": path, "status": "added", "additions": lines, "deletions": 0}


class AutomaticReviewPolicyTests(unittest.TestCase):
    def test_new_docs_in_two_languages_allowed(self):
        paths = ["docs/new-feature.md", "docs/new-feature.en.md"]
        module.evaluate_files([added(p) for p in paths], tree(*paths))

    def test_governance_and_security_docs_rejected(self):
        for name in ("docs/adr/0090.md", "docs/governance/override.md",
                     "docs/security/change.md", "docs/.github/workflow.md",
                     "AGENTS.md", ".github/workflows/test.yml", "src/service.cs",
                     "docs/architecture-design.md", "docs/../AGENTS.md"):
            with self.subTest(path=name), self.assertRaises(module.PolicyFailure):
                module.evaluate_files([added(name)], tree(name))

    def test_only_new_files_not_existing_docs(self):
        with self.assertRaises(module.PolicyFailure):
            module.evaluate_files([{"filename": "docs/old.md", "status": "modified",
                                    "additions": 1, "deletions": 0}], tree("docs/old.md"))

    def test_symlink_and_executable_rejected(self):
        for mode in ("120000", "100755"):
            with self.subTest(mode=mode), self.assertRaises(module.PolicyFailure):
                module.evaluate_files([added("docs/link.md")],
                                      {"tree": [{"path": "docs/link.md",
                                                 "mode": mode, "type": "blob"}]})

    def test_size_and_count_bounded(self):
        with self.assertRaises(module.PolicyFailure):
            module.evaluate_files([added("docs/huge.md", 251)], tree("docs/huge.md"))
        paths = [f"docs/doc{i}.md" for i in range(5)]
        with self.assertRaises(module.PolicyFailure):
            module.evaluate_files([added(p) for p in paths], tree(*paths))
        with self.assertRaises(module.PolicyFailure):
            module.evaluate_files([added("docs/a.md")]*2, tree("docs/a.md"))

    def test_truncated_tree_and_missing_mode_rejected(self):
        with self.assertRaises(module.PolicyFailure):
            module.evaluate_files([added("docs/a.md")], {"truncated": True, "tree": []})
        with self.assertRaises(module.PolicyFailure):
            module.evaluate_files([added("docs/a.md")], tree())

    def test_git_run_requires_exact_commit_branch_event_and_time(self):
        run = {"event": "workflow_dispatch", "head_sha": "abc",
               "head_branch": "ai/evolution-44-1",
               "created_at": "2026-10-11T17:00:00Z",
               "status": "completed", "conclusion": "success", "id": 12}
        self.assertEqual(module.choose_run([run], branch="ai/evolution-44-1",
                                           head_sha="abc",
                                           minimum_created_at="2026-10-11T16:00:00Z"), run)
        self.assertIsNone(module.choose_run([run], branch="another", head_sha="abc",
                                            minimum_created_at="2026-10-11T16:00:00Z"))
        self.assertIsNone(module.choose_run([run], branch="ai/evolution-44-1",
                                            head_sha="abc",
                                            minimum_created_at="2026-10-12T00:00:00Z"))

    def test_only_latest_matching_dispatch_counts(self):
        run = {"event": "workflow_dispatch", "head_sha": "abc", "head_branch": "branch",
               "created_at": "2026-10-11T17:00:00Z", "id": 2}
        old = {**run, "id": 1}
        result = module.choose_run([old, run], branch="branch",
                                   head_sha="abc", minimum_created_at="2026-10-11T16:00:00Z")
        self.assertEqual(result["id"], 2)


if __name__ == "__main__":
    unittest.main()
