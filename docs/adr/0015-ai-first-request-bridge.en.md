[🇧🇷 Português](0015-ai-first-request-bridge.md)

# ADR-0015 — Secure AI-First dispatch via GitHub issues

## Status
Proposed governance change. Human review, manual merge, and an actual end-to-end smoke test are required.

## Context
The `AI Evolution Harness` on `main` accepts `workflow_dispatch` and `inputs.task`, but the GitHub connection in ChatGPT does not offer that action. It already supports `create_issue`. Do not paste GitHub/OpenAI tokens into chats.

## Decision
Use a bridge workflow triggered by `issues: opened`. GitHub loads issues-triggered workflow code from the **default `main` branch**, preventing a request branch from replacing the validator. Accept only issues whose titles begin with `[AI-FIRST] ` and whose authenticated author is repository owner `WillianZanutoOliveira`.

The issue body must start with `<!-- ai-first-request:v1 -->` and include a nonempty UTF-8 task of **10–4096 bytes**. `scripts/ai-first-request-dispatch.py` rechecks the author and rejects control characters; `tests/ai_harness/` covers the contract. Untrusted issue text is never interpolated into shell source.

The bridge's job-scoped token has only `contents:read` and `actions:write`, and posts to `/actions/workflows/ai-evolution.yml/dispatches` with JSON `ref: main` and `inputs.task`. The original `engineer → validate → publish` guardrails and no-auto-merge behavior stay unchanged. The request issue provides an audit trail.

## Consequences and risks
- Only the repository owner can request an execution. If the connector attributes created issues to a bot account, dispatch is skipped until proven safe.
- Issue task text is public; never put secrets, PII, or sensitive data there. The bridge never receives `OPENAI_API_KEY`.
- Apply Codex/CI spending limits. Edits or comments do not retrigger the issue's `opened` event.
- GitHub permits `GITHUB_TOKEN` to trigger `workflow_dispatch`; repo policy may block `actions:write` and should not be bypassed.
- Protected workflow/governance changes require explicit human review and approval before merging.

## Acceptance
Green CI/Security, human review, manual merge, a harmless owner-authored issue with the marker, demonstrated bridge and harness execution on `main`, green `engineer`/`validate`/`publish` and independent checks, and a review-only PR with no automatic merge.
