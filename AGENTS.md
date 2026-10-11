# AI Engineering Instructions

This repository is a production-minded distributed-systems portfolio project. AI-assisted changes must make engineering quality more visible, not merely add technology.

## Read first

Before changing code, read:
1. .ai/engineering-constitution.md
2. docs/architecture.md
3. the ADRs under docs/adr
4. the code in the bounded context you are changing

## Architecture invariants

- Preserve service ownership: no service may read another service's database.
- Preserve the Orders Clean Architecture dependency direction.
- Preserve transactional outbox/inbox semantics and idempotent consumers.
- Prefer asynchronous integration events across service boundaries.
- Do not introduce distributed transactions.
- Keep observability vendor-neutral through OpenTelemetry.
- Preserve the shared Service Defaults for health, service discovery and HTTP resilience; do not duplicate those defaults per service.
- Preserve Gateway rate limiting unless a human-approved ADR replaces the policy.
- Preserve local topology parity between the Aspire AppHost and secure Docker Compose when infrastructure or security boundaries change.
- New architecture decisions require an ADR.

## Security invariants

- Never commit credentials, tokens, private keys or real customer data.
- Never commit `.aspire` runtime state or generated Vault token files.
- Application credentials belong in Vault (or the target platform secret manager), not in appsettings or application-container environment variables.
- PostgreSQL workload credentials must remain dynamic through the Vault Database Secrets Engine; do not replace them with long-lived application passwords.
- Runtime PostgreSQL roles must remain DML-only; schema DDL belongs to the separate Vault-backed DatabaseMigrator identity.
- Business services must never reintroduce `EnsureCreatedAsync`, `MigrateAsync`, or schema-creation logic during normal application startup.
- Preserve lease renewal and fail-closed behavior for dynamic database identities.
- Never trust a customer/user identifier supplied by the request body when it can be derived from authenticated identity.
- Do not disable issuer, audience, lifetime or signature validation to make tests pass.
- Do not weaken authorization policies.
- Local demo credentials must be unmistakably non-production.

## Developer experience

- The Aspire AppHost is the preferred local developer entry point.
- Do not simplify Aspire by bypassing Keycloak, Vault or dynamic PostgreSQL credentials.
- Keep Docker Compose as the CI-tested parity path.
- The AppHost is part of `DistributedCommerce.slnx` and must remain buildable under the same quality gates.

## Delivery invariants

- GitOps promotion must remain reviewable: release/promotion automation may open PRs but must not auto-merge production image changes.
- Production deployment state belongs in Git and is reconciled by Argo CD; CI must not add imperative production `kubectl apply` deployment steps.
- Database migration must complete before workload rollout, and canary changes must preserve backward-compatible expand/contract migration discipline.
- Preserve Argo Rollouts analysis gates for progressive delivery.

## Supply-chain invariants

- GitHub Actions references must remain commit-pinned; do not replace immutable SHAs with mutable tags.
- Security, Scorecard and release provenance workflows are human-governed.
- Application containers must remain non-root.
- Kubernetes examples must keep `runAsNonRoot`, `RuntimeDefault` seccomp, no privilege escalation and dropped Linux capabilities.
- Preserve the scheduled performance baseline and its pass/fail thresholds unless a human-reviewed performance decision changes them.

## Change discipline

- Keep each task narrowly scoped.
- Add or update tests for behavior changes.
- Run restore, build and tests before considering work complete.
- Update Portuguese and English documentation together when public behavior or architecture changes.
- Do not perform unrelated refactors.
- The engineering agent must never merge or push directly to main. A separate trusted governance-owned reviewer may automatically merge only new, low-risk Markdown documentation after all independent CI/Security/DAST checks; code and production changes still require explicit human review.

## Protected governance files

AI automation must not modify these files unless a human explicitly performs a separate governance change:

- AGENTS.md
- .ai/engineering-constitution.md
- .ai/prompts/engineer.md
- .github/workflows/ai-evolution.yml
- scripts/ai-first-automerge.py
- tests/ai_harness/test_ai_automerge.py
- .github/workflows/security.yml
- .github/workflows/scorecard.yml
- .github/workflows/release.yml
- .github/workflows/performance.yml
- .github/workflows/gitops-promote.yml
- .github/CODEOWNERS
- SECURITY.md

## Validation commands

- dotnet restore DistributedCommerce.slnx
- dotnet build DistributedCommerce.slnx --configuration Release --no-restore
- dotnet format DistributedCommerce.slnx --verify-no-changes --no-restore --severity warn
- dotnet test DistributedCommerce.slnx --configuration Release --no-build
- docker compose -f docker-compose.yml -f docker-compose.vault.yml config
