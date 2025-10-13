#!/usr/bin/env bash
set -euo pipefail

# Optional overrides for non-default namespace names
NAMESPACE_GATEWAY=${NAMESPACE_GATEWAY:-gateway}
NAMESPACE_LENDING=${NAMESPACE_LENDING:-lending}
NAMESPACE_ADMIN=${NAMESPACE_ADMIN:-admin}

SERVICE_GATEWAY_TARGET=${SERVICE_GATEWAY_TARGET:-loan-origination}
SERVICE_ADMIN_TARGET=${SERVICE_ADMIN_TARGET:-admin-service}
SERVICE_KEYCLOAK_TARGET=${SERVICE_KEYCLOAK_TARGET:-keycloak}

CURL_IMAGE=${CURL_IMAGE:-curlimages/curl:8.6.0}
REQUEST_TIMEOUT=${REQUEST_TIMEOUT:-5}

log() {
  printf '\n[%s] %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"
}

run_curl() {
  local name="$1"
  shift
  kubectl run "$name" \
    --image="$CURL_IMAGE" \
    --restart=Never \
    --rm \
    --attach \
    "$@"
}

log "Test 1: API Gateway should reach Loan Origination"
run_curl test-from-gateway \
  --namespace "$NAMESPACE_GATEWAY" \
  -- curl -sf -m "$REQUEST_TIMEOUT" \
    "http://${SERVICE_GATEWAY_TARGET}.${NAMESPACE_LENDING}.svc.cluster.local:8080/health"
log "✅ API Gateway -> Loan Origination succeeded"

log "Test 2: Loan Origination should NOT reach Admin Service"
if run_curl test-from-loan \
  --namespace "$NAMESPACE_LENDING" \
  -- curl -sv -m "$REQUEST_TIMEOUT" \
    "http://${SERVICE_ADMIN_TARGET}.${NAMESPACE_ADMIN}.svc.cluster.local:8080/health"; then
  log "❌ Loan Origination unexpectedly reached Admin Service"
  exit 1
else
  log "✅ Loan Origination blocked from Admin Service as expected"
fi

log "Test 3: Admin Service should reach Keycloak"
run_curl test-from-admin \
  --namespace "$NAMESPACE_ADMIN" \
  -- curl -sf -m "$REQUEST_TIMEOUT" \
    "http://${SERVICE_KEYCLOAK_TARGET}.keycloak.svc.cluster.local:8080/health"
log "✅ Admin Service -> Keycloak succeeded"
