#!/bin/sh
set -eu

export VAULT_ADDR="${VAULT_ADDR:-http://vault:8200}"
export VAULT_TOKEN="${VAULT_DEV_ROOT_TOKEN_ID:?VAULT_DEV_ROOT_TOKEN_ID is required}"

shared_postgres_user="${POSTGRES_USER:-}"
shared_postgres_password="${POSTGRES_PASSWORD:-}"

orders_postgres_user="${ORDERS_POSTGRES_USER:-$shared_postgres_user}"
orders_postgres_password="${ORDERS_POSTGRES_PASSWORD:-$shared_postgres_password}"
inventory_postgres_user="${INVENTORY_POSTGRES_USER:-$shared_postgres_user}"
inventory_postgres_password="${INVENTORY_POSTGRES_PASSWORD:-$shared_postgres_password}"
payments_postgres_user="${PAYMENTS_POSTGRES_USER:-$shared_postgres_user}"
payments_postgres_password="${PAYMENTS_POSTGRES_PASSWORD:-$shared_postgres_password}"
customers_postgres_user="${CUSTOMERS_POSTGRES_USER:-$shared_postgres_user}"
customers_postgres_password="${CUSTOMERS_POSTGRES_PASSWORD:-$shared_postgres_password}"
products_postgres_user="${PRODUCTS_POSTGRES_USER:-$shared_postgres_user}"
products_postgres_password="${PRODUCTS_POSTGRES_PASSWORD:-$shared_postgres_password}"

vault secrets enable -path=secret kv-v2 >/dev/null 2>&1 || true
vault secrets enable database >/dev/null 2>&1 || true

vault kv put secret/platform/orders \
  RabbitMq__Username="${RABBITMQ_DEFAULT_USER}" \
  RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

vault kv put secret/platform/inventory \
  RabbitMq__Username="${RABBITMQ_DEFAULT_USER}" \
  RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

vault kv put secret/platform/payments \
  RabbitMq__Username="${RABBITMQ_DEFAULT_USER}" \
  RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

vault kv put secret/platform/notifications \
  RabbitMq__Username="${RABBITMQ_DEFAULT_USER}" \
  RabbitMq__Password="${RABBITMQ_DEFAULT_PASS}" >/dev/null

configure_database() {
  connection_name="$1"
  host="$2"
  database="$3"
  runtime_vault_role="$4"
  runtime_role="$5"
  migration_vault_role="$6"
  migration_role="$7"
  admin_user="$8"
  admin_password="$9"

  if [ -z "$admin_user" ] || [ -z "$admin_password" ]; then
    echo "Missing PostgreSQL bootstrap credentials for $connection_name." >&2
    exit 1
  fi

  attempt=1
  max_attempts=30

  while ! vault write "database/config/$connection_name" \
      plugin_name="postgresql-database-plugin" \
      allowed_roles="$runtime_vault_role,$migration_vault_role" \
      connection_url="postgresql://{{username}}:{{password}}@$host:5432/$database?sslmode=disable" \
      username="$admin_user" \
      password="$admin_password" \
      password_authentication="scram-sha-256" >/dev/null 2>&1; do
    if [ "$attempt" -ge "$max_attempts" ]; then
      echo "Could not configure Vault database connection $connection_name after $max_attempts attempts." >&2
      exit 1
    fi

    echo "Waiting for PostgreSQL resource $connection_name to become reachable ($attempt/$max_attempts)..."
    attempt=$((attempt + 1))
    sleep 2
  done

  vault write "database/roles/$runtime_vault_role" \
    db_name="$connection_name" \
    creation_statements="CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}' INHERIT; GRANT $runtime_role TO \"{{name}}\";" \
    renew_statements="ALTER ROLE \"{{name}}\" VALID UNTIL '{{expiration}}';" \
    revocation_statements="REVOKE $runtime_role FROM \"{{name}}\"; DROP ROLE IF EXISTS \"{{name}}\";" \
    rollback_statements="DROP ROLE IF EXISTS \"{{name}}\";" \
    default_ttl="5m" \
    max_ttl="24h" >/dev/null

  vault write "database/roles/$migration_vault_role" \
    db_name="$connection_name" \
    creation_statements="CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}' INHERIT; GRANT $migration_role TO \"{{name}}\";" \
    renew_statements="ALTER ROLE \"{{name}}\" VALID UNTIL '{{expiration}}';" \
    revocation_statements="REVOKE $migration_role FROM \"{{name}}\"; DROP ROLE IF EXISTS \"{{name}}\";" \
    rollback_statements="DROP ROLE IF EXISTS \"{{name}}\";" \
    default_ttl="15m" \
    max_ttl="1h" >/dev/null
}

configure_database \
  orders orders-db orders \
  orders-app orders_runtime \
  orders-migration orders_migrator \
  "$orders_postgres_user" "$orders_postgres_password"

configure_database \
  inventory inventory-db inventory \
  inventory-app inventory_runtime \
  inventory-migration inventory_migrator \
  "$inventory_postgres_user" "$inventory_postgres_password"

configure_database \
  payments payments-db payments \
  payments-app payments_runtime \
  payments-migration payments_migrator \
  "$payments_postgres_user" "$payments_postgres_password"

configure_database \
  customers customers-db customers \
  customers-app customers_runtime \
  customers-migration customers_migrator \
  "$customers_postgres_user" "$customers_postgres_password"

configure_database \
  products products-db products \
  products-app products_runtime \
  products-migration products_migrator \
  "$products_postgres_user" "$products_postgres_password"

write_token_file() {
  token_dir="$1"
  token="$2"

  mkdir -p "$token_dir"
  printf '%s' "$token" > "$token_dir/token"
  chmod "${VAULT_TOKEN_FILE_MODE:-0400}" "$token_dir/token"

  if [ -n "${VAULT_TOKEN_FILE_OWNER:-}" ]; then
    chown "$VAULT_TOKEN_FILE_OWNER" "$token_dir/token"
  fi
}

write_service_policy_and_token() {
  service="$1"
  secret_path="$2"
  database_role="${3:-}"
  policy_file="/tmp/$service-policy.hcl"

  cat > "$policy_file" <<EOF
path "secret/data/$secret_path" {
  capabilities = ["read"]
}
EOF

  if [ -n "$database_role" ]; then
    cat >> "$policy_file" <<EOF

path "database/creds/$database_role" {
  capabilities = ["read"]
}

path "sys/leases/renew/database/creds/$database_role/*" {
  capabilities = ["update"]
}
EOF
  fi

  vault policy write "$service-service" "$policy_file" >/dev/null

  token="$(
    vault token create \
      -field=token \
      -policy="$service-service" \
      -ttl=24h \
      -renewable=true
  )"

  write_token_file "/tokens/$service" "$token"
}

write_migration_policy_and_token() {
  service="$1"
  database_role="$2"
  policy_file="/tmp/$service-migration-policy.hcl"

  cat > "$policy_file" <<EOF
path "database/creds/$database_role" {
  capabilities = ["read"]
}
EOF

  vault policy write "$service-migration" "$policy_file" >/dev/null

  token="$(
    vault token create \
      -field=token \
      -policy="$service-migration" \
      -ttl=30m \
      -renewable=false
  )"

  write_token_file "/tokens/$service-migrator" "$token"
}

write_service_policy_and_token orders platform/orders orders-app
write_service_policy_and_token inventory platform/inventory inventory-app
write_service_policy_and_token payments platform/payments payments-app
write_service_policy_and_token notifications platform/notifications
write_service_policy_and_token customers platform/customers customers-app
write_service_policy_and_token products platform/products products-app

write_migration_policy_and_token orders orders-migration
write_migration_policy_and_token inventory inventory-migration
write_migration_policy_and_token payments payments-migration
write_migration_policy_and_token customers customers-migration
write_migration_policy_and_token products products-migration

echo "Vault KV secrets, runtime roles, migration roles and scoped tokens are ready."
