#!/usr/bin/env python3
r"""
BW2 Pokéstar back-sprite fix.

Pokégra NARC a/0/0/4 stores 20 files per species-form entry (entry index == species id):
  normal set  = files 0..8   ->  f0 = BACK sprite (96x96 NCGR), f2 = FRONT sprite (256x128 animated),
                                  f1/f3 unused, f4..f8 = cell/anim/extra resources
  shiny  set  = files 9..17  ->  f9 = shiny BACK, f11 = shiny FRONT, f13..f17 resources
  files 18,19 = NCLR palettes (normal, shiny)
Files are LZ11-compressed NCGR (magic 0x11 -> "RGCN").

Investigation (clean Black2): every Pokéstar prop (species 652..684) has a valid FRONT sprite (f2).
The normal BACK (f0) is valid for all props EXCEPT 659 "Monster" (blank). Shiny BACK (f9) is blank
for most props. 683 "Black Belt" and 684 "Smeargle 2" have all four slots valid (Smeargle 2 already
has a real back sprite -> left untouched, matching the user's Smeargle instruction).

Fix: for each prop, if a BACK slot (normal f0 / shiny f9) is blank/error, overwrite it with a copy of
that species' FRONT sprite file (f2 / f11) so the front sprite renders & animates from the player's
side. Non-blank backs are left as-is (they already render). Use --force to copy front->back for ALL
prop back slots regardless (in case the in-game black box turns out to be engine-side, not data-side).

Only touches a/0/0/4; length-preserving per file is NOT required (NARC is rebuilt with ndspy).
Reversible: keep the input ROM.
"""
import sys, argparse, ndspy.rom, ndspy.narc
from collections import Counter

PROP_MIN, PROP_MAX = 652, 684          # inclusive; 684 = Pokéstar Smeargle (real Smeargle stats)
STRIDE = 20
# slot roles within the 20-file entry
N_BACK, N_FRONT = 0, 2                  # normal back, normal front
S_BACK, S_FRONT = 9, 11                 # shiny  back, shiny  front

def _lz11(d):
    d = bytes(d)
    if not d or d[0] != 0x11:
        return d
    size = d[1] | (d[2] << 8) | (d[3] << 16)
    out = bytearray(); i = 4
    while len(out) < size:
        fl = d[i]; i += 1
        for b in range(8):
            if len(out) >= size:
                break
            if fl & (0x80 >> b):
                b0 = d[i]; b1 = d[i+1]; i += 2; t = b0 >> 4
                if t == 0:
                    cnt = (b0 << 4 | b1 >> 4) + 0x11; disp = ((b1 & 0xF) << 8 | d[i]) + 1; i += 1
                elif t == 1:
                    cnt = ((b0 & 0xF) << 12 | b1 << 4 | d[i] >> 4) + 0x111; disp = ((d[i] & 0xF) << 8 | d[i+1]) + 1; i += 2
                else:
                    cnt = t + 1; disp = ((b0 & 0xF) << 8 | b1) + 1
                for _ in range(cnt):
                    out.append(out[-disp])
            else:
                out.append(d[i]); i += 1
    return bytes(out)

def _is_blank(fdata, distinct_thresh=4, nz_thresh=1):
    """A sprite file is 'blank/error' if its decoded NCGR pixel data is near-uniform (a black box).
    Threshold tuned so genuinely-simple-but-real sprites (e.g. F-00's few-colour back) are kept."""
    try:
        if len(bytes(fdata)) < 120:      # the tiny placeholder files (~50-75 B) are always black boxes
            return True
        dec = _lz11(fdata)
        p = dec.find(b'RAHC')
        if p < 0:
            return True
        px = dec[p+0x20:]
        if not px:
            return True
        c = Counter(px)
        distinct = len(c)
        nz = 100 * sum(v for k, v in c.items() if k) // max(1, len(px))
        return distinct <= distinct_thresh or nz <= nz_thresh
    except Exception:
        return True   # undecodable == treat as broken

def fix(inpath, outpath, force=False, verbose=True):
    rom = ndspy.rom.NintendoDSRom.fromFile(inpath)
    fid = rom.filenames.idOf('a/0/0/4')
    narc = ndspy.narc.NARC(rom.files[fid])
    files = [bytearray(f) for f in narc.files]
    changed = []
    for sp in range(PROP_MIN, PROP_MAX + 1):
        base = sp * STRIDE
        if base + STRIDE > len(files):
            continue
        # source of the FRONT sprite: normal front (f2) is valid for every prop; the shiny front
        # (f11) is itself blank for most, so shiny backs are sourced from the NORMAL front too.
        norm_front = base + N_FRONT
        if _is_blank(files[norm_front]):
            continue                                       # no valid front to copy (shouldn't happen)
        for back, label in ((N_BACK, 'normal'), (S_BACK, 'shiny')):
            bi = base + back
            back_blank = _is_blank(files[bi])
            if force or back_blank:
                if bytes(files[bi]) != bytes(files[norm_front]):
                    files[bi] = bytearray(files[norm_front])   # whole-file copy (carries NCGR dims)
                    changed.append((sp, label, 'was_blank' if back_blank else 'forced'))
    narc.files = [bytes(f) for f in files]
    rom.files[fid] = narc.save()
    rom.saveToFile(outpath)
    if verbose:
        print("[sprites] copied FRONT->BACK for %d slot(s):" % len(changed))
        for sp, lab, why in changed:
            print("   species %d %-6s (%s)" % (sp, lab, why))
        print("[sprites] wrote", outpath)
    return changed

if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('rom')
    ap.add_argument('--out')
    ap.add_argument('--force', action='store_true', help='copy front->back for ALL prop back slots')
    a = ap.parse_args()
    out = a.out or a.rom.replace('.nds', '_sprites.nds')
    fix(a.rom, out, force=a.force)
