#!/usr/bin/env bash
set -euo pipefail

if ! command -v docker >/dev/null 2>&1; then
  echo "docker is required to build the Admin Service image" >&2
  exit 1
fi

if ! command -v cosign >/dev/null 2>&1; then
  echo "cosign is required to sign the Admin Service image" >&2
  exit 1
fi

IMAGE_NAME=${IMAGE_NAME:-ghcr.io/intellifin/admin-service}
IMAGE_TAG=${IMAGE_TAG:-latest}
FULL_IMAGE="${IMAGE_NAME}:${IMAGE_TAG}"
CONTEXT_DIR=${CONTEXT_DIR:-$(git rev-parse --show-toplevel)}
DOCKERFILE_PATH="${CONTEXT_DIR}/apps/IntelliFin.AdminService/Dockerfile"

COSIGN_KEY_REF=${COSIGN_KEY_REF:-}
COSIGN_KEY_PATH=${COSIGN_KEY_PATH:-}
COSIGN_PASSWORD_ENV=${COSIGN_PASSWORD:-${COSIGN_PASSWORD_ENV:-}}

if [[ -z "${COSIGN_KEY_REF}" && -z "${COSIGN_KEY_PATH}" ]]; then
  echo "Set COSIGN_KEY_REF (KMS reference) or COSIGN_KEY_PATH (filesystem path) before signing" >&2
  exit 1
fi

echo "Building ${FULL_IMAGE} using ${DOCKERFILE_PATH}" >&2
docker build -f "${DOCKERFILE_PATH}" -t "${FULL_IMAGE}" "${CONTEXT_DIR}"

echo "Signing ${FULL_IMAGE} with cosign" >&2
if [[ -n "${COSIGN_KEY_REF}" ]]; then
  cosign sign --key "${COSIGN_KEY_REF}" "${FULL_IMAGE}"
else
  if [[ -n "${COSIGN_PASSWORD_ENV}" ]]; then
    COSIGN_PASSWORD="${COSIGN_PASSWORD_ENV}" cosign sign --key "${COSIGN_KEY_PATH}" "${FULL_IMAGE}"
  else
    cosign sign --key "${COSIGN_KEY_PATH}" "${FULL_IMAGE}"
  fi
fi

echo "Image ${FULL_IMAGE} built and signed successfully" >&2
