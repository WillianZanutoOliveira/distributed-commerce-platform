<div align="center">

[🇧🇷 Português](README.md)

# Distributed Commerce Platform

### .NET 10 · Aspire · Clean Architecture · YARP · RabbitMQ · Keycloak · Vault · OpenTelemetry · AI Engineering Harness

[![CI](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/workflows/ci.yml/badge.svg)](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/workflows/ci.yml)
![Services](https://img.shields.io/badge/Services-5-2563EB)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4%20Databases-4169E1?logo=postgresql&logoColor=white)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-MassTransit-FF6600?logo=rabbitmq&logoColor=white)
![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-OTLP--ready-7C3AED)
![Aspire](https://img.shields.io/badge/Local%20Dev-Aspire%2013.6-512BD4?logo=dotnet&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-7%20Images-2496ED?logo=docker&logoColor=white)
![Kubernetes](https://img.shields.io/badge/Kubernetes-Examples-326CE5?logo=kubernetes&logoColor=white)
![Keycloak](https://img.shields.io/badge/Identity-Keycloak-4D4D4D?logo=keycloak&logoColor=white)
![Vault](https://img.shields.io/badge/Secrets-Vault-FFEC6E?logo=vault&logoColor=black)
![Sonar](https://img.shields.io/badge/Clean%20Code-Sonar-126ED3?logo=sonarqubecloud&logoColor=white)
![AI Harness](https://img.shields.io/badge/AI%20Harness-Codex%20%2B%20GitHub%20Actions-111827)

**[Architecture](docs/architecture.en.md) · [Local development](docs/local-development.en.md) · [Platform Technical Walkthrough](docs/technical-walkthrough.en.md) · [ADRs](docs/adr) · [Kubernetes](deploy/k8s/README.en.md) · [CI](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/workflows/ci.yml)**

</div>

A production-minded distributed commerce reference platform designed to demonstrate the engineering concerns expected in **Senior .NET, Tech Lead and Software Architect** roles.

The system models a checkout flow split across independently deployable services:

1. **Orders API** receives an order and persists the aggregate.
2. The order is published through a **transactional bus outbox**.
3. **Inventory Service** consumes the order, makes an idempotent reservation decision and publishes the result.
4. **Payments Service** reacts to a successful reservation and authorizes or rejects the payment.
5. **Orders** reacts asynchronously to the final business outcome.
6. **Notifications Service** consumes payment outcomes independently.


Alongside checkout, the platform provides an **independent administrative individual/company registry (Customers)**, including CRUD, CPF/CNPJ validation, contacts, addresses, and postal-code lookup. It shares Keycloak and the Gateway but owns its own domain and database; registration does not automatically create Keycloak identities or join the Orders messaging flow.

The project intentionally focuses on the hard parts of distributed systems rather than on UI work.

---

## Why this project exists

The goal is to make advanced backend engineering visible in a public portfolio:

- Clean Architecture and dependency inversion;
- domain modeling and aggregate invariants;
- asynchronous messaging with RabbitMQ;
- event-driven service collaboration;
- transactional outbox/inbox patterns with MassTransit + EF Core;
- idempotent message processing;
- eventual consistency;
- retries and failure isolation;
- database-per-service;
- PostgreSQL;
- YARP API Gateway with edge authentication and identity-partitioned rate limiting;
- centralized secrets management with HashiCorp Vault and per-workload policies;
- dynamic PostgreSQL credentials through the Database Secrets Engine with TTL, lease renewal and revocation;
- OpenTelemetry traces/metrics with Tempo, Prometheus and Grafana;
- Docker and Docker Compose;
- local orchestration with .NET Aspire 13.6 and its integrated dashboard;
- shared Service Defaults for readiness/liveness, service discovery and HTTP resilience;
- Kubernetes-ready health endpoints;
- CI/CD quality gates;
- weekly k6 performance baseline with technical-SLO thresholds;
- automated tests, coverage and real PostgreSQL integration through Testcontainers;
- Clean Code enforcement with SonarAnalyzer, .editorconfig and dotnet format;
- DevSecOps gates with CodeQL, Gitleaks, Trivy, authenticated OWASP ZAP DAST, SBOM generation and OpenSSF Scorecard;
- commit-pinned GitHub Actions and GHCR OCI releases with OIDC/Sigstore provenance attestations;
- OpenID Connect authentication, JWT validation, RBAC and resource-level authorization with Keycloak;
- native ASP.NET Core OpenAPI validated by the Aspire topology test;
- AI-assisted engineering harness with build/test gates and pull-request-only delivery;
- architecture decision records.

---

## Architecture

```mermaid
flowchart TB
    Client["Client / Administrator"]
    subgraph Identity["Identity and access"]
        Keycloak["Keycloak<br/>OIDC / OAuth 2.0"]
    end
    subgraph Edge["HTTP edge"]
        Gateway["YARP API Gateway<br/>JWT + rate limiting"]
    end
    subgraph Services["Business services"]
        Orders["Orders API<br/>Clean Architecture"]
        Customers["Customers API<br/>Full PF / PJ CRUD<br/>Clean Architecture"]
        Inventory["Inventory Service"]
        Payments["Payments Service"]
        Notifications["Notifications Service"]
    end
    subgraph Messaging["Messaging"]
        Rabbit[(RabbitMQ<br/>MassTransit)]
    end
    subgraph Persistence["Database per service"]
        ODB[(Orders PostgreSQL)]
        CDB[(Customers PostgreSQL)]
        IDB[(Inventory PostgreSQL)]
        PDB[(Payments PostgreSQL)]
    end
    subgraph Secrets["Secrets and schema lifecycle"]
        Vault["HashiCorp Vault<br/>Database Secrets Engine"]
        Migrator["DatabaseMigrator<br/>EF Core Migrations"]
    end
    subgraph Observability["Observability"]
        OTel["OpenTelemetry Collector"]
        Tempo["Tempo"]
        Prometheus["Prometheus"]
        Grafana["Grafana"]
    end

    Client -->|"login"| Keycloak
    Keycloak -->|"access token JWT"| Client
    Client -->|"Bearer JWT"| Gateway
    Gateway -->|"validated JWT"| Orders
    Gateway -->|"admin JWT: PF/PJ CRUD and CEP"| Customers

    Orders --> ODB
    Customers -->|"CPF/CNPJ, contacts, addresses"| CDB
    Customers -->|"postal code only"| BrasilAPI["BrasilAPI CEP v2"]
    Orders -->|"OrderSubmitted"| Rabbit
    Rabbit --> Inventory
    Inventory --> IDB
    Inventory -->|"InventoryReserved / InventoryRejected"| Rabbit
    Rabbit --> Payments
    Payments --> PDB
    Payments -->|"PaymentAuthorized / PaymentFailed"| Rabbit
    Rabbit --> Orders
    Rabbit --> Notifications

    Vault -. "runtime credentials" .-> Orders
    Vault -. "runtime credentials" .-> Customers
    Vault -. "runtime credentials" .-> Inventory
    Vault -. "runtime credentials" .-> Payments
    Vault -. "RabbitMQ secret" .-> Notifications
    Vault -. "migration credential" .-> Migrator
    Migrator -. "EF Core migrations" .-> ODB
    Migrator -. "EF Core migrations" .-> CDB
    Migrator -. "EF Core migrations" .-> IDB
    Migrator -. "EF Core migrations" .-> PDB

    Gateway -. "OTLP" .-> OTel
    Orders -. "OTLP" .-> OTel
    Customers -. "OTLP" .-> OTel
    Inventory -. "OTLP" .-> OTel
    Payments -. "OTLP" .-> OTel
    Notifications -. "OTLP" .-> OTel
    OTel --> Tempo
    OTel --> Prometheus
    Tempo --> Grafana
    Prometheus --> Grafana
```
More detail: [Architecture documentation](docs/architecture.en.md) · [platform technical walkthrough](docs/technical-walkthrough.en.md)

---

## Service boundaries

| Component | Responsibility | Persistence | Integration / Security |
| --- | --- | --- | --- |
| Keycloak | identity, OIDC/OAuth 2.0 authentication and realm roles | IdP internal state | issues JWTs to clients |
| YARP API Gateway | HTTP ingress, JWT validation, rate limiting and reverse proxy | stateless | forwards authenticated requests |
| Customers API | administrative individual/company registry, contacts and addresses | PostgreSQL | JWT/RBAC + BrasilAPI CEP v2 |
| Orders API | order lifecycle, resource authorization and customer-facing API | PostgreSQL | publishes + consumes events |
| Inventory Service | idempotent stock reservation decision | PostgreSQL | consumes + publishes events |
| Payments Service | payment authorization decision | PostgreSQL | consumes + publishes events |
| Notifications Service | independent customer communication reaction | stateless demo | consumes events |
| RabbitMQ | asynchronous transport between bounded contexts | queues / exchanges | MassTransit, at-least-once |
| HashiCorp Vault | secrets, policies and dynamic PostgreSQL credentials | Vault storage | token/policy per workload |
| DatabaseMigrator | applies EF Core Migrations before workloads | target database | separate Vault migration identity |
| OpenTelemetry | vendor-neutral trace and metric collection | external backend | OTLP to Collector/Tempo/Prometheus |

Every stateful service owns its database. No service reads another service's tables. Keycloak and Vault are **platform components**, not business bounded contexts.


## Clean Architecture

The **Orders** bounded context is split into explicit layers:

```text
Orders.Domain
      ↑
Orders.Application
      ↑
Orders.Infrastructure
      ↑
Orders.Api
```

The domain knows nothing about EF Core, RabbitMQ or ASP.NET Core.

The application layer depends on ports such as:

- `IOrderRepository`
- `IUnitOfWork`
- `IIntegrationEventPublisher`

Infrastructure implements those ports with PostgreSQL, EF Core and MassTransit.

Smaller event-only services use a deliberately lighter structure. This is intentional: the project demonstrates that architecture should match service complexity rather than copy layers mechanically.

---

## Reliability patterns

### Transactional outbox

The Orders API uses the MassTransit EF Core **Bus Outbox**.

The order state and the outgoing `OrderSubmitted` message participate in the same persistence boundary. The HTTP request does not need a distributed transaction between PostgreSQL and RabbitMQ.

### Consumer inbox/outbox

Inventory, Payments and Orders consumers use the EF Core outbox integration to support duplicate protection and reliable outgoing messages.

### Idempotency

Inventory and Payments persist a unique decision per `OrderId` so repeated business events do not create duplicate reservations or payments.

### Eventual consistency

The API returns an order in `Pending` state first. The state becomes `Completed`, `InventoryRejected` or `PaymentFailed` asynchronously.

This is a deliberate distributed-system trade-off.

---

## End-to-end flow: identity + order + events

```text
Client
  |
  +--> Keycloak ------------------------------+
  |       |                                   |
  |       +--> JWT access token               |
  |                                           v
  +--------------------------------------> YARP Gateway
                                               |
                                               v
                                         POST /api/orders
                                               |
                                               v
                                           Orders
                                               |
                                               v
                                         OrderSubmitted
                                               |
                                               v
                                      Inventory Service
                                         /          \
                                        v            v
                                  Reserved        Rejected
                                     |               |
                                     v               +----> Orders -> InventoryRejected
                                  Payments
                                  /      \
                                 v        v
                              Paid      Failed
                               |           |
                               +-----------+---------> Orders
                               |
                               +---------------------> Notifications
```

Authentication is synchronous only at the HTTP edge. Collaboration between business bounded contexts remains asynchronous through RabbitMQ.

## Individual/company registry and addresses

The **Customers** microservice provides a **complete CRUD for individuals (PF) and companies (PJ)** as an administrative bounded context independent of Orders. It follows Clean Architecture (`Customers.Domain`, `Customers.Application`, `Customers.Infrastructure`, and `Customers.Api`), owns its PostgreSQL database, and exposes its API through the **YARP Gateway**. The architecture diagram above includes Customers, Keycloak, BrasilAPI, Vault, and its dedicated database.


### Individual/company registration journey

```mermaid
flowchart LR
    Admin["Administrator"] -->|"login"| KC["Keycloak OIDC"]
    KC -->|"access token with admin role"| Admin
    Admin -->|"Bearer JWT"| GW["YARP Gateway"]
    GW -->|"routes /api/customers"| API["Customers API<br/>JWT and admin RBAC"]
    API --> APP["Customers.Application<br/>PF / PJ CRUD"]
    APP --> MODEL["Customer aggregate<br/>CPF or CNPJ, contacts,<br/>1 to 10 addresses"]
    APP -->|"EF Core repository"| DB[("Customers PostgreSQL")]
    APP -->|"IPostalCodeLookup"| CEP["BrasilAPI v2 adapter"]
    CEP -->|"postal code only"| BRA["BrasilAPI CEP v2"]
    VAULT["Vault: dynamic credentials"] -.-> API
```

Registration is **synchronous and separate from the order journey**: the API validates CPF/CNPJ locally, applies aggregate invariants, and persists exclusively in Customers' database. Postal-code lookup only returns address suggestions; it does not automatically save them. Customers validates the `admin` role again. There is no automatic Keycloak-user provisioning or Orders association. See the [detailed architecture flow](docs/architecture.en.md#individual-and-company-registration-flow).

### Available operations

| Method | Public route (Gateway) | Result and purpose |
| --- | --- | --- |
| `POST` | `/api/customers` | `201 Created` — create an individual or company with addresses |
| `GET` | `/api/customers/{id}` | `200 OK` / `404 Not Found` — retrieve by GUID |
| `GET` | `/api/customers` | `200 OK` — paginated, filtered search |
| `PUT` | `/api/customers/{id}` | `200 OK` / `404 Not Found` — replace details and addresses; activate/deactivate |
| `DELETE` | `/api/customers/{id}` | `204 No Content` / `404 Not Found` — **physical deletion** of the aggregate |
| `GET` | `/api/customers/address/cep/{cep}` | `200 OK` / `404 Not Found` / `503 Service Unavailable` — BrasilAPI v2 postal-code lookup |

Search supports `search` (text), `document` (CPF/CNPJ), `personType` (`Individual` or `Company`), `page`, and `pageSize`. Defaults are page 1 and 20 results per page, capped at 100. The response contains `items`, `page`, `pageSize`, `totalCount`, and `totalPages`.

### Registry fields and validation rules

| Entity | Fields and constraints |
| --- | --- |
| **Individual — `Individual`** | `document` (valid CPF), `displayName` (name), `birthDate` (optional, before today), `email`, `phone`, and `addresses`. Company-only fields are rejected. |
| **Company — `Company`** | `document` (valid CNPJ), `displayName` (trade name), `legalName` (required legal name), `foundationDate` (optional, not in the future), `stateRegistration` and `municipalRegistration` (optional), `email`, `phone`, and `addresses`. |
| **Addresses (both types)** | **1–10** per registration, with **exactly one `isPrimary: true`**. Fields: `type` (`Primary`, `Billing`, `Shipping`, or `Other`), `postalCode`, `street`, `number`, `city`, and `state`; optional: `complement`, `neighborhood`, `ibgeCityCode`, `latitude`, and `longitude`. |

CPF/CNPJ values are normalized, check-digit validated, and protected by a unique database index (`409 Conflict` for duplicates). Postal codes are normalized to eight digits, state abbreviations to two letters, email is validated, and phone numbers are normalized. **Person type is immutable** after creation. `PUT` requires the complete registration, including the full address collection and `isActive`; `DELETE` physically removes the registration whereas `isActive: false` merely deactivates it.

### API creation examples

These are **fictional development fixtures**, not real registrations. Every request requires a **Keycloak JWT with the `admin` role**, validated again by Customers. A `customer` token cannot read or modify registrations. Examples assume the local Gateway at `http://localhost:8080` and a valid administrative JWT in `ADMIN_TOKEN`.

**Individual:**

```bash
curl -i -X POST http://localhost:8080/api/customers \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "personType": "Individual",
    "document": "111.444.777-35",
    "displayName": "Example Person",
    "birthDate": "1990-01-01",
    "email": "pf@example.invalid",
    "phone": "(44) 99999-0000",
    "addresses": [{
      "type": "Primary",
      "isPrimary": true,
      "postalCode": "87000-000",
      "street": "Example Street",
      "number": "100",
      "neighborhood": "Centro",
      "city": "Maringa",
      "state": "PR"
    }]
  }'
```

**Company:**

```bash
curl -i -X POST http://localhost:8080/api/customers \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "personType": "Company",
    "document": "11.222.333/0001-81",
    "displayName": "Example Company",
    "legalName": "Example Company LTDA",
    "foundationDate": "2020-01-01",
    "stateRegistration": "ISENTO",
    "email": "pj@example.invalid",
    "phone": "44999990000",
    "addresses": [{
      "type": "Primary",
      "isPrimary": true,
      "postalCode": "87000-000",
      "street": "Example Avenue",
      "number": "200",
      "city": "Maringa",
      "state": "PR"
    }]
  }'
```

**Search, filtering, and postal-code lookup:**

```bash
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  "http://localhost:8080/api/customers?personType=Individual&page=1&pageSize=10"

curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  "http://localhost:8080/api/customers/address/cep/01001000"
```

Postal-code `GET` uses **BrasilAPI CEP v2** through the `IPostalCodeLookup` application port and a resilient, time-limited HTTP client. Its response may contain street, neighborhood, city, state, IBGE city code, and **optional** coordinates. Lookup does not automatically persist an address. Only postal codes go to BrasilAPI — **never CPF/CNPJ**. Provider failures return `503`; unknown postal codes return `404`.

### Security, persistence, and tests

- **Keycloak + RBAC:** every Customers route, including postal-code lookup, requires the `admin` role; both Gateway and API validate JWTs. Missing authentication returns `401`, insufficient permissions `403`, and invalid data results in `400` validation responses.
- **Isolation and credentials:** a dedicated Customers PostgreSQL database; EF Core Migrations use a `customers_migrator` identity (DDL), separate from the `customers_runtime` identity (DML). Vault supplies dynamic credentials for the secure runtime.
- **Local execution:** Customers participates in .NET Aspire and Docker Compose. Follow the [local development guide](docs/local-development.en.md) to start Keycloak, Vault, Gateway, and dependencies.
- **Automated checks:** with local infrastructure running, execute `bash scripts/customers-smoke.sh` (requires `curl` and `jq`, uses the local fixture user `demo-admin`). This tests PF/PJ CRUD through the Gateway, authorization, address replacement, pagination, duplicate documents, and deletion. Domain, infrastructure, and persistence regression tests are also available.

**Current boundaries:** this is an **administrative API**, not a ready-made user-facing web UI; creating a customer does not automatically provision a Keycloak identity or attach the registration to an Orders order. See [ADR-0013](docs/adr/0013-customers-pf-pj-brasilapi-cep.en.md), [architecture](docs/architecture.en.md), and [security posture](docs/security-posture.en.md) for the design and security decisions.

---

## Running locally

### Recommended path: .NET Aspire

Requirements:

- .NET 10 SDK;
- Docker or an Aspire-compatible Podman installation.

Start the complete platform with one command:

```bash
dotnet run --project src/Platform/DistributedCommerce.AppHost
```

The AppHost starts PostgreSQL, RabbitMQ, Keycloak and Vault, bootstraps dynamic credentials, launches the Gateway + five services as local projects and opens the Aspire Dashboard for logs, traces, metrics, endpoints and resource state.

Local infrastructure passwords are generated through the Aspire secret store. Workload-scoped Vault tokens are written only under `.aspire/vault-tokens`, which is excluded from Git.

See the [local-development guide](docs/local-development.en.md) and [ADR-0008](docs/adr/0008-dotnet-aspire-local-orchestration.en.md).

### Parity / CI path: Docker Compose

Secure Compose remains supported and is still exercised by CI:

```bash
cp .env.example .env
docker compose -f docker-compose.yml -f docker-compose.vault.yml up --build
```

The simple `docker-compose.yml` remains useful for learning, while the Vault overlay preserves dynamic PostgreSQL credentials.

Endpoints:

| Component | URL |
| --- | --- |
| API Gateway | http://localhost:8080 |
| Orders API | http://localhost:8081 |
| Orders health | http://localhost:8081/health |
| Inventory health | http://localhost:8082/health |
| Payments health | http://localhost:8083/health |
| Notifications health | http://localhost:8084/health |
| RabbitMQ Management | http://localhost:15672 |
| Keycloak | http://localhost:8180 |
| Vault | http://localhost:8200 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| Tempo | http://localhost:3200 |

Get a token for the local demo user:

```bash
TOKEN=$(curl -s -X POST http://localhost:8180/realms/distributed-commerce/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=commerce-cli" \
  -d "username=demo-customer" \
  -d "password=local-demo-only" | jq -r .access_token)
```

Create an authenticated order. CustomerId is derived from the token sub claim and is not accepted from the payload:

```bash
curl -X POST http://localhost:8080/api/orders \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      { "sku": "NOTEBOOK-01", "quantity": 1, "unitPrice": 3499.90 }
    ]
  }'
```

Query its asynchronous status:

```bash
curl http://localhost:8080/api/orders/{order-id} \
  -H "Authorization: Bearer $TOKEN"
```

### Demo failure paths

The sample contains deterministic policies so the distributed flow can be tested without external providers:

- an item quantity above **10** produces `InventoryRejected`;
- an order total above **5,000** produces `PaymentFailed`.

These rules are intentionally simple; the architecture around them is the focus.

---

## Security and identity

The Orders API validates access tokens issued by Keycloak. Signature, issuer, audience and lifetime are validated, and realm roles feed authorization policies.

Order CustomerId is derived from the token sub claim instead of trusting the request payload. Users with the customer role can only read their own orders, while admin can read across customers.

The versioned local realm exists for demos and smoke tests. Direct password grant is only a local fixture; real interactive clients should use Authorization Code + PKCE.

See [ADR-0004](docs/adr/0004-identity-keycloak.en.md).

### Pentest readiness

Security is treated as a **verifiable property**, not a promise of “zero vulnerabilities”.

The project enforces signed RS256 JWTs, production HTTPS for identity metadata, customer-scoped database reads, identity rate limiting, bounded HTTP/JSON/business inputs, security headers, non-root runtime hardening, CodeQL/Gitleaks/Trivy and an **authenticated OWASP ZAP active API scan** against a disposable Keycloak → YARP → Orders stack.

The gate target is **no known High/Critical findings** without hiding findings merely to make CI pass. Independent manual penetration testing is still recommended before real production exposure.

See [security posture and pentest readiness](docs/security-posture.en.md).

### Secrets vault

The secure profile uses two Vault layers. RabbitMQ remains in KV v2, while PostgreSQL uses the Database Secrets Engine: Orders, Inventory and Payments receive a unique temporary login with TTL and a renewable lease. Each workload receives its own file-mounted Vault token and can only read/renew paths associated with that identity.

The PostgreSQL connection string is built only in memory. `ConnectionStrings__*-db` remains empty in the container environment, and the host terminates if the lease cannot be renewed — a fail-closed posture.

Production does not use dev mode/root tokens: prefer platform identity (for example Kubernetes Auth), short-lived tokens, TLS, auditing and a dedicated Vault database-administration identity. See [secrets management](docs/secrets-management.en.md), [ADR-0006](docs/adr/0006-secrets-hashicorp-vault.en.md) and [ADR-0007](docs/adr/0007-dynamic-postgresql-credentials.en.md).

---

## Database migrations and least privilege

Schema creation no longer belongs to application runtime. Orders, Inventory and Payments have committed **EF Core Migrations** plus a one-shot `DatabaseMigrator`.

The identity split is explicit:

```text
Vault <service>-migration -> temporary login -> <service>_migrator -> DDL
Vault <service>-app       -> temporary login -> <service>_runtime  -> DML only
```

Compose and Aspire wait for migration completion before starting the workload. The Kubernetes/GitOps model executes the same migrator as an Argo CD `PreSync` Job. CI fails if `EnsureCreatedAsync` returns or if runtime regains schema `CREATE`.

See [ADR-0011](docs/adr/0011-ef-migrations-vault-deployment-identity.en.md).

---

## Service Defaults and edge protection

All workloads use a shared Aspire-aligned building block:

- `/health` for readiness;
- `/alive` for liveness;
- shared OpenTelemetry;
- service discovery;
- Standard Resilience Handler for future `HttpClient` usage.

The YARP Gateway applies token-bucket rate limiting per authenticated subject with an IP fallback. Orders exposes `/openapi/v1.json` in Development, and the Aspire topology test validates that document automatically.

See [ADR-0009](docs/adr/0009-service-defaults-api-resilience.en.md).

---

## Software supply chain

Beyond CodeQL, Trivy and SBOM generation, the repository automates:

- weekly OpenSSF Scorecard with SARIF/Code Scanning results;
- immutable-SHA GitHub Action references;
- `CODEOWNERS` and `SECURITY.md`;
- automatic publishing of six OCI images to GHCR on `v*` tags;
- cryptographic provenance attestation for each OCI digest through GitHub OIDC + Sigstore.

Example release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Published image provenance can then be verified with `gh attestation verify`.

See [ADR-0010](docs/adr/0010-software-supply-chain.en.md).

---

## AI Engineering Harness

The [AI Evolution Harness](.github/workflows/ai-evolution.yml) is **installed on `main`** following the human-reviewed merge of [PR #19](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/19) (`d5f76b272d3b02826bd7b16eb4d032412bc5a015`). The workflow isolates `engineer` (Codex with GitHub read-only scope), `validate` (trusted governance guard and independent quality gates), and `publish` (patch SHA-256 verification, a review-only branch/PR, explicit CI/Security/DAST dispatch). Governance files and the guard itself are protected from agent edits.

**Operational status:** installation and governance-PR checks are verified, but **the first end-to-end agent run is not yet confirmed**. Maintainers must check the Actions secret `OPENAI_API_KEY`, review workflow permissions, and manually invoke `workflow_dispatch` on `main` with a harmless documentation task. [Issue #15](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/15) tracks this final acceptance; the agent cannot auto-merge or deploy.

**Implementation history:** [PR #17 — guard and tests](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/17) · [PR #18 — governance proposal](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/18) · [PR #19 — installed workflow](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/19). See the [AI-First operations and smoke-test guide](docs/ai-first-operations.en.md) and [ADR-0005](docs/adr/0005-ai-engineering-harness.en.md).

---

## Observability

All services use a shared OpenTelemetry building block. In the Aspire inner loop, the Dashboard aggregates resources, logs, traces and metrics. Docker Compose still includes OpenTelemetry Collector, Tempo, Prometheus and Grafana to demonstrate observability independently from Aspire, through:

- `ActivitySource` for distributed trace spans;
- custom message-processing metrics;
- OTLP export when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured;
- console export as the local fallback.

This keeps telemetry vendor-neutral.

---

## CI/CD

GitHub Actions validates every relevant change with:

1. dependency restore;
2. Release build;
3. explicit .NET Aspire AppHost validation;
4. automated tests;
5. code-coverage collection;
6. Sonar/.editorconfig/dotnet format quality gate;
7. Testcontainers integration tests;
8. API Gateway container build;
9. Orders container build;
10. Inventory container build;
11. Payments container build;
12. Notifications container build;
13. real Gateway + Keycloak + Vault smoke testing;
14. real dynamic PostgreSQL identity, environment isolation and lease-renewal verification;
15. CodeQL, Trivy and SPDX SBOM security gates.

Dependabot monitors NuGet and GitHub Actions dependencies.

---

## Architecture, contract and chaos tests

Executable quality gates protect properties beyond ordinary unit tests:

- **Architecture Tests** block forbidden dependencies across layers and bounded contexts;
- **Contract Compatibility Tests** guard the public shape of published integration events;
- **Chaos Tests** use Testcontainers + Toxiproxy to cut and restore PostgreSQL connectivity and assert failure/recovery.

See [ADR-0012](docs/adr/0012-architecture-contract-chaos-gitops.en.md).

---

## GitOps and canary delivery

`deploy/gitops` demonstrates **Argo CD + Argo Rollouts + Vault Kubernetes Auth**:

```text
v* tag
  -> GHCR images/attestations
  -> GitOps Promotion opens PR
  -> review + merge
  -> Argo CD PreSync migration
  -> 20% canary -> analysis -> 50% -> analysis -> 100%
```

CI never imperatively deploys production. Git is the source of truth and the promotion workflow never auto-merges.

See the [GitOps guide](deploy/gitops/README.en.md) and [ADR-0012](docs/adr/0012-architecture-contract-chaos-gitops.en.md).

---

## Performance baseline

The [Performance Baseline](.github/workflows/performance.yml) workflow runs k6 weekly or on demand against Orders using the secure Keycloak/Vault/PostgreSQL/RabbitMQ stack.

The baseline fails when HTTP errors reach 1%, fewer than 99% of order creations return HTTP 201, or p95 latency exceeds one second. This detects regressions without slowing every pull request.

See the [performance documentation](docs/performance.en.md).

---

## Kubernetes

The repository includes Kubernetes-oriented deployment examples under [deploy/k8s](deploy/k8s/README.en.md).

They demonstrate:

- liveness and readiness probes;
- resource requests and limits;
- ConfigMap/Secret separation;
- independently scalable services;
- stateless application containers.

RabbitMQ and PostgreSQL are treated as platform dependencies that would normally be provided through managed services or dedicated operators in production.

---

## Architecture decisions

- [ADR-0001 — Event-driven services and Clean Architecture](docs/adr/0001-event-driven-clean-architecture.md)
- [ADR-0002 — Transactional outbox instead of distributed transactions](docs/adr/0002-transactional-outbox.md)
- [ADR-0003 — Idempotency and eventual consistency](docs/adr/0003-idempotency-eventual-consistency.md)
- [ADR-0004 — Identity and authorization with Keycloak](docs/adr/0004-identity-keycloak.en.md)
- [ADR-0005 — AI-assisted engineering harness](docs/adr/0005-ai-engineering-harness.en.md)
- [ADR-0006 — Centralized secrets management with HashiCorp Vault](docs/adr/0006-secrets-hashicorp-vault.en.md)
- [ADR-0007 — Dynamic PostgreSQL credentials with Vault Database Secrets Engine](docs/adr/0007-dynamic-postgresql-credentials.en.md)
- [ADR-0008 — .NET Aspire as the local development orchestrator](docs/adr/0008-dotnet-aspire-local-orchestration.en.md)
- [ADR-0009 — Service Defaults, health model, OpenAPI and edge protection](docs/adr/0009-service-defaults-api-resilience.en.md)
- [ADR-0010 — Software supply chain and attested releases](docs/adr/0010-software-supply-chain.en.md)
- [ADR-0011 — EF Core Migrations with a separate Vault deployment identity](docs/adr/0011-ef-migrations-vault-deployment-identity.en.md)
- [ADR-0012 — Architecture guardrails, contracts, fault injection and progressive delivery](docs/adr/0012-architecture-contract-chaos-gitops.en.md)

---

## Repository structure

```text
src/
├── BuildingBlocks/
│   ├── Contracts/
│   ├── Observability/
│   ├── Security/
│   └── Secrets/
├── Gateway/
│   └── ApiGateway/
├── Platform/
│   ├── DistributedCommerce.AppHost/
│   └── DatabaseMigrator/
└── Services/
    ├── Orders/
    │   ├── Orders.Domain/
    │   ├── Orders.Application/
    │   ├── Orders.Infrastructure/
    │   └── Orders.Api/
    ├── Inventory/
    ├── Payments/
    └── Notifications/

tests/
├── Architecture.Tests/
├── Chaos.Tests/
├── Contracts.Compatibility.Tests/
├── DistributedCommerce.AppHost.Tests/
├── Orders.Domain.Tests/
└── Orders.Persistence.IntegrationTests/

docs/
├── architecture.md
└── adr/

deploy/
└── k8s/
```

---

## Engineering trade-offs

This is a portfolio/reference implementation, not a claim that every system should use microservices.

A modular monolith would be preferable for many smaller products. This project uses distributed services intentionally to make visible the concerns that only appear when boundaries are separated: delivery guarantees, idempotency, asynchronous state transitions, independent persistence, retry policy, operational health and observability.

That trade-off is documented rather than hidden.
