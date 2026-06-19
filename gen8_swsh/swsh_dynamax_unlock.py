#!/usr/bin/env python3
"""
swsh_dynamax_unlock.py  —  let restricted species Dynamax in Pokémon Sword/Shield.

Clears the per-species `CanNotDynamax` flag in the personal table so Zacian,
Zamazenta (and optionally Eternatus, or every species) can Dynamax. This is the
exact flag pkNX's PersonalInfo8SWSH exposes:

    PersonalInfo8SWSH.SIZE       = 0xB0           # one entry per species/form
    RegionalFlags                = u16 @ 0x5A
    CanNotDynamax                = (Data[0x5A] >> 2) & 1     # bit 2 (value 4)
    FormStatsIndex               = u16 @ 0x1E     # where this species' alt forms begin
    FormCount                    = u8  @ 0x20     # number of forms incl. base

For a species we clear the flag on its BASE entry AND on every alternate-form
entry (so Crowned Zacian/Zamazenta — which are separate appended entries — also
become Dynamax-capable, not just the Hero forme).

INPUT  : your game's  bin/pml/personal/personal_data_total.perbin
         (Eden: right-click the game -> "Dump RomFS", then grab that file. Use
          YOUR v1.3.1 dump so DLC entries stay intact.)
OUTPUT : a patched .perbin to drop into a LayeredFS RomFS mod at
         load/<TitleID>/<ModName>/romfs/bin/pml/personal/personal_data_total.perbin
         Sword TitleID = 0100ABF008968000 , Shield = 01008DB008C2C000

USAGE  :
  python swsh_dynamax_unlock.py personal_data_total.perbin                 # Zacian + Zamazenta (default)
  python swsh_dynamax_unlock.py personal_data_total.perbin --eternatus     # + Eternatus
  python swsh_dynamax_unlock.py personal_data_total.perbin --species 888 889 890 150
  python swsh_dynamax_unlock.py personal_data_total.perbin --all           # every species/form
  python swsh_dynamax_unlock.py personal_data_total.perbin --verify        # report only, no write
  python swsh_dynamax_unlock.py in.perbin -o out.perbin
"""
import sys, os, argparse, struct

ENTRY        = 0xB0
FLAG_OFF     = 0x5A
CANNOTDYNA   = 0x04        # bit 2 of byte 0x5A
FORMIDX_OFF  = 0x1E        # u16
FORMCNT_OFF  = 0x20        # u8

# National dex = personal base index for these species
NAMED = {888: "Zacian", 889: "Zamazenta", 890: "Eternatus", 150: "Mewtwo"}

def u16(d, o): return struct.unpack_from("<H", d, o)[0]

def entries_for_species(data, n, species):
    """base index + all alternate-form indices for a species (via FormStatsIndex/FormCount)."""
    if species >= n:
        return []
    base = species * ENTRY
    idxs = [species]
    fcount = data[base + FORMCNT_OFF]
    fstart = u16(data, base + FORMIDX_OFF)
    if fcount > 1 and fstart != 0:
        for k in range(fcount - 1):
            fi = fstart + k
            if fi < n:
                idxs.append(fi)
    return idxs

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input")
    ap.add_argument("-o", "--output", default=None)
    ap.add_argument("--all", action="store_true", help="clear on EVERY entry")
    ap.add_argument("--eternatus", action="store_true", help="also include Eternatus (890)")
    ap.add_argument("--species", type=int, nargs="+", default=None,
                    help="explicit species (national dex) list; overrides defaults")
    ap.add_argument("--verify", action="store_true", help="report only; do not write")
    a = ap.parse_args()

    data = bytearray(open(a.input, "rb").read())
    if len(data) % ENTRY:
        print(f"WARNING: size {len(data)} not a multiple of 0x{ENTRY:X}; is this a SwSh personal_data_total.perbin?")
    n = len(data) // ENTRY
    print(f"entries: {n}  (0x{ENTRY:X} bytes each)")

    if a.all:
        targets = list(range(n)); mode = "ALL entries"
    else:
        base_species = a.species if a.species else [888, 889] + ([890] if a.eternatus else [])
        targets = []
        for sp in base_species:
            e = entries_for_species(data, n, sp)
            nm = NAMED.get(sp, f"species {sp}")
            print(f"  {nm} (dex {sp}): entries -> {e}")
            targets += e
        mode = "species: " + ", ".join(NAMED.get(s, str(s)) for s in base_species)

    changed = 0
    for i in sorted(set(targets)):
        off = i * ENTRY + FLAG_OFF
        if data[off] & CANNOTDYNA:
            if not a.verify:
                data[off] &= ~CANNOTDYNA
            changed += 1

    print(f"\n{'WOULD clear' if a.verify else 'cleared'} CanNotDynamax on {changed} entr"
          f"{'y' if changed == 1 else 'ies'}  ({mode})")

    if a.verify:
        print("verify mode: no file written."); return
    out = a.output or (os.path.splitext(a.input)[0] + "_DynamaxUnlocked.perbin")
    open(out, "wb").write(bytes(data))
    print(f"\nwrote: {out}")
    print("Install: load/<TitleID>/DynamaxUnlock/romfs/bin/pml/personal/personal_data_total.perbin")
    print("then enable the add-on in Eden (Properties -> Add-Ons).")

if __name__ == "__main__":
    main()
