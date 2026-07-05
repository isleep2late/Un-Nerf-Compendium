#!/usr/bin/env python3
r"""
MASTER build pipeline — BW2 "Un-Nerf Compendium + Pokéstar" patch.

Takes a CLEAN Black 2 / White 2 .nds and applies, in order:
  1. Un-Nerf Compendium   — ban-list / Species+Item clause removal (regulation NARC a/1/0/6)      [bw2_nobanlist.py]
  2. Un-Nerf Compendium   — Arceus form-driven typing (personal NARC rebuild)                     [bw2_arceus_typefix.py]
  3. Pokéstar Bad-Egg fix — arm9 validator species-aware skip for props 652..684 (REVISION 9)     [bw2_pokestar_validator_fix.py]
  4. Pokéstar back sprites — copy front sprite into any blank back-sprite slot                     [bw2_pokestar_sprites.py]

=> writes the final patched ROM.

Why step 3 changed (REVISION 9): the Bad Egg is NOT a species gate. A prop is stored valid; the game
transiently desyncs its checksum during party-load and the per-mon validator (arm9 0x201DDC8) sets the
sticky `checksumFailed` bit -> Bad Egg (a timing race DeSmuME loses, melonDS usually wins). The fix makes
that validator read the decrypted species and skip the flag for props. Save-state forensics + Unicorn
emulation confirm it (see POKESTAR_PROJECT_LOG.md). The old 649->684 "IsValidSpecies" gate-flips were the
wrong location and are no longer used.

Box icons intentionally remain the default "?" placeholder (per user request).

Usage:  python3 bw2_pokestar_build.py "Black 2.nds" --out Black2_UNNERF_POKESTAR.nds
"""
import sys, os, argparse, importlib.util, tempfile, shutil, struct

TOOLS = os.path.dirname(os.path.abspath(__file__))

def _find_compendium(start):
    """Walk up from this tools/ folder to the Un-Nerf Compendium root (the dir that holds both
    gen5_bw2/ and gen45_nds_arceus_typefix/). Works whether tools/ lives in gen5_bw2/ or elsewhere."""
    d = start
    for _ in range(6):
        if os.path.isdir(os.path.join(d, "gen5_bw2")) and os.path.isdir(os.path.join(d, "gen45_nds_arceus_typefix")):
            return d
        d = os.path.dirname(d)
    return os.path.normpath(os.path.join(start, "..", ".."))  # fallback: gen5_bw2/tools -> root

COMPENDIUM = _find_compendium(TOOLS)
sys.path.insert(0, TOOLS)

def _load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec)
    src = open(spec.origin).read().split("if __name__")[0]     # skip the __main__ block
    exec(compile(src, spec.origin, "exec"), m.__dict__)
    return m

def build(inpath, outpath):
    import ndspy.rom
    work = tempfile.mkdtemp()
    try:
        cur = os.path.join(work, "rom.nds")
        shutil.copyfile(inpath, cur)

        # 1) nobanlist (in place)
        nb = _load(os.path.join(COMPENDIUM, "gen5_bw2", "bw2_nobanlist.py"), "nb")
        with open(cur, "r+b") as f:
            buf = bytearray(f.read()); n, _ = nb.patch(buf); f.seek(0); f.write(buf)
        print(f"[1/4] nobanlist: {n} regulation files unbanned")

        # 2) arceus typefix (rebuilds personal NARC -> new file)
        atf = _load(os.path.join(COMPENDIUM, "gen45_nds_arceus_typefix", "bw2_arceus_typefix.py"), "atf")
        step2 = os.path.join(work, "rom2.nds")
        atf.build(cur, step2)
        cur = step2
        print("[2/4] arceus typefix: 16 typed form entries appended")

        # 3) Pokéstar Bad-Egg fix — arm9 validator species-aware skip
        vf = _load(os.path.join(TOOLS, "bw2_pokestar_validator_fix.py"), "vf")
        step3 = os.path.join(work, "rom3.nds")
        info = vf.patch(cur, step3)
        cur = step3
        print(f"[3/4] validator Bad-Egg fix: cave @ {info['cave']} (validator {info['validator']})")

        # 4) back sprites (final output)
        sp = _load(os.path.join(TOOLS, "bw2_pokestar_sprites.py"), "sp")
        sp.fix(cur, outpath, force=False, verbose=False)
        print("[4/4] back sprites: blank back slots filled from front")

        # sanity: re-open + verify the validator was redirected to a cave with the prop range
        vf2 = _load(os.path.join(TOOLS, "bw2_pokestar_validator_fix.py"), "vf2")
        r = ndspy.rom.NintendoDSRom.fromFile(outpath)
        a9 = vf2.read_arm9(r)
        prol = a9.find(vf2._sig()); assert prol >= 0, "validator prologue missing after build"
        site = prol + 0x2A
        # flag-site must now be a BL (0xF000 high halfword) not the original ldrh r1,[r5,#4]
        hw = struct.unpack_from('<H', a9, site)[0]
        assert (hw & 0xF800) == 0xF000, f"validator not redirected (hw={hw:04x})"
        assert 652 .to_bytes(4,'little') in a9 and (684).to_bytes(4,'little') in a9, "prop range literals missing"
        print("[verify] validator redirected to species-aware cave; prop range 652..684 present; output ok")
        print("[done] ->", outpath)
    finally:
        shutil.rmtree(work, ignore_errors=True)

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("rom", help="clean Black 2 or White 2 .nds")
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    build(a.rom, a.out)
