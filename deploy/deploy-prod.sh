#!/usr/bin/env bash
#
# deploy-prod.sh — ship the PurePrep backend to the Hetzner host.
#
# The server has a copy of the source (not a git checkout), so this syncs the files the image is
# built from, then rebuilds the container in place. Coldstart's Caddy handles TLS and routing.
#
# It REFUSES to deploy while Google Play validation is denied. Deploying then would close purchase
# forgery but also reject genuine purchases — customers charged, no credits granted.
#
# Usage:
#   ./deploy/deploy-prod.sh --confirm [/path/to/service-account.json]
#
set -euo pipefail

HOST="${PUREPREP_HOST:-coldstart-prod}"
PUBLIC_URL="${PUREPREP_PUBLIC_URL:-https://api.pureprep.lechdigital.nl}"
REMOTE_DIR="${PUREPREP_REMOTE_DIR:-/opt/pureprep}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
KEY="${2:-${PLAY_KEY_PATH_LOCAL:-}}"

if [ "${1:-}" != "--confirm" ]; then
  echo "This deploys to production ($HOST:$REMOTE_DIR)." >&2
  echo "Re-run with --confirm to proceed." >&2
  exit 2
fi

# ---- Interlock 1: Play validation must be live -----------------------------------------------
if [ -n "$KEY" ]; then
  echo "==> Checking Google Play access before deploying"
  if ! "$ROOT/deploy/verify-play-access.sh" "$KEY" >/dev/null 2>&1; then
    echo
    echo "REFUSING TO DEPLOY: Google Play validation is not live yet." >&2
    echo "Run ./deploy/verify-play-access.sh '$KEY' for details." >&2
    echo "Deploying now would reject genuine purchases as well as forged ones." >&2
    exit 1
  fi
  echo "    Play access OK."
else
  echo "!! No key path given, skipping the Play access check."
  echo "!! Only do this if you have already verified it separately."
fi

# ---- Interlock 2: local tests must pass -------------------------------------------------------
echo "==> Running tests"
dotnet test "$ROOT/PurePrep.slnx" -c Release --nologo -v q >/dev/null || {
  echo "REFUSING TO DEPLOY: tests are failing." >&2; exit 1; }
echo "    Tests green."

# ---- Sync ------------------------------------------------------------------------------------
echo "==> Syncing source to $HOST:$REMOTE_DIR"
rsync -az --delete \
  --exclude 'bin/' --exclude 'obj/' --exclude '.git/' \
  "$ROOT/src/PurePrep.Core" "$ROOT/src/PurePrep.Server" \
  "$HOST:$REMOTE_DIR/src/"
rsync -az "$ROOT/Dockerfile" "$ROOT/NuGet.config" "$HOST:$REMOTE_DIR/"
rsync -az "$ROOT/deploy/docker-compose.prod.yml" "$HOST:$REMOTE_DIR/deploy/"

# ---- Build & restart -------------------------------------------------------------------------
echo "==> Rebuilding container"
ssh "$HOST" "cd $REMOTE_DIR && docker compose --env-file .env -f deploy/docker-compose.prod.yml up -d --build"

# ---- Verify ----------------------------------------------------------------------------------
# The container only publishes 8080 to the Docker network (compose uses `expose`, not `ports`),
# so it is NOT reachable on the host's localhost. Health is checked through the public URL, which
# also exercises Caddy's routing to this container rather than a neighbour's.
echo "==> Waiting for health"
for _ in $(seq 1 30); do
  sleep 2
  if curl -sf -m 10 "$PUBLIC_URL/health" >/dev/null 2>&1; then
    echo "    Healthy: $(curl -s -m 10 "$PUBLIC_URL/health")"
    exit 0
  fi
done

echo >&2
echo "DEPLOY FAILED: the container did not become healthy." >&2
echo "The most likely cause is the Play key — the server refuses to start without a usable one." >&2
# --env-file is required here too: without it compose refuses to interpolate the service
# definition and reports a missing-variable error instead of the container's actual logs.
ssh "$HOST" "cd $REMOTE_DIR && docker compose --env-file .env -f deploy/docker-compose.prod.yml logs --tail 30 pureprep" >&2
exit 1
