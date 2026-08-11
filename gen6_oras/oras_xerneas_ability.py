#!/usr/bin/env python3
r"""
Omega Ruby / Alpha Sapphire -- let a hacked Ability stay on Xerneas.

Xerneas keeps Fairy Aura by default, exactly as it should. What this removes is the engine's habit
of putting Fairy Aura back on a Xerneas whose Ability you edited.

Why it happens: Xerneas carries Fairy Aura in all three Ability slots of the personal table and has
FormStatsIndex = 0, so its Neutral and Active formes share one personal row. Whenever the engine
re-derives a Pokemon's Ability from that row it calls

    r0 = pml::personal ability getter (species, form, slot)
    pml::pokepara::CoreParam::SetTokusei(mon, r0)

and for Xerneas the getter always answers Fairy Aura, so the write is always destructive. Xerneas is
put through a forme change on the way into battle, which is one of those re-derivation paths.

The fix guards the WRITE, not the callers. SetTokusei has only three call sites in the executable;
the two that re-derive from the personal table are patched so the call becomes conditional and is
skipped when the species is Xerneas:

    cmp   rN, #0x2CC
    blne  SetTokusei

Guarding the write covers every path that reaches it, including calls made from the battle module,
which is why this succeeds where NOPing an individual caller does not. Nothing else changes: Megas,
Primals, Zygarde, Kyogre/Groudon, Hoopa and every other forme keep re-deriving normally, because the
guard only fires for species 716.

Both patched functions have a spare instruction slot immediately after the call, so the displaced
instruction is relocated into it and no code is lost.

  python oras_xerneas_ability.py "Pokemon Omega Ruby.3ds"
  python oras_xerneas_ability.py game.cia --inplace
  python oras_xerneas_ability.py game.3ds --verify
"""
import struct, hashlib, os, shutil, argparse

CODE_VBASE = 0x100000
ARMNOP = 0xE320F000

TITLES = {
    0x000400000011C500: "Alpha Sapphire",
    0x000400000011C400: "Omega Ruby",
}

def u32(d, o): return struct.unpack_from("<I", d, o)[0]
def u64(d, o): return struct.unpack_from("<Q", d, o)[0]
def align(x, a): return (x + a - 1) & ~(a - 1)

def bl_word(frm, to, cond=0xE):
    off = (to - (frm + 8)) >> 2
    return (cond << 28) | (0xB << 24) | (off & 0xFFFFFF)

def bl_target(w, frm):
    if ((w >> 24) & 0xF) != 0xB: return None
    imm = w & 0xFFFFFF
    if imm & 0x800000: imm -= 0x1000000
    return frm + 8 + imm * 4

def locate(f):
    f.seek(0); sig = f.read(0x200)
    if sig[0x100:0x104] == b"NCSD":
        ncch = u32(sig, 0x120) * 0x200; is_cia = False; tmd_off = tmd_sz = 0
    else:
        f.seek(0); head = f.read(0x2020)
        hs, _t, _v, cert, tik, tmd, _m = struct.unpack_from("<IHHIIII", head, 0)
        co = align(hs, 64); to = align(co + cert, 64)
        tmd_off = align(to + tik, 64); ncch = align(tmd_off + tmd, 64)
        is_cia = True; tmd_sz = tmd
    f.seek(ncch); h = f.read(0x200)
    if h[0x100:0x104] != b"NCCH":
        raise SystemExit("NCCH not found (a DECRYPTED .3ds/.cia is required).")
    exo = u32(h, 0x1A0) * 0x200; exhr = u32(h, 0x1A8) * 0x200
    f.seek(ncch + exo); eh = bytearray(f.read(0x200)); cfo = cfsz = None
    for i in range(10):
        if eh[i * 0x10:i * 0x10 + 8].rstrip(b"\0") == b".code":
            cfo, cfsz = struct.unpack_from("<II", eh, i * 0x10 + 8)
    if cfo is None:
        raise SystemExit("ExeFS .code not found.")
    return dict(ncch=ncch, is_cia=is_cia, tmd_off=tmd_off, tmd_sz=tmd_sz, tid=u64(h, 0x118),
                exo=exo, exhr=exhr, eh=eh, code_abs=ncch + exo + 0x200 + cfo, code_sz=cfsz)

PATCHES = [
    dict(name="ChangeFormNo",
         anchor=0x3B4334,
         old=[0xE596200C, 0xE1A01000, 0xE1A00002, None, 0xE596000C, ARMNOP],
         new=[0xE1A01000, 0xE59D2000, 0xE596000C, 0xE3520FB3, "BLNE", 0xE596000C]),
    dict(name="ChangeMonsNoForm",
         anchor=0x11F594,
         old=[0xE594000C, 0xE1A01007, None, 0xE594000C, ARMNOP],
         new=[0xE594000C, 0xE1A01007, 0xE3560FB3, "BLNE", 0xE594000C]),
]

def resolve(code, p):
    """Return (setter_vaddr, list_of(vaddr, oldword, newword)) or None if it does not match."""
    words = []
    setter = None
    for i, exp in enumerate(p["old"]):
        va = p["anchor"] + i * 4
        cur = u32(code, va - CODE_VBASE)
        if exp is None:
            t = bl_target(cur, va)
            if t is None: return None
            setter = t
        elif cur != exp:
            return None
        words.append((va, cur))
    out = []
    for i, nw in enumerate(p["new"]):
        va = p["anchor"] + i * 4
        if nw == "BLNE":
            nw = bl_word(va, setter, cond=0x1)
        out.append((va, words[i][1], nw))
    return setter, out

def already(code, p):
    for i, nw in enumerate(p["new"]):
        va = p["anchor"] + i * 4
        cur = u32(code, va - CODE_VBASE)
        if nw == "BLNE":
            if ((cur >> 28) & 0xF) != 0x1 or ((cur >> 24) & 0xF) != 0xB: return False
        elif cur != nw:
            return False
    return True

def sha_region(f, off, size):
    h = hashlib.sha256(); f.seek(off); rem = size
    while rem > 0:
        b = f.read(min(8 << 20, rem))
        if not b: break
        h.update(b); rem -= len(b)
    return h.digest()

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("rom"); ap.add_argument("--out")
    ap.add_argument("--inplace", action="store_true")
    ap.add_argument("--verify", action="store_true", help="report current state, write nothing")
    a = ap.parse_args()
    if not os.path.exists(a.rom):
        raise SystemExit("file not found: " + a.rom)

    with open(a.rom, "rb") as f:
        info = locate(f)
        name = TITLES.get(info["tid"])
        if name is None:
            raise SystemExit("Title 0x%016X is not Omega Ruby or Alpha Sapphire." % info["tid"])
        f.seek(info["code_abs"]); code = f.read(info["code_sz"])
        print("  %s  (Title 0x%016X, %s)" % (name, info["tid"], "cia" if info["is_cia"] else "3ds"))
        plan = []
        for p in PATCHES:
            if already(code, p):
                print("  %-16s @ 0x%06X : ALREADY PATCHED" % (p["name"], p["anchor"])); continue
            r = resolve(code, p)
            if r is None:
                raise SystemExit("%s does not match at 0x%06X on this build. Refusing to guess. "
                                 "Nothing was written." % (p["name"], p["anchor"]))
            setter, words = r
            print("  %-16s @ 0x%06X : guardable, SetTokusei = 0x%X"
                  % (p["name"], p["anchor"], setter))
            plan.append((p, words))
        if not plan:
            print("  nothing to do")
    if a.verify:
        return

    ext = os.path.splitext(a.rom)[1]
    out = a.rom if a.inplace else (a.out or os.path.splitext(a.rom)[0] + "_XerneasAbility" + ext)
    if not a.inplace and out != a.rom:
        print("[copy] duplicating ROM (%.2f GB)..." % (os.path.getsize(a.rom) / 1e9))
        shutil.copyfile(a.rom, out)

    with open(out, "r+b") as f:
        info = locate(f)
        f.seek(info["code_abs"]); code = f.read(info["code_sz"])
        n = 0
        for p in PATCHES:
            if already(code, p):
                print("[fix] %s: already patched" % p["name"]); continue
            r = resolve(code, p)
            if r is None:
                raise SystemExit("%s stopped matching mid-run. Aborting." % p["name"])
            _setter, words = r
            for va, old, new in words:
                if old == new: continue
                f.seek(info["code_abs"] + va - CODE_VBASE)
                f.write(struct.pack("<I", new)); n += 1
                print("[fix] 0x%06X: %08X -> %08X" % (va, old, new))
        f.flush()

        code_hash = sha_region(f, info["code_abs"], info["code_sz"])
        eh = info["eh"]; eh[0x200 - 0x20:0x200] = code_hash
        f.seek(info["ncch"] + info["exo"]); f.write(bytes(eh))
        sup = sha_region(f, info["ncch"] + info["exo"], info["exhr"])
        f.seek(info["ncch"] + 0x1C0); f.write(sup)
        f.flush()
        print("[hash] ExeFS .code + ExeFS superblock (RomFS untouched)")

        if info["is_cia"]:
            f.seek(info["tmd_off"]); td = bytearray(f.read(info["tmd_sz"])); th = 0x140
            base = th + 0xC4 + 64 * 0x24
            c0 = struct.unpack_from(">Q", td, base + 0x08)[0]
            print("[cia] rehashing content0 for the TMD (%.2f GB, one-time)..." % (c0 / 1e9))
            td[base + 0x10:base + 0x30] = sha_region(f, info["ncch"], c0)
            ci = th + 0xC4; idx, cnt = struct.unpack_from(">HH", td, ci)
            td[ci + 0x04:ci + 0x24] = hashlib.sha256(
                bytes(td[base + idx * 0x30:base + (idx + max(cnt, 1)) * 0x30])).digest()
            td[th + 0xA4:th + 0xC4] = hashlib.sha256(bytes(td[th + 0xC4:th + 0xC4 + 64 * 0x24])).digest()
            f.seek(info["tmd_off"]); f.write(bytes(td)); f.flush()

        f.seek(info["code_abs"]); code = f.read(info["code_sz"])
        print("[verify]")
        for p in PATCHES:
            print("  %-16s : %s" % (p["name"], "OK" if already(code, p) else "FAILED"))
    print("[done] %d words written -> %s" % (n, os.path.basename(out)))
    print("[note] Set the Ability in PKHeX/PKHaX, then battle from a clean boot, not a save state.")
    print("[note] After the battle ends, reopen the save and confirm the Ability is still there.")

if __name__ == "__main__":
    main()
