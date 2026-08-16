# Building and installing PKHaX Mobile

There is no Mac in this setup, and you don't need one. Android builds locally on Linux; iOS builds on GitHub's
macOS cloud runner. Both installables end up on your phones over USB.

## Android — build locally on Linux (proven working)

One command builds the APK and installs it to a USB-connected phone:

```bash
# On the phone: Settings > About phone > tap "Build number" 7x, then Developer options > USB debugging ON.
# Plug the phone in, tap "Allow", then:
bash PKHaX-Mobile/build-android-local.sh
```

The script sets up the toolchain env, installs the MAUI Android workload + Android API 36 if missing, builds
the Release APK, and `adb install`s it. Re-run it any time to rebuild after a code / PKHeX.Core change.

Manual equivalent (if you prefer explicit commands): see the top of the script, or:

```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
export ANDROID_HOME="$HOME/Android/Sdk"; export ANDROID_SDK_ROOT="$HOME/Android/Sdk"
export JAVA_HOME="$(dirname "$(dirname "$(readlink -f "$(which java)")")")"
CSPROJ=PKHaX-Mobile/src/PKHaX.Mobile.csproj
dotnet build "$CSPROJ" -c Release -f net10.0-android -p:AndroidPackageFormats=apk
adb -d install -r PKHaX-Mobile/src/bin/Release/net10.0-android/com.unnerf.pkhax-Signed.apk
```

The Release APK is signed with the machine-local Android debug key — fine for personal sideloading, and stable
across rebuilds on this machine (so reinstalls upgrade in place). The csproj only targets `net10.0-ios` when
building on macOS, so the iOS workload (which can't install on Linux) is never needed here.

## iOS — build in the cloud (no Mac)

See **`docs/ios-cloud-build.md`** for the full one-time signing setup and the push-to-build flow. In short:
generate a certificate with `openssl`, create an Ad Hoc provisioning profile on developer.apple.com, store 5
secrets in GitHub, then the `.github/workflows/build-mobile.yml` workflow builds a signed `.ipa` on every push.
Install it over USB from Linux with `ideviceinstaller -i PKHaX.ipa`.

## One push builds both (the "eas build --platform all" flow)

`.github/workflows/build-mobile.yml` builds **both** the Android APK and the iOS IPA on one macOS runner and
uploads them as artifacts. Once it's on `main` and the iOS secrets are set, a push (or Actions → Run workflow)
produces both — download them and install to each phone.

## Updating after a PKHeX sync

The app is a thin shell over `PKHeX.Core`, so a rebuild is the update. After the compendium's `PKHeX.Core` is
synced upstream, just rebuild: re-run `build-android-local.sh` for Android, and push (or re-run the workflow)
for iOS. Whatever features/fixes the fork's core gained come along automatically.
