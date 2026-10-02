#!/usr/bin/env bash
# Single entry point for production deploys (spec 14 §3.12): used by the deploy-server workflow and by the incident runbook.
#   burdock-deploy.sh <api image tag>
# Steps: decrypt secrets → remember the previous tag → pull → migrate (expand phase) → start → wait for readiness →
# smoke /v1/config → record the tag; on failure roll back to the previous tag.
set -euo pipefail

TAG="${1:?usage: burdock-deploy.sh <api image tag>}"
ROOT="${BURDOCK_ROOT:-/opt/burdock}"
COMPOSE=(docker compose -f "$ROOT/compose.yaml" -f "$ROOT/compose.prod.yaml" --env-file "$ROOT/.env")
STATE="$ROOT/deploy-state.json"
PREVIOUS_TAG="$(python3 -c "import json,sys; print(json.load(open(sys.argv[1])).get('tag',''))" "$STATE" 2>/dev/null || true)"

echo "deploy: $PREVIOUS_TAG -> $TAG"
umask 077
sops -d "$ROOT/secrets/prod.enc.env" > "$ROOT/.env"

export API_IMAGE_TAG="$TAG"
"${COMPOSE[@]}" pull api
"${COMPOSE[@]}" run --rm --no-deps api migrate
"${COMPOSE[@]}" up -d --no-deps api

healthy=0
for _ in $(seq 1 30); do
  if "${COMPOSE[@]}" exec -T api curl -fsS http://127.0.0.1:8080/health/ready >/dev/null 2>&1; then
    healthy=1
    break
  fi
  sleep 2
done

if [ "$healthy" = "1" ] && curl -fsS "https://api.burdock.org/v1/config" >/dev/null; then
  printf '{"tag": "%s", "deployedAt": "%s"}\n' "$TAG" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$STATE"
  "${COMPOSE[@]}" up -d caddy
  echo "deploy OK: $TAG"
  exit 0
fi

echo "deploy FAILED: readiness or smoke test did not pass; rolling back to ${PREVIOUS_TAG:-<none>}" >&2
"${COMPOSE[@]}" logs --tail=200 api >&2 || true
if [ -n "$PREVIOUS_TAG" ]; then
  API_IMAGE_TAG="$PREVIOUS_TAG" "${COMPOSE[@]}" up -d --no-deps api
fi
exit 1
