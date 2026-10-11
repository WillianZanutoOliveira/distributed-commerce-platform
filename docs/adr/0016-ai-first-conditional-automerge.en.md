[🇧🇷 Português](0016-ai-first-conditional-automerge.md)

# ADR-0016 — Conditional automated AI-First review and merge

## Status
Governance proposal. CI, Security, permission review and an end-to-end smoke run are required before hands-free merges are considered verified.

## Context
AI Evolution Harness already demonstrated owner issue → sandboxed Codex → immutable SHA-256 patch → independent validator → Actions-created PR → CI/Security/DAST. PR #38 passed these checks and was merged on October 11, 2026. The owner requested automated review and integration without human PR approval.

## Decision
Introduce a **separate, trusted, deterministic policy-review job** after publish. Its code comes from the original immutable main revision, not the proposed PR. Verify GitHub Actions bot authorship, exact ai/evolution-<run>-<attempt> branch, matching immutable base and head SHA, single commit, no non-owner/team review requests or blocking reviews (the default owner CODEOWNERS request is informational only for eligible docs), changed path and normal file mode. Fail closed if main advances, the PR changes or any identity cannot be verified.

**First narrowly scoped authorization:** 1–4 **new Markdown files** under low-risk docs/ paths, no more than 250 added lines, no deletions, executable files, symlinks, code, workflows, ADRs, governance, security or production configuration. All other PRs remain open for separate approval. Production, GitOps and migration changes never qualify under this policy.

Before merging, recheck immutable PR identity and require three independently dispatched ci.yml, security.yml and dast.yml workflow runs to finish **successfully on exactly the same branch and commit**, each originating after the harness started. The policy-review token is limited to contents:write, pull-requests:write and actions:read. The reviewer records the evidence and submits a SHA-pinned merge through GitHub APIs, without bypassing rulesets.

This provides deterministic automated review and security gates, **not** proof that an LLM can guarantee software correctness or security.

## Risks and controls
- GitHub branch rules requiring human approval still apply: fail closed, never bypass.
- Failed, stale or timed-out workflows prevent merge.
- The reviewer receives no OPENAI_API_KEY and never runs untrusted PR code or policies.
- Code and security files still require human approval.
- This governance PR needs separate verification before activation.

## Acceptance
Green CI/Security for the governance PR, a small-docs issue smoke that automatically creates and merges its reviewed PR only after all three independent checks pass, and a rejection test for forbidden paths.
