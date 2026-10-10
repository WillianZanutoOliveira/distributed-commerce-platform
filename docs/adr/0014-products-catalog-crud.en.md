[🇧🇷 Português](0014-products-catalog-crud.md)

# ADR-0014 — Dedicated product catalog service

## Status

Proposed — awaiting CI, security validation and human review.

## Context

The platform needs product registration without merging catalog data with orders or inventory counts. Existing rules require Clean Architecture, database-per-service, Keycloak JWT, separate Vault-backed migration identities, and .NET Aspire.

## Decision

Introduce the **Products** bounded context with Domain/Application/Infrastructure/Api layers, dedicated PostgreSQL, and EF Core migrations run by `DatabaseMigrator`.

- Fields: GUID `id`, unique normalized `sku`, `name`, `description`, `category`, `brand`, unique optional `barcode`, `unit`, `price`, fixed `currency=BRL`, `isActive`, timestamps.
- SKU: 3–64 ASCII characters. Price: positive, maximum two fractional digits (`numeric(18,2)`). Barcode: numeric 8/12/13/14-digit format only; GTIN check-digit validity is not asserted.
- `POST /api/products`, `GET /api/products/{id}`, `GET /api/products` (filtered/paged), `PUT /api/products/{id}` (full replacement), `DELETE /api/products/{id}` (physical removal).
- GET roles: `customer` or `admin`; write operations: `admin` only. JWT is validated at Gateway and in Products.
- Dedicated `products-db`, DML-only `products_runtime` and DDL-capable `products_migrator`, using dynamic Vault credentials.
- Stock and reservations remain Inventory's responsibility; cross-context event integration requires a separate reviewed design.

## Operation and tests

Run via `dotnet run --project src/Platform/DistributedCommerce.AppHost` or Compose with Vault overlay. Once started, execute `bash scripts/products-smoke.sh` (requires `curl`, `jq`, local Keycloak fixture users). NUnit and PostgreSQL 18 Testcontainers tests cover domain rules, uniqueness, CRUD persistence, and migrations.

**Do not promote to production or merge before green CI/Security and human review.** This connector-assisted PR is not evidence of an AI Evolution Harness workflow dispatch.
