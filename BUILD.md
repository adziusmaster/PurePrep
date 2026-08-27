# Building PurePrep locally (Android)

This documents the **verified** local recipe for building the MAUI Android app and
producing a signed release AAB for Google Play. The Web/Server projects build with
a plain `dotnet build`; only the MAUI app needs the extra setup below.

## Prerequisites (maintainer machine)

The maui workload is **not** in the global dotnet SDK. A user-local SDK with the
workload installed lives at `~/dotnet-maui`, plus a separate Android SDK and JDK 17:

```sh
export DOTNET_ROOT=$HOME/dotnet-maui
export PATH=$HOME/dotnet-maui:$PATH
export JAVA_HOME=$HOME/Library/Java/JavaVirtualMachines/jdk-17.0.20+8/Contents/Home
# Android SDK: ~/android-sdk
```

## Gotchas (why the plain build fails)

1. **iOS/Mac Catalyst workloads aren't installed.** The csproj multi-targets
   (`net10.0-android;net10.0-ios;net10.0-maccatalyst`), so a normal restore/build
   aborts with `NETSDK1147: ... workloads must be installed: ios`. Work around it by
   restoring/building **only** the Android TFM (`-f net10.0-android`). Do **not** pass
   `-p:TargetFrameworks=net10.0-android` — that global property leaks into the
   referenced `PurePrep.Core` project and clobbers its `net10.0` assets
   (`NETSDK1005: ... doesn't have a target for 'net10.0'`).
2. **The codeartifact NuGet feed is unreachable.** Always restore from nuget.org:
   `-s https://api.nuget.org/v3/index.json`.
3. **`-s` is a restore-only switch** — restore first, then build with `--no-restore`
   (passing `-s` to `dotnet build` errors with `MSB1001: Unknown switch`).
4. **Release `PublishTrimmed=true` appends the host RID** (`XA0035 osx-arm64`).
   Fix with `-p:UseDefaultPublishRuntimeIdentifier=false`.

## Restore (once, or after dependency changes)

```sh
dotnet restore src/PurePrep.Core/PurePrep.Core.csproj -s https://api.nuget.org/v3/index.json
dotnet restore src/PurePrep/PurePrep.csproj       -s https://api.nuget.org/v3/index.json
```

If the app restore still trips over the iOS workload, restore only the Android TFM
without leaking the property into Core — the simplest reliable way is to temporarily
single-target the app csproj (`<TargetFrameworks>net10.0-android</TargetFrameworks>`),
restore, then revert.

## Debug build (compile check)

```sh
dotnet build src/PurePrep/PurePrep.csproj -c Debug -f net10.0-android --no-restore \
  -p:UseDefaultPublishRuntimeIdentifier=false \
  -p:AndroidSdkDirectory=$HOME/android-sdk -p:JavaSdkDirectory=$JAVA_HOME
```

## Signed release AAB

Signing activates only for Release Android builds when `PUREPREP_KEYSTORE_PASS` is set.
The keystore lives outside the repo (`~/keystores/pureprep-upload.jks`, alias
`pureprep`); no secrets are committed.

```sh
export PUREPREP_KEYSTORE_PASS=$(cat ~/keystores/pureprep-upload.pass.txt)
dotnet build src/PurePrep/PurePrep.csproj -c Release -f net10.0-android --no-restore \
  -p:UseDefaultPublishRuntimeIdentifier=false \
  -p:AndroidPackageFormat=aab \
  -p:AndroidSdkDirectory=$HOME/android-sdk -p:JavaSdkDirectory=$JAVA_HOME
```

Output: `src/PurePrep/bin/Release/net10.0-android/com.adziusmaster.pureprep-Signed.aab`

Verify the signature:

```sh
"$JAVA_HOME/bin/jarsigner" -verify \
  src/PurePrep/bin/Release/net10.0-android/com.adziusmaster.pureprep-Signed.aab
# -> "jar verified." signed by CN=adziusmaster, OU=PurePrep, O=PurePrep, L=Warsaw, C=PL
```

## Before each Play upload

Bump the Android **version code** in `src/PurePrep/PurePrep.csproj`
(`<ApplicationVersion>`), which must be unique and higher than any previously
uploaded build. `<ApplicationDisplayVersion>` is the user-facing version name and
only needs bumping for a real release. ApplicationId: `com.adziusmaster.pureprep`.
