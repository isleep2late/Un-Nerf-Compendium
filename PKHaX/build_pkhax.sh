#!/usr/bin/env bash
# ============================================================
#  Build PKHaX (PKHeX fork) -> PKHaX.exe   [Linux/macOS]
#  PKHeX is a WinForms app, so this cross-builds a WINDOWS .exe
#  (run it on Windows, or via Wine). Needs the .NET 10 SDK; if
#  missing, this installs it locally to ~/.dotnet (no root).
# ============================================================
set -e
cd "$(dirname "$0")"

DOTNET=""
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  DOTNET="dotnet"
elif [ -x "$HOME/.dotnet/dotnet" ] && "$HOME/.dotnet/dotnet" --list-sdks 2>/dev/null | grep -q '^10\.'; then
  DOTNET="$HOME/.dotnet/dotnet"; export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
else
  echo "No .NET 10 SDK found - installing it to ~/.dotnet (a few minutes)..."
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
  DOTNET="$HOME/.dotnet/dotnet"; export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
fi

SRC_COMMIT="$(sed -n 's/^is2l-commit=//p' PKHAX_SOURCE.txt 2>/dev/null | head -n 1)"
SRC_STATE="$(sed -n 's/^state=//p' PKHAX_SOURCE.txt 2>/dev/null | head -n 1)"
if ! printf '%s' "$SRC_COMMIT" | grep -Eq '^[0-9a-f]{40}$'; then
  echo "[X] PKHAX_SOURCE.txt names no is2l commit, so the build cannot be stamped with its source."
  echo "    Run ./build.sh pkhax-export <this clone> from the is2l worktree first."
  exit 1
fi
STAMP="is2l.$SRC_COMMIT"
if [ "$SRC_STATE" != "clean" ]; then
  STAMP="$STAMP.dirty"
fi

echo "=== Building PKHaX with: $DOTNET (Release, win-x64, source $STAMP). Takes a minute or two... ==="
"$DOTNET" publish PKHeX.WinForms -c Release -r win-x64 \
  -p:PublishSingleFile=true --self-contained false -p:EnableWindowsTargeting=true \
  "-p:SourceRevisionId=$STAMP"

OUTDIR="PKHeX.WinForms/bin/Release/net10.0-windows/win-x64/publish"
if [ ! -f "$OUTDIR/PKHeX.exe" ]; then
  echo "[X] Build finished but PKHeX.exe not found in: $OUTDIR"
  exit 1
fi
cp -f "$OUTDIR/PKHeX.exe" "$OUTDIR/PKHaX.exe"
echo
echo "[OK] PKHaX built:"
echo "     $(pwd)/$OUTDIR/PKHaX.exe"
echo
echo "It's a Windows binary (WinForms). Run on Windows (needs the .NET 10 Desktop Runtime),"
echo "or on Linux with Wine:  wine \"$OUTDIR/PKHaX.exe\""
