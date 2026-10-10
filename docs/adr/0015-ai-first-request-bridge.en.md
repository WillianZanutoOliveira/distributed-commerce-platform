[🇧🇷 Português](0015-ai-first-request-bridge.md)

# ADR-0015 — GitHub request bridge for AI-First

## Status
Governance proposal — requires human review and merge, passing CI/Security and a successful real end-to-end smoke test.

## Problem
The existing `ai-evolution.yml` supports `workflow_dispatch`, but the GitHub connector available to ChatGPT exposes branch/file/PR operations rather than the native dispatch action. Changing GitHub App permissions does not create missing connector operations. GitHub credentials and `OPENAI_API_KEY` must never be pasted into the chat.

## Decision
Create **one independent workflow** (`.github/workflows/ai-evolution-request.yml`) as a least-privilege governance bridge:
1. Trigger on `push` to `ai-requests/**` only when the push touches `.ai-requests/task.md`.
2. Require a **single direct-child commit** from a `main` ancestor which only adds `.ai-requests/task.md`; reject other changes, existing-file updates, symlinks, invalid encoding, and inputs over 4 KiB.
3. Forward only the task as JSON to GitHub's `workflow_dispatch` API with `ref=main`, using job-scoped `contents:read` and `actions:write`. Never execute the task as shell or print its contents in logs.
4. Preserve the original harness security boundaries: no persisted Git credentials for agent, isolated validation, trusted guard, patch identity checks, human-reviewed PRs, and no auto-merge.

## Consequences and risks
- Collaborators with write permission to request branches may launch billable Codex/CI runs. Manage repository access and budgets; audit GitHub Actions.
- External GitHub App `push` triggers must be confirmed with a real run. Pushes performed using another workflow's `GITHUB_TOKEN` normally do not start subsequent Actions workflows.
- The bridge still depends on the harness's `OPENAI_API_KEY` secret and `publish` permissions. Diagnose failures rather than bypassing governance.
- If a native dispatch capability becomes available in the connector, this bridge can be retired after a reviewed migration.

## Acceptance
Human governance approval and green checks; create an `ai-requests/<id>` branch from ChatGPT; verify the bridge starts a `main` AI Evolution Harness run with the same task; ensure `engineer`, `validate`, `publish` and independent CI/Security/DAST checks pass, with no automatic merge.
