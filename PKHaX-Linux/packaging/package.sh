#!/usr/bin/env bash
set -euo pipefail

usage() {
	echo "usage: package.sh <publish folder> <output .tar.gz>" >&2
	echo "  <publish folder> holds the self-contained single-file PKHaX from dotnet publish -r linux-x64" >&2
	echo "  the tarball holds PKHaX-linux-x64/ with PKHaX pkhax.desktop icon.png LICENSE THIRD_PARTY_NOTICES.md README.txt" >&2
	exit 2
}

[ "$#" -eq 2 ] || usage
publish="$1"
out="$2"
case "$out" in
	*.tar.gz) ;;
	*) echo "package.sh: the output must end in .tar.gz, not '$out'" >&2; exit 2 ;;
esac

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
headdir="$(cd "$here/.." && pwd)"
root="PKHaX-linux-x64"

[ -d "$publish" ] || { echo "package.sh: the publish folder '$publish' does not exist" >&2; exit 2; }
binary="$publish/PKHaX"
[ -f "$binary" ] && [ ! -L "$binary" ] || { echo "package.sh: $binary is missing or is a link; publish with -p:PublishSingleFile=true first" >&2; exit 1; }
[ "$(head -c 4 "$binary" | od -An -tx1 | tr -d ' \n')" = "7f454c46" ] || { echo "package.sh: $binary is not an ELF binary" >&2; exit 1; }

[ -z "${PKHAX_LICENSE:-}" ] || [ -f "$PKHAX_LICENSE" ] || { echo "package.sh: PKHAX_LICENSE=$PKHAX_LICENSE is not a file" >&2; exit 1; }
license=""
for candidate in "${PKHAX_LICENSE:-}" "$headdir/LICENSE" "$headdir/../PKHaX/LICENSE"; do
	if [ -n "$candidate" ] && [ -f "$candidate" ] && [ ! -L "$candidate" ]; then
		license="$candidate"
		break
	fi
done
[ -n "$license" ] || { echo "package.sh: no LICENSE found: looked for \$PKHAX_LICENSE, $headdir/LICENSE and $headdir/../PKHaX/LICENSE (the PKHeX GPL-3.0 text)" >&2; exit 1; }

for need in "$here/pkhax.desktop" "$here/icon.png" "$here/README.txt" "$headdir/THIRD_PARTY_NOTICES.md"; do
	[ -f "$need" ] && [ ! -L "$need" ] || { echo "package.sh: $need is missing or is a link" >&2; exit 1; }
done

tmp=""
stage="$(mktemp -d "${TMPDIR:-/tmp}/pkhax-package.XXXXXX")"
trap 'rm -rf "$stage"; [ -z "$tmp" ] || rm -f "$tmp"' EXIT
mkdir -p "$stage/$root"
install -m 0755 "$binary" "$stage/$root/PKHaX"
install -m 0644 "$here/pkhax.desktop" "$stage/$root/pkhax.desktop"
install -m 0644 "$here/icon.png" "$stage/$root/icon.png"
install -m 0644 "$here/README.txt" "$stage/$root/README.txt"
install -m 0644 "$headdir/THIRD_PARTY_NOTICES.md" "$stage/$root/THIRD_PARTY_NOTICES.md"
install -m 0644 "$license" "$stage/$root/LICENSE"

mkdir -p "$(dirname "$out")"
tmp="$out.partial.$$"
rm -f "$tmp"
tar --sort=name --owner=0 --group=0 --numeric-owner --format=gnu -C "$stage" -czf "$tmp" "$root"
listed="$(tar -tzf "$tmp" | sort | tr '\n' ' ')"
for f in PKHaX pkhax.desktop icon.png LICENSE THIRD_PARTY_NOTICES.md README.txt; do
	case " $listed " in
		*" $root/$f "*) ;;
		*) rm -f "$tmp"; echo "package.sh: the tarball lacks $root/$f" >&2; exit 1 ;;
	esac
done
mv -f "$tmp" "$out"
tmp=""
echo "package.sh: $out ($(stat -c %s "$out") bytes, sha256 $(sha256sum "$out" | cut -d' ' -f1))"
