#!/usr/bin/env bash
#
# verify-play-access.sh — check whether the Google Play service account can actually read
# purchases for this app, before trusting it in production.
#
# Google permission changes propagate slowly (minutes to ~24h), and until they land the server
# validates every purchase as invalid — meaning real customers would be charged and get nothing.
# Run this until it reports READY.
#
# Usage:
#   ./deploy/verify-play-access.sh /path/to/service-account.json
#
set -uo pipefail

KEY="${1:-${PLAY_KEY_PATH:-}}"
PACKAGE="${PLAY_PACKAGE_NAME:-com.adziusmaster.pureprep}"
PORT="${PORT:-5399}"

if [ -z "$KEY" ] || [ ! -f "$KEY" ]; then
  echo "usage: $0 /path/to/service-account.json" >&2
  exit 2
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"; [ -n "${PID:-}" ] && kill "$PID" 2>/dev/null' EXIT

echo "Building server..."
dotnet build "$ROOT/src/PurePrep.Server" -c Release --nologo -v q >/dev/null 2>&1 || {
  echo "BUILD FAILED" >&2; exit 1; }

ASPNETCORE_URLS="http://127.0.0.1:$PORT" \
ConnectionStrings__Db="Data Source=$WORK/verify.db" \
ASPNETCORE_ENVIRONMENT=Production \
Security__IpHashSalt=verify-only \
Play__ServiceAccountJsonPath="$KEY" \
Play__PackageName="$PACKAGE" \
dotnet run --project "$ROOT/src/PurePrep.Server" --no-build -c Release > "$WORK/verify.log" 2>&1 &
PID=$!

for _ in $(seq 1 30); do
  sleep 1
  curl -sf "http://127.0.0.1:$PORT/health" >/dev/null 2>&1 && break
done

if ! curl -sf "http://127.0.0.1:$PORT/health" >/dev/null 2>&1; then
  echo
  echo "RESULT: server refused to start — the key could not be loaded."
  grep -oE "The Google Play service-account key at .* could not be loaded\.|Google Play purchase validation is not configured\..*" "$WORK/verify.log" | head -1
  exit 1
fi

# A deliberately invalid token. What matters is HOW the stack refuses it.
STATUS=$(curl -s -o /dev/null -w '%{http_code}' -X POST "http://127.0.0.1:$PORT/api/billing/redeem" \
  -H 'Content-Type: application/json' \
  -d '{"deviceId":"00000000-0000-0000-0000-0000000000ff","productId":"credits_10","purchaseToken":"probe-not-a-real-purchase"}')
sleep 2
kill "$PID" 2>/dev/null; wait "$PID" 2>/dev/null; PID=""

echo
if grep -q "permissionDenied\|insufficient permissions" "$WORK/verify.log"; then
  echo "RESULT: NOT READY — Google authenticated the key but denied access to this app."
  echo
  echo "  The key itself is fine. The Play Console link is not active yet. Check:"
  echo "   - Play Console > Users and permissions: the service account appears and is not 'Invited'"
  echo "   - It has app access to PurePrep, plus 'View financial data' and 'Manage orders'"
  echo "   - The Google Play Android Developer API is enabled in the SAME Cloud project as the key"
  echo "   - Otherwise: wait. Propagation can take up to 24 hours."
  exit 1
elif grep -qE "invalid_grant|Invalid JWT|unauthorized_client" "$WORK/verify.log"; then
  echo "RESULT: BAD KEY — Google rejected the credential itself."
  echo "  Create a fresh JSON key on the service account and try again."
  exit 1
elif [ "$STATUS" = "400" ]; then
  # Google reached, app authorised, and the fake token rejected on its own merits — with no
  # permission or credential error logged. That is exactly the healthy path.
  echo "RESULT: READY — Google accepted the credential and answered for this app."
  echo "  The probe token was rejected as invalid, which is correct: it is not a real purchase."
  echo "  Safe to deploy."
  exit 0
else
  echo "RESULT: UNCLEAR — redeem returned HTTP $STATUS. Full log below."
  echo
  grep -iE "google|androidpublisher|error|warn" "$WORK/verify.log" | tail -20
  exit 1
fi
