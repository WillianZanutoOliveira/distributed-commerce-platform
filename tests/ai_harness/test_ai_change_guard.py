"""Isolated Git fixtures for the AI change guard (no third-party dependencies)."""

from __future__ import annotations

from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

GUARD = Path(__file__).resolve().parents[2] / "scripts" / "ai-change-guard.py"


class AiChangeGuardTests(unittest.TestCase):
    def setUp(self) -> None:
        self.sandbox = tempfile.TemporaryDirectory(prefix="ai-change-guard-")
        self.addCleanup(self.sandbox.cleanup)
        self.repo = Path(self.sandbox.name)
        self.git("init", "-q", "-b", "main")
        self.git("config", "user.name", "CI Fixture")
        self.git("config", "user.email", "ci@example.invalid")
        self.write("README.md", "Initial repository state.\n")
        self.git("add", "README.md")
        self.git("commit", "-qm", "initial")
        self.base_sha = subprocess.check_output(
            ("git", "-C", str(self.repo), "rev-parse", "HEAD"),
            text=True,
        ).strip()

    def git(self, *arguments: str) -> None:
        subprocess.run(
            ("git", "-C", str(self.repo), *arguments),
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.PIPE,
        )

    def write(self, name: str, content: str) -> None:
        path = self.repo / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    def guard(self) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            (sys.executable, str(GUARD), "--repo", str(self.repo), "--base-sha", self.base_sha),
            capture_output=True,
            check=False,
            text=True,
        )

    def test_rejects_empty_worktree(self) -> None:
        result = self.guard()
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertIn("no eligible Git changes", result.stderr)

    def test_allows_unstaged_non_governance_change(self) -> None:
        self.write("README.md", "Safe documentation change.\n")
        result = self.guard()
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_allows_staged_non_governance_change(self) -> None:
        self.write("README.md", "Safe staged edit.\n")
        self.git("add", "README.md")
        self.assertEqual(self.guard().returncode, 0)

    def test_allows_untracked_source_file(self) -> None:
        self.write("src/example.cs", "class Sample {}\n")
        self.assertEqual(self.guard().returncode, 0)

    def test_rejects_unstaged_protected_edit(self) -> None:
        self.write("AGENTS.md", "Initial rules.\n")
        self.git("add", "AGENTS.md")
        self.git("commit", "-qm", "add governance")
        self.write("AGENTS.md", "Tampered rules.\n")
        result = self.guard()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("AGENTS.md", result.stderr)

    def test_rejects_staged_protected_edit(self) -> None:
        self.write("SECURITY.md", "Initial policy.\n")
        self.git("add", "SECURITY.md")
        self.git("commit", "-qm", "add governance")
        self.write("SECURITY.md", "Weakened policy.\n")
        self.git("add", "SECURITY.md")
        result = self.guard()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("SECURITY.md", result.stderr)

    def test_rejects_untracked_protected_file(self) -> None:
        self.write(".github/workflows/ai-evolution.yml", "name: replaced\n")
        result = self.guard()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("ai-evolution.yml", result.stderr)

    def test_rejects_protected_rename_even_when_destination_is_safe(self) -> None:
        self.write("AGENTS.md", "Policies.\n")
        self.git("add", "AGENTS.md")
        self.git("commit", "-qm", "add governance")
        self.git("mv", "AGENTS.md", "old-rules.txt")
        result = self.guard()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("AGENTS.md", result.stderr)

    def test_rejects_protected_deletion(self) -> None:
        self.write(".ai/engineering-constitution.md", "Constitution.\n")
        self.git("add", ".ai/engineering-constitution.md")
        self.git("commit", "-qm", "add governance")
        self.git("rm", ".ai/engineering-constitution.md")
        self.assertEqual(self.guard().returncode, 1)

    def test_rejects_workflow_and_guard_self_modification(self) -> None:
        for protected_path in (
            ".github/CODEOWNERS",
            ".github/workflows/security.yml",
            ".github/workflows/ci.yml",
            "scripts/ai-change-guard.py",
            "scripts/ai-first-automerge.py",
            "tests/ai_harness/test_ai_automerge.py",
            "tests/ai_harness/test_ai_change_guard.py",
        ):
            with self.subTest(path=protected_path):
                self.write(protected_path, "Changed.\n")
                result = self.guard()
                self.assertEqual(result.returncode, 1, result.stderr)
                self.assertIn(protected_path, result.stderr)
                (self.repo / protected_path).unlink()

    def test_rejects_new_unapproved_workflow_yaml(self) -> None:
        for extension in (".yml", ".yaml"):
            with self.subTest(extension=extension):
                name = f".github/workflows/unauthorized{extension}"
                self.write(name, "name: Unapproved\\n")
                result = self.guard()
                self.assertEqual(result.returncode, 1, result.stderr)
                self.assertIn(name, result.stderr)
                (self.repo / name).unlink()

    def test_rejects_new_policy_and_governance_test_files(self) -> None:
        for protected_path in (
            ".ai/prompts/another-agent.md",
            "docs/governance/unapproved-policy.md",
            "tests/ai_harness/test_injected_policy.py",
            ".github/workflows/disabled-workflow.txt",
        ):
            with self.subTest(path=protected_path):
                self.write(protected_path, "Unreviewed policy.\\n")
                result = self.guard()
                self.assertEqual(result.returncode, 1, result.stderr)
                self.assertIn(protected_path, result.stderr)
                (self.repo / protected_path).unlink()

    def test_rejects_protected_changes_mixed_with_safe_ones(self) -> None:
        self.write("README.md", "Safe change.\n")
        self.write(".ai/prompts/engineer.md", "Malicious change.\n")
        result = self.guard()
        self.assertEqual(result.returncode, 1, result.stderr)

    def test_does_not_confuse_shell_metacharacters_with_commands(self) -> None:
        self.write("safe $(echo attack).txt", "Text.\n")
        self.assertEqual(self.guard().returncode, 0)

    def test_does_not_misread_paths_containing_newlines(self) -> None:
        self.write("AGENTS.md\nlooks-dangerous.txt", "Not the protected file.\n")
        self.assertEqual(self.guard().returncode, 0)

    def test_rejects_protected_change_already_committed_by_agent(self) -> None:
        self.write("AGENTS.md", "Policies modified after trusted base.\n")
        self.git("add", "AGENTS.md")
        self.git("commit", "-qm", "agent committed forbidden governance change")
        self.write("README.md", "A safe unstaged change cannot hide the commit.\n")
        result = self.guard()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("AGENTS.md", result.stderr)

    def test_allows_non_governance_change_already_committed(self) -> None:
        self.write("README.md", "Safe change already committed.\n")
        self.git("add", "README.md")
        self.git("commit", "-qm", "safe agent commit")
        self.assertEqual(self.guard().returncode, 0)

    def test_rejects_unknown_trusted_base(self) -> None:
        result = subprocess.run(
            (sys.executable, str(GUARD), "--repo", str(self.repo), "--base-sha", "0" * 40),
            capture_output=True,
            check=False,
            text=True,
        )
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertIn("failed closed", result.stderr)

    def test_git_failure_is_rejected(self) -> None:
        result = subprocess.run(
            (sys.executable, str(GUARD), "--repo", str(self.repo / "not-a-repo"), "--base-sha", self.base_sha),
            capture_output=True,
            check=False,
            text=True,
        )
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertIn("failed closed", result.stderr)


if __name__ == "__main__":
    unittest.main()
