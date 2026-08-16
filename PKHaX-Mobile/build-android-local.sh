#!/usr/bin/env bash
# Build the PKHaX Android APK and install it on a USB-connected phone.
# Proven working on this box 2026-08-15 (dotnet 10.0.302, Android SDK android-36, OpenJDK 21).
#
#   1. Enable Developer Options + USB debugging on the phone, plug it in, tap "Allow".
#   2. Run:  bash PKHaX-Mobile/build-android-local.sh
#
# Re-run it any time to rebuild + reinstall after a code or PKHeX.Core change.
set -euo pipefail

# --- toolchain env (dotnet is installed under ~/.dotnet and not on PATH by default) ---
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
export ANDROID_HOME="$HOME/Android/Sdk"
export ANDROID_SDK_ROOT="$HOME/Android/Sdk"
export JAVA_HOME="$(dirname "$(dirname "$(readlink -f "$(command -v java)")")")"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ="$SCRIPT_DIR/src/PKHaX.Mobile.csproj"
ADB="$ANDROID_HOME/platform-tools/adb"

echo ">> dotnet $(dotnet --version), java $(java -version 2>&1 | head -1)"

# --- one-time: MAUI Android workload ---
if ! dotnet workload list 2>/dev/null | grep -qi 'maui-android\|maui'; then
	echo ">> installing maui-android workload (one-time, a few minutes)..."
	dotnet workload install maui-android
fi

# --- one-time: make sure the Android API level the SDK targets is installed + licenses accepted ---
echo ">> ensuring Android SDK dependencies (API level + licenses)..."
dotnet build "$CSPROJ" -t:InstallAndroidDependencies -f net10.0-android \
	-p:AndroidSdkDirectory="$ANDROID_HOME" -p:AcceptAndroidSDKLicenses=True >/dev/null

# --- build the release APK (debug-key signed -> fine for personal sideloading) ---
echo ">> building release APK..."
dotnet build "$CSPROJ" -c Release -f net10.0-android \
	-p:AndroidPackageFormats=apk -p:AndroidSdkDirectory="$ANDROID_HOME"

APK="$SCRIPT_DIR/src/bin/Release/net10.0-android/com.unnerf.pkhax-Signed.apk"
echo ">> built: $APK"

# --- install to the connected phone ---
mapfile -t DEVICES < <("$ADB" devices | awk 'NR>1 && $2=="device"{print $1}')
if [ "${#DEVICES[@]}" -eq 0 ]; then
	echo ">> No device detected by adb. Plug the phone in (USB debugging on), then run:"
	echo "     $ADB install -r \"$APK\""
elif [ "${#DEVICES[@]}" -gt 1 ]; then
	echo ">> Multiple devices attached: ${DEVICES[*]}"
	echo "   Install to your phone with (replace <serial>):"
	echo "     $ADB -s <serial> install -r \"$APK\""
else
	SER="${DEVICES[0]}"
	# Samsung blocks adb installs unless "Install via USB" is on; unset, the install hangs forever with
	# no error and no on-device prompt. Enabling it is harmless on non-Samsung devices.
	if [ "$("$ADB" -s "$SER" shell settings get global install_via_usb 2>/dev/null | tr -d '\r')" != "1" ]; then
		"$ADB" -s "$SER" shell settings put global install_via_usb 1 >/dev/null 2>&1 &&
			echo ">> enabled 'Install via USB' on $SER (Samsung blocks installs without it)"
	fi
	echo ">> installing to $SER ..."
	"$ADB" -s "$SER" install -r "$APK" || {
		echo ">> signature mismatch? removing the old copy and retrying..."
		"$ADB" -s "$SER" uninstall com.unnerf.pkhax || true
		"$ADB" -s "$SER" install "$APK"
	}
	echo ">> done. Open 'PKHaX' on the phone."
fi
