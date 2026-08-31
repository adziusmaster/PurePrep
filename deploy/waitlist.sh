#!/usr/bin/env bash
#
# waitlist.sh — list the people who signed up to be emailed when PurePrep opens to everyone.
#
# These are the addresses collected by the landing-page form, where the user ticked:
#   "Email me once when PurePrep opens to everyone. One message about availability —
#    no marketing, no sharing your address."
# The server only stores a signup when that consent box is ticked, so every row here is a
# consented open-testing / launch notification opt-in.
#
# Requirements:
#   - curl and python3 on PATH.
#   - ADMIN_SECRET must match the server's Admin__Secret (set in deploy/.env as ADMIN_SECRET).
#
# Usage:
#   export ADMIN_SECRET=your-secret
#   export PUREPREP_API=https://api.pureprep.lechdigital.nl   # optional, this is the default
#
#   ./deploy/waitlist.sh                 # pretty table of everyone who registered (default)
#   ./deploy/waitlist.sh list            # same as above
#   ./deploy/waitlist.sh emails          # just the email addresses, one per line
#   ./deploy/waitlist.sh bcc             # all addresses on one comma-separated line (for BCC)
#   ./deploy/waitlist.sh json            # raw JSON response, pretty-printed
set -euo pipefail

# Auto-load deploy/.env (git-ignored) so ADMIN_SECRET etc. are available without exporting.
_SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -f "$_SCRIPT_DIR/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  . "$_SCRIPT_DIR/.env"
  set +a
fi

# The admin secret. Either export ADMIN_SECRET=... before running, or paste it here.
# It must match the server's Admin__Secret (deploy/.env ADMIN_SECRET).
ADMIN_SECRET="${ADMIN_SECRET:-}"

API="${PUREPREP_API:-https://api.pureprep.lechdigital.nl}"
API="${API%/}"

die() { echo "error: $*" >&2; exit 1; }

[ -n "${ADMIN_SECRET:-}" ] || die "ADMIN_SECRET is not set. export ADMIN_SECRET=... (must match the server's Admin__Secret)."
command -v curl >/dev/null || die "curl is required."
command -v python3 >/dev/null || die "python3 is required."

# fetch — GET the admin waitlist as a JSON array (newest first).
fetch() {
  curl -fsS "$API/api/admin/waitlist" -H "X-Admin-Secret: $ADMIN_SECRET"
}

cmd="${1:-list}"

case "$cmd" in
  list)
    WAITLIST_JSON="$(fetch)" python3 -c '
import json, os, sys
rows = json.loads(os.environ["WAITLIST_JSON"])
if not rows:
    print("No signups yet.")
    sys.exit(0)

def day(v):
    return (v or "")[:10]  # ISO date part only

width = max([len(r.get("email", "")) for r in rows] + [len("EMAIL")])
fmt = "%3s  %-*s  %-10s  %-10s  %s"

print("%d signup(s) for the open-testing / launch email:\n" % len(rows))
print(fmt % ("#", width, "EMAIL", "SIGNED UP", "CONSENTED", "SOURCE"))
print(fmt % ("-"*3, width, "-"*width, "-"*10, "-"*10, "-"*6))
for i, r in enumerate(rows, 1):
    print(fmt % (str(i), width, r.get("email", ""),
                 day(r.get("createdAt")), day(r.get("consentedAt")),
                 r.get("source", "") or "-"))
'
    ;;

  emails)
    fetch | python3 -c "import json,sys; [print(r['email']) for r in json.load(sys.stdin) if r.get('email')]"
    ;;

  bcc)
    fetch | python3 -c "import json,sys; print(', '.join(r['email'] for r in json.load(sys.stdin) if r.get('email')))"
    ;;

  json)
    fetch | python3 -m json.tool
    ;;

  -h|--help|help)
    cat <<'USAGE'
waitlist.sh — list the people who asked to be emailed when PurePrep opens to everyone.

Commands:
  ./deploy/waitlist.sh            pretty table of everyone who registered (default)
  ./deploy/waitlist.sh list       same as above
  ./deploy/waitlist.sh emails     just the email addresses, one per line
  ./deploy/waitlist.sh bcc        all addresses on one comma-separated line (for BCC)
  ./deploy/waitlist.sh json       raw JSON response, pretty-printed

ADMIN_SECRET must match the server's Admin__Secret (deploy/.env ADMIN_SECRET).
Override the backend with PUREPREP_API (defaults to https://api.pureprep.lechdigital.nl).
USAGE
    ;;

  *)
    die "unknown command '$cmd' (try: list, emails, bcc, json, help)"
    ;;
esac
