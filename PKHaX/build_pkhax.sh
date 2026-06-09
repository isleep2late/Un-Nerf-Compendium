#!/usr/bin/env bash
# ============================================================
#  Build PKHaX (PKHeX fork) -> PKHaX.exe   [Linux/macOS]
#  PKHeX is a Windows WinForms app, so this cross-builds a
#  Windows .exe (run it on Windows, or via Wine on Linux).
#  Needs the .NET 10 SDK installed.
# ============================================================
set -e
cd "$(dirname "$0")"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "[X] .NET SDK not found on PATH."
  echo "    Install the .NET 10 SDK:"
  echo "      curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0"
  echo "      export PATH=\"\$HOME/.dotnet:\$PATH\""
  echo "    (or your distro's dotnet-sdk-10.0 package), then run this again."
  exit 1
fi

echo "=== Building PKHaX (Release, win-x64, Windows cross-target). Takes a minute or two... ==="
dotnet publish PKHeX.WinForms -c Release -r win-x64 \
  -p:PublishSingleFile=true --self-contained false -p:EnableWindowsTargeting=true

OUTDIR="PKHeX.WinForms/bin/Release/net10.0-windows/win-x64/publish"
if [ ! -f "$OUTDIR/PKHeX.exe" ]; then
  echo "[X] Build finished but PKHeX.exe was not found in: $OUTDIR"
  exit 1
fi

cp -f "$OUTDIR/PKHeX.exe" "$OUTDIR/PKHaX.exe"
echo
echo "[OK] PKHaX built:"
echo "     $(pwd)/$OUTDIR/PKHaX.exe"
echo
echo "It's a Windows binary: copy to Windows (needs the .NET 10 Desktop Runtime),"
echo "or run on Linux with: wine \"$OUTDIR/PKHaX.exe\""
