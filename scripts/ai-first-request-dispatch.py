#!/usr/bin/env python3
"""Validate a trusted-owner GitHub Issue before dispatching AI Evolution Harness.

GitHub Actions loads this file from the immutable default-branch commit for
the 'issues: opened' event. The untrusted Issue body is never evaluated as code.
"""

from __future__ import annotations

import json
import os
from pathlib import Path

TITLE_PREFIX = "[AI-FIRST] "
BODY_MARKER = "<!-- ai-first-request:v1 -->\n"
MAX_TASK_BYTES = 4096
MIN_TASK_BYTES = 10


def task_from_issue(title: str, body: str, author: str, owner: str) -> str:
    """Return validated task text or fail closed with a redacted error."""
    if not owner or not author or author.casefold() != owner.casefold():
        raise ValueError("Only repository-owner issues can request AI-First runs.")

    if not title.startswith(TITLE_PREFIX) or not title[len(TITLE_PREFIX):].strip():
        raise ValueError("Issue title must start with '[AI-FIRST] ' and describe the task.")

    normalized = body.replace("\r\n", "\n")
    if not normalized.startswith(BODY_MARKER):
        raise ValueError("Missing required AI-First request marker.")

    task = normalized[len(BODY_MARKER):].strip()
    encoded = task.encode("utf-8")
    if len(encoded) < MIN_TASK_BYTES or len(encoded) > MAX_TASK_BYTES:
        raise ValueError("AI-First task must be between 10 and 4096 UTF-8 bytes.")

    if any((ord(char) < 32 and char not in "\n\t") or ord(char) == 127 for char in task):
        raise ValueError("Control characters are not allowed in AI-First tasks.")

    return task


def main() -> None:
    task = task_from_issue(
        title=os.environ["AI_FIRST_REQUEST_TITLE"],
        body=os.environ["AI_FIRST_REQUEST_BODY"],
        author=os.environ["AI_FIRST_REQUEST_AUTHOR"],
        owner=os.environ["AI_FIRST_REPOSITORY_OWNER"],
    )
    temporary = Path(os.environ["RUNNER_TEMP"])
    destination = temporary / "ai-first-dispatch.json"
    payload = {"ref": "main", "inputs": {"task": task}}

    # Never print the instruction or credentials to build logs.
    destination.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
    destination.chmod(0o600)
    print("Owner request validated, payload prepared for main workflow_dispatch.")


if __name__ == "__main__":
    main()
