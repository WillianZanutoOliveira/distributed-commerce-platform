# Engineering Constitution for AI-Assisted Evolution

## Purpose

The AI harness exists to accelerate small, reviewable improvements while keeping human control over architecture, security and merge decisions.

## Non-negotiable rules

1. Governed delivery — AI engineers may only propose a branch and PR. A separate immutable, trusted policy reviewer may automatically merge strictly eligible new Markdown documentation after independent CI, Security and DAST success; all code, migrations, deploy, security, architecture and governance changes retain human merge approval.
2. Green quality gates — build and automated tests must pass before a pull request is created.
3. Least privilege — automation receives only the repository permissions required to create its branch and PR.
4. Architecture preservation — service ownership, Clean Architecture boundaries, outbox/inbox and idempotency must not be weakened.
5. Security preservation — authentication and authorization validation cannot be disabled for convenience.
6. No secrets in source — credentials belong in secret stores or explicitly local demo fixtures.
7. Secret-manager preservation — application credentials must remain behind the Vault/platform-secret boundary; agents may not reintroduce effective service passwords through appsettings or application-container environment values.
8. Dynamic database identity preservation — PostgreSQL workload credentials remain Vault-issued, leased and renewable; agents may not replace them with long-lived passwords or remove fail-closed lease handling.
9. Local developer experience preservation — Aspire AppHost and secure Docker Compose must preserve the same Keycloak/Vault/dynamic-database security boundaries; convenience must not bypass architecture.
10. Small diffs — one task should result in one coherent change, not broad opportunistic refactoring.
11. Explain important decisions — material architectural choices require an ADR.
12. Bilingual public docs — public Portuguese and English documentation evolve together.
13. Supply-chain preservation — GitHub Actions remain commit-pinned, application containers remain non-root, and AI may not weaken CodeQL/Trivy/Scorecard/provenance gates for convenience.
14. Service-default preservation — readiness/liveness, service discovery, HTTP resilience and edge rate limiting remain centralized unless a human-reviewed ADR explicitly replaces them.
15. Migration privilege separation — runtime database identities remain DML-only and schema changes are applied only through the separate Vault-backed migration identity.
16. Executable architecture — architecture/contract/chaos tests cannot be removed or weakened merely to make a change pass.
17. Reviewable GitOps delivery — production promotion remains pull-request driven; AI cannot introduce automatic merge for application/production changes or imperative production deployment. Low-risk new Markdown documentation may use only the separate verified auto-review policy.
18. Progressive delivery preservation — migration-before-rollout and canary analysis gates remain in place unless a human-reviewed ADR replaces them.
19. Governance cannot rewrite itself — the agent cannot edit the harness policy or workflow that governs it.

## Pull-request expectation

An AI-generated PR should explain:
- requested outcome;
- files changed;
- architecture/security impact;
- validation performed;
- remaining trade-offs or follow-up work.

Outside the narrow, separately protected docs-only automated policy, a human reviewer remains responsible for merge approval. AI engineers cannot modify the reviewer policy or approve their own production code.
