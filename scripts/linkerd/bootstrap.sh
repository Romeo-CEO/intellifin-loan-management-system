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

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")"/../.. && pwd)"

echo "[linkerd] running pre-flight checks"
linkerd check --pre

echo "[linkerd] installing control plane"
linkerd install | kubectl apply -f -
linkerd check

echo "[linkerd] installing viz extension"
linkerd viz install | kubectl apply -f -
linkerd viz check

echo "[linkerd] enabling namespace-wide proxy injection"
kubectl apply -k "${REPO_ROOT}/infra/linkerd"

echo "[linkerd] bootstrap complete"
