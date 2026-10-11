"""Regression tests for the trusted GitHub Issues AI-First dispatch contract."""

from __future__ import annotations

import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


SCRIPT_PATH = Path(__file__).resolve().parents[2] / "scripts" / "ai-first-request-dispatch.py"
SPEC = importlib.util.spec_from_file_location("ai_request_bridge", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
bridge = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(bridge)

OWNER = "WillianZanutoOliveira"
TITLE = "[AI-FIRST] Document safe smoke test"
BODY = "<!-- ai-first-request:v1 -->\n\nCreate only a harmless bilingual smoke-test document."


class IssueRequestContractTests(unittest.TestCase):
    def test_owner_request_is_accepted(self):
        task = bridge.task_from_issue(TITLE, BODY, OWNER, OWNER)
        self.assertEqual(task, "Create only a harmless bilingual smoke-test document.")

    def test_owner_comparison_is_case_insensitive(self):
        self.assertIn("smoke-test", bridge.task_from_issue(TITLE, BODY, OWNER.lower(), OWNER))

    def test_other_users_are_rejected(self):
        with self.assertRaises(ValueError):
            bridge.task_from_issue(TITLE, BODY, "outside-user", OWNER)

    def test_wrong_title_does_not_dispatch(self):
        with self.assertRaises(ValueError):
            bridge.task_from_issue("AI task", BODY, OWNER, OWNER)

    def test_marker_is_required(self):
        with self.assertRaises(ValueError):
            bridge.task_from_issue(TITLE, "Run anything", OWNER, OWNER)

    def test_empty_task_is_rejected(self):
        with self.assertRaises(ValueError):
            bridge.task_from_issue(TITLE, "<!-- ai-first-request:v1 -->\n\n", OWNER, OWNER)

    def test_too_long_task_is_rejected(self):
        with self.assertRaises(ValueError):
            bridge.task_from_issue(TITLE, "<!-- ai-first-request:v1 -->\n" + "a" * 4097, OWNER, OWNER)

    def test_utf8_byte_length_is_checked(self):
        with self.assertRaises(ValueError):
            bridge.task_from_issue(TITLE, "<!-- ai-first-request:v1 -->\n" + "é" * 2049, OWNER, OWNER)

    def test_nul_and_control_characters_are_rejected(self):
        for text in ["hello\x00world", "hello\x1bworld"]:
            with self.subTest(text=repr(text)), self.assertRaises(ValueError):
                bridge.task_from_issue(TITLE, "<!-- ai-first-request:v1 -->\n" + text, OWNER, OWNER)

    def test_does_not_execute_input_and_preserves_lines(self):
        body = "<!-- ai-first-request:v1 -->\n\nWrite a note:\n$(echo do-not-run)"
        self.assertEqual(
            bridge.task_from_issue(TITLE, body, OWNER, OWNER),
            "Write a note:\n$(echo do-not-run)",
        )

    def test_payload_uses_main_without_logging_task(self):
        with tempfile.TemporaryDirectory() as directory:
            environ = {
                "AI_FIRST_REQUEST_TITLE": TITLE,
                "AI_FIRST_REQUEST_BODY": BODY,
                "AI_FIRST_REQUEST_AUTHOR": OWNER,
                "AI_FIRST_REPOSITORY_OWNER": OWNER,
                "RUNNER_TEMP": directory,
            }
            with patch.dict(os.environ, environ, clear=False):
                bridge.main()
            payload_path = Path(directory) / "ai-first-dispatch.json"
            payload = json.loads(payload_path.read_text(encoding="utf-8"))
            self.assertEqual(payload["ref"], "main")
            self.assertEqual(payload["inputs"]["task"],
                             "Create only a harmless bilingual smoke-test document.")
            self.assertEqual(payload_path.stat().st_mode & 0o777, 0o600)


if __name__ == "__main__":
    unittest.main()
