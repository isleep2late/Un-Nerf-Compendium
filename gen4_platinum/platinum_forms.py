#!/usr/bin/env python3
r"""
Pokemon Platinum -- remove the Battle Frontier FORM restrictions.

Companion to platinum_nobanlist.py. The species banlist lives in arm9; the *form*
restriction (which makes Origin Giratina, the Rotom appliance forms and Sky Shaymin
revert to their base form when entered into a Frontier facility) lives in the field
SCRIPTS, not in a banlist byte. Credit for finding it: SmolJoltik, Project Pokemon
topic 67882 ("[DSPRE] Removing Form Restrictions In Pokemon Platinum's Battle Frontier").

SmolJoltik located, in DSPRE, a small per-Pokemon "form check" run that the facility
scripts execute for each chosen party member:

    CMD_477 53 <slot> 32780      (Battle Tower only; reads the slot's form sentinel)
    CMD_798 <src> 32780          (copies the sentinel into var 32780 / 0x800C)
    CompareVarValue 32780 255    (banned form -> sentinel == 255)
    JumpIf EQUAL Function#<n>    (jump to the "revert this mon" handler)

He removed every instance of this run in four script files and the forms stopped
reverting. Deleting bytecode in DSPRE is safe because DSPRE recompiles jump targets;
doing it as a raw byte edit would shift every following offset. So instead of deleting,
this script performs the equivalent control-flow change WITHOUT moving a single byte:
it rewrites the compare constant from 255 (0x00FF) to 0xFFFF. The form sentinel is a
byte (0..255), so it can never equal 0xFFFF -- the EQUAL jump to the revert handler is
therefore never taken, exactly as if the run had been deleted. Every file offset, every
jump target and the NARC/ROM layout stay byte-identical, so this composes cleanly with
platinum_nobanlist.py and ships as a tiny, deterministic patch.

The run appears the exact number of times SmolJoltik documented, per facility:
    script 367 Battle Tower  : 5  (Fn90 singles/doubles x3 + Fn114 multi x2)
    script 377 Battle Hall   : 2
    script 378 Battle Castle : 3
    script 379 Battle Arcade : 3                                  total = 13
(The Battle Factory rents Pokemon, so it has no such check.)

The same 9-byte signature also occurs elsewhere in scr_seq.narc (other facility logic),
so this script does NOT blind-replace ROM-wide: it locates each of the four script files
inside the ROM and only edits hits that fall inside them.

  python platinum_forms.py "Pokemon Platinum.nds"      # -> *_noforms.nds

Use a decrypted Platinum .nds you dumped yourself. Works on a clean ROM or on one that
already has platinum_nobanlist.py applied.
"""
import os, sys, re, argparse, shutil
try:
    import ndspy.rom, ndspy.narc
except ImportError:
    raise SystemExit("This patch needs ndspy:  pip install ndspy --break-system-packages")

SCR_SEQ_FILEID = 337                      # fielddata/script/scr_seq.narc in Platinum (CPUE)
TARGETS = {367: 5, 377: 2, 378: 3, 379: 3}   # script file -> expected form-check count
SIG = bytes.fromhex("11000c80ff001c0001")    # CompareVarValue 0x800C,0x00FF ; JumpIf EQUAL
VALUE_HI = 5                               # index within SIG of the 0x00 high byte of "255"

def find_targets(path):
    """Return sorted list of absolute ROM offsets of the 13 form-checks to patch."""
    rom = ndspy.rom.NintendoDSRom.fromFile(path)
    if bytes(rom.idCode) != b"CPUE":
        print(f"[warn] idCode {bytes(rom.idCode)!r} is not CPUE (Platinum USA); proceeding anyway")
    raw = open(path, "rb").read()
    narc = ndspy.narc.NARC(rom.files[SCR_SEQ_FILEID])
    abs_offsets = []
    for idx, expected in sorted(TARGETS.items()):
        d = narc.files[idx]
        base = raw.find(d)
        if base < 0 or raw.find(d, base + 1) != -1:
            raise SystemExit(f"[abort] script file {idx} not uniquely located in ROM")
        hits = [m.start() for m in re.finditer(re.escape(SIG), d)]
        if len(hits) != expected:
            raise SystemExit(f"[abort] script {idx}: found {len(hits)} form-checks, expected "
                             f"{expected}. Is this Platinum (CPUE)?")
        abs_offsets += [base + h for h in hits]
    return sorted(abs_offsets)

def patch(buf, offsets):
    changed = 0
    for o in offsets:
        if bytes(buf[o:o+len(SIG)]) != SIG:
            # already patched? high byte flipped to FF
            chk = bytearray(SIG); chk[VALUE_HI] = 0xFF
            if bytes(buf[o:o+len(SIG)]) == bytes(chk):
                continue
            raise SystemExit(f"[abort] unexpected bytes at 0x{o:X}")
        buf[o+VALUE_HI] = 0xFF      # 255 (0x00FF) -> 0xFFFF ; EQUAL jump can never fire
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
    offsets = find_targets(a.nds)
    out = a.nds if a.inplace else (a.out or os.path.splitext(a.nds)[0] + "_noforms.nds")
    if not a.inplace and out != a.nds:
        shutil.copyfile(a.nds, out)
    with open(out, "r+b") as f:
        buf = bytearray(f.read())
        n = patch(buf, offsets)
        f.seek(0); f.write(buf)
    print(f"[done] neutralized {n} Battle Frontier form-checks (Tower/Hall/Castle/Arcade) "
          f"-> {os.path.basename(out)}")
    print("[note] Origin Giratina/Rotom forms/Sky Shaymin no longer revert in the Frontier. "
          "Credit: SmolJoltik (PP topic 67882).")

if __name__ == "__main__":
    main()
