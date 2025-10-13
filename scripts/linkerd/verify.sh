#!/usr/bin/env bash
set -euo pipefail

if ! command -v linkerd >/dev/null 2>&1; then
  echo "linkerd CLI is required" >&2
  exit 1
fi

if ! command -v kubectl >/dev/null 2>&1; then
  echo "kubectl is required" >&2
  exit 1
fi

echo "[linkerd] checking control-plane health"
linkerd check

if linkerd viz check >/dev/null 2>&1; then
  echo "[linkerd] viz extension is healthy"
else
  echo "[linkerd] warning: viz extension not reachable; TLS edge inspection may be incomplete" >&2
fi

echo "[linkerd] checking proxy health across namespaces"
for ns in gateway identity admin lending integrations communications collections reporting finance kyc offline keycloak; do
  if kubectl get namespace "$ns" >/dev/null 2>&1; then
    linkerd check --proxy -n "$ns"
  fi
done

echo "[linkerd] verifying mTLS edges"
edges_output=$(linkerd viz edges deploy -A || true)
printf '%s\n' "$edges_output"
if printf '%s\n' "$edges_output" | grep -Ei 'TLS[^[:alnum:]]+(false|disabled|-)' >/dev/null; then
  echo "[linkerd] detected unsecured edges above" >&2
  exit 1
fi

echo "[linkerd] verifying proxy restart stability"
if kubectl get --raw "/apis/metrics.k8s.io/" >/dev/null 2>&1; then
  echo "[linkerd] metrics API reachable"
fi
if command -v jq >/dev/null 2>&1; then
  kubectl get pods -A -l linkerd.io/proxy-deployment -o json \
    | jq -r '.items[] | "\(.metadata.namespace)/\(.metadata.name): restarts=\(.status.containerStatuses[] | select(.name==\"linkerd-proxy\").restartCount)"' 2>/dev/null || true
else
  echo "[linkerd] jq not found; skipping detailed restart summary"
fi

echo "[linkerd] verification complete"
