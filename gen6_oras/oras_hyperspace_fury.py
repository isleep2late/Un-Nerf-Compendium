#!/usr/bin/env python3
import argparse, hashlib, os, shutil, struct, sys

TITLES = {0x000400000011C400: "Omega Ruby", 0x000400000011C500: "Alpha Sapphire"}
TARGET = "DllBattle.cro"
TARGET_SIZE = 1089536
EDITS = [(0x000BC55C, bytes.fromhex("1100001a"), bytes.fromhex("110000ea"), "Hyperspace Fury user check -> always passes")]


def u32(b, o): return struct.unpack_from("<I", b, o)[0]
def u64(b, o): return struct.unpack_from("<Q", b, o)[0]
def align(x, a): return (x + a - 1) & ~(a - 1)


def locate(f):
    f.seek(0); h = f.read(0x2040)
    info = {}
    if h[0x100:0x104] == b"NCSD":
        info["ncch"] = u32(h, 0x120) * 0x200; info["cia"] = False
    else:
        hs, _t, _v, cert, tik, tmd, _meta = struct.unpack_from("<IHHIIII", h, 0)
        if hs != 0x2020:
            raise SystemExit("not a .3ds or .cia file")
        o = align(hs, 64); o = align(o + cert, 64); o = align(o + tik, 64)
        info.update(cia=True, tmd_off=o, tmd_size=tmd, ncch=align(o + tmd, 64))
    f.seek(info["ncch"]); n = f.read(0x200)
    if n[0x100:0x104] != b"NCCH":
        raise SystemExit("NCCH not found (a decrypted .3ds or .cia is required)")
    info["tid"] = u64(n, 0x108)
    if info["tid"] not in TITLES:
        raise SystemExit("this is not Omega Ruby or Alpha Sapphire (title %016X)" % info["tid"])
    romfs = info["ncch"] + u32(n, 0x1B0) * 0x200
    f.seek(romfs); iv = f.read(0x60)
    if iv[:4] != b"IVFC":
        raise SystemExit("RomFS not found (is the file decrypted?)")
    lv = [(u64(iv, 0x0C + 0x18 * k + 8), 1 << u32(iv, 0x0C + 0x18 * k + 16)) for k in range(3)]
    l3 = romfs + align(0x60 + u32(iv, 8), lv[2][1])
    l1 = l3 + align(lv[2][0], lv[2][1])
    l2 = l1 + align(lv[0][0], lv[0][1])
    info.update(romfs=romfs, levels=lv, l1=l1, l2=l2, l3=l3, sup=u32(n, 0x1B8) * 0x200)
    f.seek(l3); hh = struct.unpack("<10I", f.read(0x28))
    f.seek(l3 + hh[7]); fm = f.read(hh[8]); p = 0
    while p + 0x20 <= len(fm):
        nlen = u32(fm, p + 0x1C)
        if u32(fm, p) == 0 and fm[p + 0x20:p + 0x20 + nlen].decode("utf-16le", "replace") == TARGET and u64(fm, p + 0x10) == TARGET_SIZE:
            info["file"] = l3 + hh[9] + u64(fm, p + 8)
            return info
        p += 0x20 + align(nlen, 4)
    raise SystemExit("%s not found in the RomFS root" % TARGET)


def block(f, start, size, bs, i):
    f.seek(start + i * bs); b = f.read(bs)
    valid = max(0, min(bs, size - i * bs))
    return b[:valid] + b"\0" * (bs - valid)


def rehash(f, info, offsets, write):
    (s1, b1), (s2, b2), (s3, b3) = info["levels"]
    ok = True
    k3 = sorted({(o - info["l3"]) // b3 for o in offsets})
    for k in k3:
        d = hashlib.sha256(block(f, info["l3"], s3, b3, k)).digest()
        f.seek(info["l2"] + k * 32)
        ok &= write or f.read(32) == d
        if write: f.seek(info["l2"] + k * 32); f.write(d)
    k2 = sorted({k * 32 // b2 for k in k3})
    for k in k2:
        d = hashlib.sha256(block(f, info["l2"], s2, b2, k)).digest()
        f.seek(info["l1"] + k * 32)
        ok &= write or f.read(32) == d
        if write: f.seek(info["l1"] + k * 32); f.write(d)
    k1 = sorted({k * 32 // b1 for k in k2})
    for k in k1:
        d = hashlib.sha256(block(f, info["l1"], s1, b1, k)).digest()
        f.seek(info["romfs"] + 0x60 + k * 32)
        ok &= write or f.read(32) == d
        if write: f.seek(info["romfs"] + 0x60 + k * 32); f.write(d)
    return ok


def fix_tmd(f, info):
    f.seek(info["tmd_off"]); tmd = bytearray(f.read(info["tmd_size"]))
    sig = struct.unpack_from(">I", tmd, 0)[0]
    sizes = {0x10000: 0x200, 0x10001: 0x100, 0x10002: 0x3C, 0x10003: 0x200, 0x10004: 0x100, 0x10005: 0x3C}
    hdr = align(4 + sizes[sig], 64); info_rec = hdr + 0xC4; chunks = info_rec + 64 * 36
    count = struct.unpack_from(">H", tmd, hdr + 0x9E)[0]
    size = struct.unpack_from(">Q", tmd, chunks + 8)[0]
    print("[cia] rehashing content 0 for the TMD (%.2f GB)..." % (size / 1e9))
    h = hashlib.sha256(); f.seek(info["ncch"]); left = size
    while left:
        b = f.read(min(64 << 20, left)); h.update(b); left -= len(b)
    tmd[chunks + 16:chunks + 48] = h.digest()
    tmd[info_rec + 4:info_rec + 36] = hashlib.sha256(tmd[chunks:chunks + 48 * count]).digest()
    tmd[hdr + 0xA4:hdr + 0xC4] = hashlib.sha256(tmd[info_rec:info_rec + 64 * 36]).digest()
    f.seek(info["tmd_off"]); f.write(tmd)


def main():
    ap = argparse.ArgumentParser(description="Omega Ruby / Alpha Sapphire: let any Pokemon use Hyperspace Fury (edits RomFS DllBattle.cro)")
    ap.add_argument("rom", help="decrypted Omega Ruby / Alpha Sapphire .3ds or .cia")
    ap.add_argument("--out", help="output file (default: <name>_HyperspaceFury next to the input)")
    ap.add_argument("--inplace", action="store_true", help="patch the input file directly (no copy)")
    ap.add_argument("--verify", action="store_true", help="report the current state, write nothing")
    a = ap.parse_args()
    if not os.path.exists(a.rom):
        raise SystemExit("file not found: " + a.rom)
    with open(a.rom, "rb") as f:
        info = locate(f)
        state = []
        for off, old, new, label in EDITS:
            f.seek(info["file"] + off); cur = f.read(len(old))
            state.append("patched" if cur == new else "original" if cur == old else "unknown")
        print("[info] %s, %s @0x%X: %s" % (TITLES[info["tid"]], TARGET, info["file"], ", ".join(state)))
        if "unknown" in state:
            raise SystemExit("[abort] unexpected bytes in %s; this is not a clean v1.0 dump. Nothing was written." % TARGET)
        if not rehash(f, info, [info["file"] + o for o, _a, _b, _c in EDITS], False):
            raise SystemExit("[abort] the RomFS hash tree around %s does not verify. Nothing was written." % TARGET)
    if a.verify:
        print("[verify] %s; nothing written" % ("already patched" if set(state) == {"patched"} else "patch applies cleanly"))
        return
    if set(state) == {"patched"}:
        print("[done] already patched; nothing to do"); return
    out = a.rom if a.inplace else (a.out or os.path.splitext(a.rom)[0] + "_HyperspaceFury" + os.path.splitext(a.rom)[1])
    if not a.inplace and (os.path.realpath(out) == os.path.realpath(a.rom) or (os.path.exists(out) and os.path.samefile(out, a.rom))):
        raise SystemExit("--out names the input file itself; use --inplace to patch it directly. Nothing was written.")
    if out != a.rom:
        print("[copy] %s -> %s (%.2f GB)" % (os.path.basename(a.rom), os.path.basename(out), os.path.getsize(a.rom) / 1e9))
        shutil.copyfile(a.rom, out)
    with open(out, "r+b") as f:
        info = locate(f)
        for off, old, new, label in EDITS:
            f.seek(info["file"] + off); f.write(new)
            print("[patch] %s +0x%X: %s -> %s (%s)" % (TARGET, off, old.hex(), new.hex(), label))
        rehash(f, info, [info["file"] + o for o, _a, _b, _c in EDITS], True)
        f.flush()
        if not rehash(f, info, [info["file"] + o for o, _a, _b, _c in EDITS], False):
            raise SystemExit("[error] RomFS hashes did not verify after writing")
        f.seek(info["romfs"]); d = hashlib.sha256(f.read(info["sup"])).digest()
        f.seek(info["ncch"] + 0x1E0); f.write(d)
        if info["cia"]:
            f.flush(); fix_tmd(f, info)
    print("[done] Hyperspace Fury can be used by any Pokemon -> %s" % os.path.basename(out))
    print("[note] Test in a fresh battle after a clean boot, not a loaded save state.")


if __name__ == "__main__":
    main()
