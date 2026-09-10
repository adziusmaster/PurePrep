#!/usr/bin/env bash
#
# Builds a native-debug-symbols.zip for Google Play from a signed PurePrep .aab.
#
# Why: the .aab contains native code (the .NET/Mono runtime .so libraries plus PurePrep's own
# libxamarin-app.so). Play shows a "you've not uploaded debug symbols" warning until a native symbols
# file is provided. This extracts the shipped, unstripped .so files (they keep their symbol tables)
# and repackages them in the ABI-folder layout Play expects:
#
#     arm64-v8a/<lib>.so
#     x86_64/<lib>.so
#     ...
#
# Upload the resulting zip once per release in Play Console:
#   Release > App bundle explorer > (pick the version) > Downloads > "Native debug symbols" > Upload.
#
# Usage:
#   store/make-native-symbols.sh [path/to/App-Signed.aab] [output.zip]
#
# With no arguments it defaults to the standard Release AAB path and writes the zip beside it.

set -euo pipefail

AAB="${1:-src/PurePrep/bin/Release/net10.0-android/com.adziusmaster.pureprep-Signed.aab}"
OUT="${2:-$(dirname "$AAB")/native-debug-symbols.zip}"

if [[ ! -f "$AAB" ]]; then
  echo "error: AAB not found: $AAB" >&2
  echo "Build a Release AAB first (see BUILD.md)." >&2
  exit 1
fi

# Resolve to an absolute path: the zip step runs from a temp dir, so a relative OUT would be wrong.
OUT_DIR="$(cd "$(dirname "$OUT")" && pwd)"
OUT="$OUT_DIR/$(basename "$OUT")"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# The .aab stores native libs under base/lib/<abi>/*.so — extract just those.
unzip -q "$AAB" 'base/lib/*/*.so' -d "$WORK"

if [[ ! -d "$WORK/base/lib" ]]; then
  echo "error: no native libraries found inside $AAB" >&2
  exit 1
fi

# Flatten base/lib/<abi>/ -> <abi>/ so the zip root holds the ABI folders Play expects.
STAGE="$WORK/symbols"
mkdir -p "$STAGE"
cp -R "$WORK/base/lib/." "$STAGE/"

rm -f "$OUT"
( cd "$STAGE" && zip -qr -X "$OUT" . )

echo "Wrote $OUT"
echo "Contents:"
unzip -l "$OUT" | awk 'NR>3 && $4 != "" {print "  " $4}' | head -8
echo "  ... ($(unzip -l "$OUT" | grep -c '\.so$') .so files total)"
