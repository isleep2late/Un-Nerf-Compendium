#!/usr/bin/env python3
r"""
Pokemon Platinum -- remove the Battle Frontier Species Clause and Item Clause.

Companion to platinum_nobanlist.py (banned-species list) and platinum_forms.py (form revert).
This lifts the two team-building clauses enforced at Frontier team SELECTION:
  - Species Clause: "Identical Pokemon are not permitted."           (pl_msg file 453, msg 182)
  - Item Clause:    "Some Pokemon are holding identical items."       (pl_msg file 453, msg 183)

How it was found / what it does
--------------------------------
Unlike the banlist (an arm9 data array) and the form revert (a field-script compare), the clauses
are computed in arm9 CODE. The Frontier team-selection validator calls a per-facility "duplicate
checker" that loops the chosen party and returns:
    1 = a duplicate SPECIES was found   -> shows msg 182
    2 = a duplicate ITEM was found      -> shows msg 183
    0 = no duplicates (team accepted)
There are three such checkers (one per facility type 0x11/0x16/0x17; the Battle Tower is 0x11 and is
the only one that checks items as well as species). Each is called from exactly one site.

This patch changes every "return 1" / "return 2" inside those checkers to "return 0" -- a single byte
per site (the Thumb `movs r0,#1`/`#2` immediate -> `#0`). After patching, the checkers can only ever
return 0, so no team is ever rejected for duplicate species or items, at any facility. The edits are
length-preserving (no code moves), so this composes with platinum_nobanlist.py and platinum_forms.py
in any order.

Patched return sites (arm9-relative file offsets; arm9 loads at 0x02000000, uncompressed):
    0x80922  checker@0x020808cc (type 0x11 / Battle Tower) -- duplicate species  (01 -> 00)
    0x80938  checker@0x020808cc (type 0x11 / Battle Tower) -- duplicate item     (02 -> 00)
    0x809b4  checker@0x02080968 (type 0x16)                -- duplicate species  (01 -> 00)
    0x80a28  checker@0x020809dc (type 0x17)                -- duplicate species  (01 -> 00)

  python platinum_clauses.py "Pokemon Platinum.nds"      # -> *_noclause.nds

Use a decrypted Platinum .nds you dumped yourself. Works on a clean ROM or one that already has the
banlist/forms patches applied (arm9 code offsets are identical either way).
"""
import struct, os, sys, shutil, argparse

def u32(d, o): return struct.unpack_from("<I", d, o)[0]

# (arm9-relative offset, original first byte, new first byte). Second byte is 0x20 (movs r0,#imm).
ARM9_EDITS = [
    (0x80922, 0x01, 0x00),  # Tower: return 1 (dup species) -> 0
    (0x80938, 0x02, 0x00),  # Tower: return 2 (dup item)    -> 0
    (0x809B4, 0x01, 0x00),  # facility 0x16: return 1        -> 0
    (0x80A28, 0x01, 0x00),  # facility 0x17: return 1        -> 0
]

def patch(buf):
    a9o = u32(buf, 0x20)  # ARM9 ROM offset (Platinum: 0x4000)
    changed = 0
    for off, oh, nh in ARM9_EDITS:
        o = a9o + off
        cur = buf[o]
        if cur == nh and buf[o + 1] == 0x20:
            continue  # already patched
        if cur != oh or buf[o + 1] != 0x20:
            raise SystemExit(f"[abort] unexpected arm9 bytes at 0x{o:X} "
                             f"({buf[o]:02x} {buf[o+1]:02x}); is this an unmodified Platinum arm9?")
        buf[o] = nh
        changed += 1
    return changed

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("nds")
    ap.add_argument("--out")
    ap.add_argument("--inplace", action="store_true")
    a = ap.parse_args()
    if not os.path.exists(a.nds):
        raise SystemExit("file not found: " + a.nds)
    out = a.nds if a.inplace else (a.out or os.path.splitext(a.nds)[0] + "_noclause.nds")
    if not a.inplace and out != a.nds:
        shutil.copyfile(a.nds, out)
    with open(out, "r+b") as f:
        buf = bytearray(f.read())
        n = patch(buf)
        f.seek(0); f.write(buf)
    print(f"[done] removed {n} Battle Frontier clause check(s) (Species + Item) -> {os.path.basename(out)}")
    print("[note] no team is rejected for duplicate species or held items at any Frontier facility.")

if __name__ == "__main__":
    main()
