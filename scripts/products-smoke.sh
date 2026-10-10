#!/usr/bin/env bash
set -euo pipefail

GATEWAY_BASE="${GATEWAY_BASE:-http://localhost:8080}"
KEYCLOAK_BASE="${KEYCLOAK_BASE:-http://localhost:8180}"

token_for() {
  curl -fsS -X POST "$KEYCLOAK_BASE/realms/distributed-commerce/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    --data-urlencode "grant_type=password" \
    --data-urlencode "client_id=commerce-cli" \
    --data-urlencode "username=$1" \
    --data-urlencode "password=local-demo-only" | jq -er '.access_token'
}

admin_token="$(token_for demo-admin)"
customer_token="$(token_for demo-customer)"
api="$GATEWAY_BASE/api/products"
sku="SMOKE-$(date +%s)-$RANDOM"
payload="$(jq -n --arg sku "$sku" '{sku:$sku,name:"Demo Product",description:"Disposable fixture",
  category:"Tests",brand:"Fixture",barcode:null,unit:"UN",price:19.90}')"

customer_status="$(curl -sS -o /dev/null -w '%{http_code}' -X POST "$api" \
    -H "Authorization: Bearer $customer_token" -H "Content-Type: application/json" -d "$payload")"
test "$customer_status" = "403"

created="$(curl -fsS -X POST "$api" -H "Authorization: Bearer $admin_token" \
    -H "Content-Type: application/json" -d "$payload")"
id="$(printf '%s' "$created" | jq -er '.id')"
trap 'curl -fsS -X DELETE "$api/$id" -H "Authorization: Bearer $admin_token" >/dev/null || true' EXIT

curl -fsS "$api/$id" -H "Authorization: Bearer $customer_token" |
  jq -e --arg sku "$sku" '.sku == $sku' >/dev/null

curl -fsS "$api?category=Tests&page=1&pageSize=100" -H "Authorization: Bearer $customer_token" |
  jq -e --arg id "$id" 'any(.items[]; .id == $id)' >/dev/null

duplicate="$(curl -sS -o /dev/null -w '%{http_code}' -X POST "$api" \
  -H "Authorization: Bearer $admin_token" -H "Content-Type: application/json" -d "$payload")"
test "$duplicate" = "409"

updated="$(printf '%s' "$payload" | jq '. + {name:"Updated fixture",isActive:false,price:24.90}')"
curl -fsS -X PUT "$api/$id" -H "Authorization: Bearer $admin_token" \
  -H "Content-Type: application/json" -d "$updated" |
  jq -e '.name == "Updated fixture" and .isActive == false' >/dev/null

test "$(curl -sS -o /dev/null -w '%{http_code}' -X DELETE "$api/$id" \
  -H "Authorization: Bearer $admin_token")" = "204"
trap - EXIT

test "$(curl -sS -o /dev/null -w '%{http_code}' "$api/$id" \
  -H "Authorization: Bearer $customer_token")" = "404"

echo "Products CRUD, RBAC, filtering and uniqueness smoke passed."
