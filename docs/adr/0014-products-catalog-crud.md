[🇺🇸 English](0014-products-catalog-crud.en.md)

# ADR-0014 — Catálogo de produtos em serviço dedicado

## Status

Proposto — aguardando validação completa de CI, segurança e revisão humana.

## Contexto

A plataforma precisa cadastrar produtos sem misturar catálogo com pedidos ou quantidade em estoque. A arquitetura existente usa Clean Architecture, banco por serviço, JWT/Keycloak, Vault com identidades de migração separadas e .NET Aspire.

## Decisão

Adicionar o bounded context **Products**, com camadas Domain/Application/Infrastructure/Api, PostgreSQL próprio e EF Core migrations aplicadas por `DatabaseMigrator`.

- Campos: `id` (GUID), `sku` único (normalizado para maiúsculas), `name`, `description`, `category`, `brand`, `barcode` único quando informado, `unit`, `price`, `currency=BRL`, `isActive` e timestamps.
- SKU tem 3–64 caracteres ASCII; preço positivo, com até duas casas decimais (tipo `numeric(18,2)`); código de barras aceita formatos numéricos de 8, 12, 13 ou 14 dígitos, sem afirmar validade GTIN.
- `POST /api/products`, `GET /api/products/{id}`, `GET /api/products` (paginado e filtrado), `PUT /api/products/{id}` (substituição completa), `DELETE /api/products/{id}` (exclusão física).
- GET: roles `customer` ou `admin`; escrita: somente `admin`. JWT é validado no Gateway e novamente em Products.
- Banco `products-db`, `products_runtime` DML e `products_migrator` DDL; credenciais dinâmicas providas por Vault.
- Não há tabela de estoque nem chamada síncrona para Inventory. Integração por eventos fica para evolução futura com contrato e testes apropriados.

## Operação e testes

Iniciar com `dotnet run --project src/Platform/DistributedCommerce.AppHost` ou Compose + overlay Vault. Executar `bash scripts/products-smoke.sh` após o ambiente subir (requer `curl`, `jq`, e os usuários fictícios locais do Keycloak). Testes de domínio e de integração usam NUnit e Testcontainers PostgreSQL 18 com migration real.

**Não publicar em produção nem mesclar antes da aprovação de CI, Security, testes e revisão humana.** O workflow AI Evolution Harness requer disparo e validação independentes; este PR criado com assistência direta não comprova sua execução.
