#!/usr/bin/env python3
r"""
BW2 Pokéstar Bad-Egg PERMANENT FIX — species-aware skip in the per-mon checksum validator.

Root cause (proven by save-state forensics, REVISION 9 in POKESTAR_PROJECT_LOG.md):
A Pokéstar prop is stored as a fully VALID Pokémon (checksum matches its data). During party-load the
game briefly scribbles into the prop's data block; if the per-mon validator at arm9 `0x201DDC8` happens
to run in that window it sees a transient checksum mismatch and sets the sticky `checksumFailed` bit
(bit 2 of mon+4) -> the mon shows as a Bad Egg for the rest of the session. It is a timing race
(melonDS usually wins it, DeSmuME loses it consistently); `checksumFailed` is NOT stored in the save.

Fix: at the validator's flag-set site, read the (decrypted, PID-unshuffled) species. If it is a Pokéstar
prop (652..684) take the validator's "valid" path (return 1, do NOT set checksumFailed). Non-props are
unaffected — genuinely corrupt saves still show Bad Eggs. Implemented as an arm9 code cave; the site is
redirected with a single BL and the cave restores the exact original flag-set for non-props.

Only arm9 is touched; recompressed with the game's normal boot path (ModuleParams.compressed_static_end
updated). Reversible (keep the input ROM). Works for Black 2 and White 2 (validator auto-located by its
prologue signature; offsets are identical in both).

Usage:  python3 bw2_pokestar_validator_fix.py in.nds --out out.nds
"""
import sys, os, struct, argparse
sys.path.insert(0, os.path.dirname(__file__))
import ndspy.rom, ndspy.codeCompression as cc
import thumb1 as T
import build_cave

ARM9_RAM = 0x2004000
NITRO    = struct.pack('<I', 0xDEC00621)
PROP_LO, PROP_HI = 652, 684

# validator prologue (identical Black2/White2): push{r3,r4,r5,lr}; adds r5,r0,#0; ldrh r0,[r5,#4];
#                                               movs r4,#1; lsls r0,r0,#0x1e; lsrs r0,r0,#0x1f
def _sig():
    return (T.h(0xB538) + T.h(0x1C05) + T.h(0x88A8) + T.h(0x2401) + T.h(0x0780) + T.h(0x0FC0))

# expected flag-set block at prologue+0x2A: ldrh r1,[r5,#4]; movs r0,#4; movs r4,#0; orrs r0,r1; strh r0,[r5,#4]
def _flagset():
    return (T.h(0x88A9) + T.h(0x2004) + T.h(0x2400) + T.h(0x4308) + T.h(0x80A8))

def read_arm9(rom):
    raw = bytes(rom.arm9)
    off = raw.find(NITRO)
    if off >= 8 and struct.unpack_from('<I', raw, off-8)[0] == 0:
        return bytearray(raw)
    return bytearray(cc.decompress(raw))

def write_arm9(rom, dec):
    rec = bytearray(cc.compress(bytes(dec), isArm9=True))
    assert cc.decompress(bytes(rec)) == bytes(dec), "arm9 recompress round-trip failed"
    off = rec.find(NITRO)
    if off >= 8:
        struct.pack_into('<I', rec, off-8, ARM9_RAM + len(rec))
    rom.arm9 = bytes(rec)

def find_cave(dec, bss_start, need=0x58):
    """First zero-run >= need below staticBssStart (i.e. in initialized static data, not BSS/autoload)
    whose [base,base+need) is not referenced by any 32-bit word in arm9."""
    n = len(dec)
    words = set(struct.unpack_from('<%dI' % (n // 4), dec, 0))
    i = 0
    while i < n and ARM9_RAM + i < bss_start:
        if dec[i] == 0:
            j = i
            while j < n and dec[j] == 0:
                j += 1
            if j - i >= need + 4:
                base = ARM9_RAM + ((i + 3) & ~3)
                if not any(base <= w < base + need for w in words):
                    return base
            i = j
        else:
            i += 1
    raise RuntimeError("no safe cave found below compressedStaticEnd")

def patch(path_in, path_out):
    rom = ndspy.rom.NintendoDSRom.fromFile(path_in)
    dec = read_arm9(rom)
    prol = dec.find(_sig())
    if prol < 0:
        raise RuntimeError("validator prologue not found")
    flag_site = prol + 0x2A
    ret_addr  = ARM9_RAM + prol + 0x34
    assert bytes(dec[flag_site:flag_site+10]) == _flagset(), \
        "flag-set block mismatch @%x: %s" % (ARM9_RAM+flag_site, dec[flag_site:flag_site+10].hex())
    no = dec.find(NITRO); comp_end = struct.unpack_from('<I', dec, no-8)[0]
    bss_start = struct.unpack_from('<I', dec, no-0x10)[0]   # ModuleParams.staticBssStart / autoloadStart
    cave = find_cave(dec, bss_start)

    # write the cave
    blob = build_cave.build(cave, ret_addr)
    coff = cave - ARM9_RAM
    dec[coff:coff+len(blob)] = blob

    # redirect the flag-set site: BL cave, then NOP-fill the 6 dead bytes (0x201DDF6..0x201DDFB)
    site_ram = ARM9_RAM + flag_site
    dec[flag_site:flag_site+4] = T.bl(site_ram, cave)
    dec[flag_site+4:flag_site+10] = T.nop()*3

    write_arm9(rom, dec)
    rom.saveToFile(path_out)
    return dict(validator=hex(ARM9_RAM+prol), flag_site=hex(site_ram), ret=hex(ret_addr),
                cave=hex(cave), cave_len=len(blob), comp_end=hex(comp_end))

if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('rom'); ap.add_argument('--out', required=True)
    a = ap.parse_args()
    info = patch(a.rom, a.out)
    print("Pokestar validator fix applied:")
    for k, v in info.items():
        print(f"  {k}: {v}")
