#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")"/../.. && pwd)"
KUSTOMIZE_PATH="${REPO_ROOT}/infra/keycloak"

if ! command -v kubectl >/dev/null 2>&1; then
  echo "kubectl is required" >&2
  exit 1
fi

kubectl apply -f "${KUSTOMIZE_PATH}/namespace.yaml"
kubectl apply -k "${KUSTOMIZE_PATH}"
