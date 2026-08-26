#!/usr/bin/env bash
# Drives a running scaffolded API over real HTTP and asserts every behaviour the README
# promises a consumer - including the failure paths, which are the ones that quietly rot.
#
#   verify-scaffolded-api.sh <base-url> <ServiceName>
#
# Template-repo only - excluded from scaffolded projects by template.json.
set -uo pipefail

base="${1:?usage: verify-scaffolded-api.sh <base-url> <ServiceName>}"
service="${2:?usage: verify-scaffolded-api.sh <base-url> <ServiceName>}"
fail=0
body=$(mktemp)

check() {
  if [ "$1" -eq 0 ]; then
    echo "  PASS  $2"
  else
    echo "  FAIL  $2"
    fail=1
  fi
}

# req <METHOD> <path> [data] [auth-header] -> echoes status, leaves response in $body
req() {
  local method="$1" path="$2" data="${3:-}" auth="${4:-}"
  local args=(-s -o "$body" -w '%{http_code}' -X "$method" "$base$path")
  [ -n "$data" ] && args+=(-H 'Content-Type: application/json' -d "$data")
  [ -n "$auth" ] && args+=(-H "Authorization: Bearer $auth")
  curl "${args[@]}"
}

status_is() { # <expected> <actual> <description>
  [ "$2" = "$1" ]
  check $? "$3 (expected $1, got $2)"
}

echo "== Public endpoints (README: 'these work with zero configuration')"
status_is 200 "$(req GET /health)" "GET /health"

code=$(req GET /)
status_is 200 "$code" "GET /"
jq -e --arg s "$service.Api" '.service == $s and .status == "running"' "$body" >/dev/null
check $? "GET / reports service '$service.Api' and status running"

code=$(req GET /openapi/v1.json)
status_is 200 "$code" "GET /openapi/v1.json"
jq -e '.paths | has("/api/v1/todo-items")' "$body" >/dev/null
check $? "OpenAPI document lists the CRUD route"
jq -e '.paths | has("/api/v1/auth/register") and has("/api/v1/auth/login")' "$body" >/dev/null
check $? "OpenAPI document lists the auth routes"
jq -e '.paths | has("/api/v1/auth/me/phone")' "$body" >/dev/null
check $? "OpenAPI document lists the profile route"

status_is 200 "$(req GET /scalar/v1)" "GET /scalar/v1"

echo
echo "== Secure by default"
status_is 401 "$(req GET /api/v1/todo-items)" "CRUD list without a token is rejected"
status_is 401 "$(req GET /api/v1/todo-items '' 'not-a-real-token')" "CRUD list with a garbage token is rejected"
status_is 401 "$(req PUT /api/v1/auth/me/phone '{"phoneNumber":"+989123456789"}')" "profile update without a token is rejected"

echo
echo "== Register / login"
email="user-$RANDOM@example.com"
code=$(req POST /api/v1/auth/register "{\"email\":\"$email\",\"password\":\"correct-horse-battery\"}")
status_is 201 "$code" "register a new user"
jq -e '.token != "" and .userId > 0 and (.expiresAt | length) > 0' "$body" >/dev/null
check $? "register returns userId, token and expiresAt"
token=$(jq -r '.token' "$body")

code=$(req POST /api/v1/auth/register "{\"email\":\"$email\",\"password\":\"another-password\"}")
status_is 409 "$code" "registering a taken email conflicts"

code=$(req POST /api/v1/auth/login "{\"email\":\"$email\",\"password\":\"correct-horse-battery\"}")
status_is 200 "$code" "login with correct credentials"
login_token=$(jq -r '.token' "$body")

code=$(req POST /api/v1/auth/login "{\"email\":\"$email\",\"password\":\"wrong-password\"}")
status_is 401 "$code" "login with a wrong password"
# ProblemDetails carries a per-request traceId, so compare everything else.
wrong_pw_body=$(jq -S 'del(.traceId, .instance)' "$body")

code=$(req POST /api/v1/auth/login '{"email":"nobody-here@example.com","password":"correct-horse-battery"}')
status_is 401 "$code" "login with an unknown email"
[ "$(jq -S 'del(.traceId, .instance)' "$body")" = "$wrong_pw_body" ]
check $? "wrong password and unknown email are indistinguishable (no user enumeration)"

echo
echo "== CRUD lifecycle with a token from login"
code=$(req POST /api/v1/todo-items '{"title":"buy milk","isDone":false}' "$login_token")
status_is 201 "$code" "create a todo item"
id=$(jq -r '.id' "$body")
location=$(curl -s -D - -o /dev/null -X POST "$base/api/v1/todo-items" \
  -H 'Content-Type: application/json' -H "Authorization: Bearer $login_token" \
  -d '{"title":"second item","isDone":false}' | grep -i '^location:' || true)
[ -n "$location" ]
check $? "create returns a Location header (IHasId) -> ${location:-none}"

code=$(req GET "/api/v1/todo-items/$id" '' "$login_token")
status_is 200 "$code" "read it back by id"

code=$(req GET '/api/v1/todo-items?pageIndex=1&pageSize=1' '' "$login_token")
status_is 200 "$code" "list with paging"
jq -e 'has("totalPages") and has("hasNextPage") and has("hasPreviousPage")' "$body" >/dev/null
check $? "list carries PagedResult metadata"
jq -e '.items | length == 1' "$body" >/dev/null
check $? "pageSize=1 returns exactly one item"
jq -e '.hasNextPage == true and .hasPreviousPage == false' "$body" >/dev/null
check $? "paging metadata is computed correctly on page 1 of 2"

code=$(req PUT "/api/v1/todo-items/$id" "{\"id\":$id,\"title\":\"buy oat milk\",\"isDone\":true}" "$login_token")
status_is 200 "$code" "update it"
jq -e '.title == "buy oat milk" and .isDone == true' "$body" >/dev/null
check $? "update persisted the new values"

status_is 204 "$(req DELETE "/api/v1/todo-items/$id" '' "$login_token")" "delete it"

echo
echo "== Error contract (RFC 7807 ProblemDetails)"
code=$(curl -s -o "$body" -w '%{content_type}|%{http_code}' \
  -H "Authorization: Bearer $login_token" "$base/api/v1/todo-items/999999")
echo "$code" | grep -q '^application/problem+json'
check $? "404 is served as application/problem+json (got $code)"
echo "$code" | grep -q '|404$'
check $? "reading a deleted/missing item is a 404"

code=$(req POST /api/v1/todo-items '{"title":"","isDone":false}' "$login_token")
status_is 400 "$code" "a failing validator returns 400"
jq -e '.errors | has("Dto.Title")' "$body" >/dev/null
check $? "400 carries a per-property errors object"

echo
echo "== Self-service profile: phone numbers from any country"
code=$(req PUT /api/v1/auth/me/phone '{"phoneNumber":"09123456789","region":"IR"}' "$login_token")
status_is 200 "$code" "local-format Iranian number with region"
jq -e '.phoneNumber == "+989123456789"' "$body" >/dev/null
check $? "stored normalised to E.164"

code=$(req PUT /api/v1/auth/me/phone '{"phoneNumber":"+16502530000"}' "$login_token")
status_is 200 "$code" "international-format US number without region"
jq -e '.phoneNumber == "+16502530000"' "$body" >/dev/null
check $? "US number stored as given"

code=$(req PUT /api/v1/auth/me/phone '{"phoneNumber":"09123456789"}' "$login_token")
status_is 400 "$code" "local-format number with no region is rejected as ambiguous"

# A second user may not take the first user's number.
other="other-$RANDOM@example.com"
req POST /api/v1/auth/register "{\"email\":\"$other\",\"password\":\"correct-horse-battery\"}" >/dev/null
other_token=$(jq -r '.token' "$body")
req PUT /api/v1/auth/me/phone '{"phoneNumber":"+16502530000"}' "$other_token" >/dev/null
code=$(req PUT /api/v1/auth/me/phone '{"phoneNumber":"+16502530000"}' "$other_token")
status_is 409 "$code" "a number already taken by another user conflicts"

code=$(req PUT /api/v1/auth/me/phone '{"phoneNumber":null}' "$login_token")
status_is 200 "$code" "clearing the number"
jq -e '.phoneNumber == null' "$body" >/dev/null
check $? "number cleared"

echo
echo "== API versioning"
status_is 404 "$(req GET /api/v2/todo-items '' "$login_token")" "an unknown version is not routed"

rm -f "$body"
echo
[ "$fail" -eq 0 ] && echo "== API verification passed" || echo "== API verification FAILED"
exit "$fail"
