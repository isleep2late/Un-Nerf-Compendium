#!/usr/bin/env python3
import argparse
import os
import struct
import sys

TYPES = ["Normal", "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost", "Steel", "Fire",
         "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon", "Dark"]
ARCEUS = 493
T1 = 0x06
T2 = 0x07
FSI = 0x1C
FCNT = 0x20
GAMES = {b"IRBO": "Black", b"IRAO": "White"}
ALIGN = 0x200


def u16(d, o):
    return struct.unpack_from("<H", d, o)[0]


def u32(d, o):
    return struct.unpack_from("<I", d, o)[0]


def crc16(data):
    crc = 0xFFFF
    for x in data:
        crc ^= x
        for _ in range(8):
            crc = (crc >> 1) ^ 0xA001 if crc & 1 else crc >> 1
    return crc


def find_fid(buf, path):
    fnt = u32(buf, 0x40)
    cur = 0xF000
    parts = path.split("/")
    for k, part in enumerate(parts):
        idx = cur & 0xFFF
        p = fnt + u32(buf, fnt + idx * 8)
        fid = u16(buf, fnt + idx * 8 + 4)
        hit = None
        while hit is None:
            t = buf[p]
            p += 1
            if t == 0:
                break
            ln = t & 0x7F
            nm = bytes(buf[p:p + ln]).decode("ascii", "replace")
            p += ln
            if t & 0x80:
                if nm == part:
                    hit = ("dir", u16(buf, p))
                p += 2
            else:
                if nm == part:
                    hit = ("file", fid)
                fid += 1
        if hit is None:
            raise SystemExit("%s not found in this .nds" % path)
        if hit[0] == "file":
            if k != len(parts) - 1:
                raise SystemExit("%s is not a directory path" % path)
            return hit[1]
        cur = hit[1]
    raise SystemExit("%s is a directory, not a file" % path)


def fat_get(buf, fid):
    return struct.unpack_from("<II", buf, u32(buf, 0x48) + fid * 8)


def fat_set(buf, fid, s, e):
    struct.pack_into("<II", buf, u32(buf, 0x48) + fid * 8, s, e)


def narc_parse(b):
    if bytes(b[:4]) != b"NARC":
        raise SystemExit("not a NARC")
    o = u16(b, 0xC)
    if bytes(b[o:o + 4]) != b"BTAF":
        raise SystemExit("NARC without BTAF")
    n = u16(b, o + 8)
    fat = [(u32(b, o + 12 + 8 * i), u32(b, o + 16 + 8 * i)) for i in range(n)]
    o2 = o + u32(b, o + 4)
    if bytes(b[o2:o2 + 4]) != b"BTNF":
        raise SystemExit("NARC without BTNF")
    fntb = bytes(b[o2 + 8:o2 + u32(b, o2 + 4)])
    o3 = o2 + u32(b, o2 + 4)
    if bytes(b[o3:o3 + 4]) != b"GMIF":
        raise SystemExit("NARC without GMIF")
    base = o3 + 8
    return [bytes(b[base + s:base + e]) for s, e in fat], fntb, u16(b, 6)


def narc_build(files, fntb, ver):
    img = bytearray()
    fat = bytearray()
    for f in files:
        while len(img) % 4:
            img += b"\xFF"
        s = len(img)
        img += f
        fat += struct.pack("<II", s, s + len(f))
    while len(img) % 4:
        img += b"\xFF"
    btaf = b"BTAF" + struct.pack("<IHH", 12 + len(fat), len(files), 0) + bytes(fat)
    btnf = b"BTNF" + struct.pack("<I", 8 + len(fntb)) + fntb
    gmif = b"GMIF" + struct.pack("<I", 8 + len(img)) + bytes(img)
    total = 16 + len(btaf) + len(btnf) + len(gmif)
    return b"NARC" + struct.pack("<HHIHH", 0xFFFE, ver, total, 16, 3) + btaf + btnf + gmif


def load_narc(buf, path):
    fid = find_fid(buf, path)
    s, e = fat_get(buf, fid)
    raw = bytes(buf[s:e])
    files, fntb, ver = narc_parse(raw)
    if narc_build(files, fntb, ver) != raw:
        raise SystemExit("%s does not rebuild byte for byte; refusing to touch it" % path)
    return fid, files, fntb, ver


def build(buf, personal_path, learnset_path, evolution_path):
    pfid, pfiles, pfnt, pver = load_narc(buf, personal_path)
    lfid, lfiles, lfnt, lver = load_narc(buf, learnset_path)
    efid, efiles, efnt, ever = load_narc(buf, evolution_path)
    rows = len(pfiles)
    if rows <= ARCEUS:
        raise SystemExit("%s has only %d rows" % (personal_path, rows))
    base = bytearray(pfiles[ARCEUS])
    width = len(base)
    if width not in (0x3C, 0x4C):
        raise SystemExit("Arceus personal row is %d bytes, expected 60 (B/W) or 76 (B2/W2)" % width)
    fc = base[FCNT]
    if u16(base, FSI) != 0:
        raise SystemExit("Arceus FormStatsIndex is already %d: this ROM was patched before" % u16(base, FSI))
    if base[T1] != 0 or base[T2] != 0 or fc != 17:
        raise SystemExit("Arceus row is not Normal/Normal with 17 forms: not a clean Gen 5 personal table")
    if len(lfiles) > rows or len(efiles) > rows:
        raise SystemExit("learnset/evolution tables are longer than the personal table: unexpected layout")
    first = rows
    last = first + fc - 2
    for form in range(1, fc):
        e = bytearray(base)
        e[T1] = form
        e[T2] = form
        struct.pack_into("<H", e, FSI, 0)
        e[FCNT] = 0
        pfiles.append(bytes(e))
    patched = bytearray(base)
    struct.pack_into("<H", patched, FSI, first)
    pfiles[ARCEUS] = bytes(patched)
    want = len(pfiles)
    lb, eb = len(lfiles), len(efiles)
    empty_learnset = lfiles[0]
    zero_evo = bytes(len(efiles[ARCEUS]))
    for k in range(lb, want):
        lfiles.append(lfiles[ARCEUS] if first <= k <= last else empty_learnset)
    for k in range(eb, want):
        efiles.append(efiles[ARCEUS] if first <= k <= last else zero_evo)
    new = [(pfid, narc_build(pfiles, pfnt, pver)),
           (lfid, narc_build(lfiles, lfnt, lver)),
           (efid, narc_build(efiles, efnt, ever))]
    ntr_end = u32(buf, 0x80)
    if buf[0x12] & 2:
        limit = u16(buf, 0x92) * 0x80000
    else:
        limit = len(buf)
    limit = min(limit, len(buf))
    pos = (ntr_end + ALIGN - 1) // ALIGN * ALIGN
    start = pos
    places = []
    for fid, blob in new:
        places.append((fid, pos, blob))
        pos = (pos + len(blob) + ALIGN - 1) // ALIGN * ALIGN
    end = pos
    if end > limit:
        raise SystemExit("no room after the NTR data: need 0x%X..0x%X, limit 0x%X" % (start, end, limit))
    if any(x != 0xFF for x in buf[ntr_end:end]):
        raise SystemExit("0x%X..0x%X is not free (0xFF) space; refusing" % (ntr_end, end))
    for fid, at, blob in places:
        buf[at:at + len(blob)] = blob
        fat_set(buf, fid, at, at + len(blob))
    struct.pack_into("<I", buf, 0x80, end)
    struct.pack_into("<H", buf, 0x15E, crc16(buf[:0x15E]))
    return {"rows": rows, "first": first, "last": last, "fc": fc, "learnset": (lb, len(lfiles)),
            "evolution": (eb, len(efiles)), "start": start, "end": end, "ntr_end": ntr_end}


def verify(buf, personal_path, learnset_path, evolution_path, info):
    pfid, pfiles, _, _ = load_narc(buf, personal_path)
    _, lfiles, _, _ = load_narc(buf, learnset_path)
    _, efiles, _, _ = load_narc(buf, evolution_path)
    f2 = u16(pfiles[ARCEUS], FSI)
    got = [pfiles[f2 + k - 1][T1] for k in range(1, info["fc"])]
    if got != list(range(1, info["fc"])):
        raise SystemExit("verify failed: Arceus form rows read back as %r" % got)
    if len(lfiles) < len(pfiles) or len(efiles) < len(pfiles):
        raise SystemExit("verify failed: learnset/evolution rows %d/%d < personal rows %d"
                         % (len(lfiles), len(efiles), len(pfiles)))
    if u16(buf, 0x15E) != crc16(buf[:0x15E]):
        raise SystemExit("verify failed: header CRC")
    return ", ".join(TYPES[t] for t in got), (len(pfiles), len(lfiles), len(efiles))


def main():
    ap = argparse.ArgumentParser(description="Pokemon Black / White: Arceus form typing. Appends 16 typed "
                                             "Arceus rows to the personal table and points Arceus's "
                                             "FormStatsIndex at them, with matching learnset and evolution "
                                             "rows. The three tables are written into the free space after "
                                             "the NTR data; every other byte of the ROM stays where it was.")
    ap.add_argument("nds")
    ap.add_argument("--out")
    ap.add_argument("--inplace", action="store_true")
    ap.add_argument("--personal", default="a/0/1/6")
    ap.add_argument("--learnset", default="a/0/1/8")
    ap.add_argument("--evolution", default="a/0/1/9")
    ap.add_argument("--any-game", action="store_true", help="skip the Black / White game code check")
    a = ap.parse_args()
    if not os.path.exists(a.nds):
        raise SystemExit("file not found: " + a.nds)
    with open(a.nds, "rb") as f:
        buf = bytearray(f.read())
    code = bytes(buf[0x0C:0x10])
    if code not in GAMES and not a.any_game:
        raise SystemExit("game code %r is not Pokemon Black (IRBO) or White (IRAO)" % code)
    info = build(buf, a.personal, a.learnset, a.evolution)
    line, counts = verify(buf, a.personal, a.learnset, a.evolution, info)
    out = a.nds if a.inplace else (a.out or os.path.splitext(a.nds)[0] + "_arceustyped.nds")
    with open(out, "wb") as f:
        f.write(buf)
    print("%s: personal %d rows, Arceus FormStatsIndex 0 -> %d (+%d typed forms)"
          % (GAMES.get(code, "?"), info["rows"], info["first"], info["fc"] - 1))
    print("  learnset %d -> %d, evolution %d -> %d rows (rows %d..%d copy Arceus)"
          % (info["learnset"] + info["evolution"] + (info["first"], info["last"])))
    print("  tables written at 0x%X..0x%X (NTR end was 0x%X)" % (info["start"], info["end"], info["ntr_end"]))
    print("  verify: Arceus forms 1..%d = %s" % (info["fc"] - 1, line))
    print("  verify: personal %d, learnset %d, evolution %d rows" % counts)
    print("  wrote " + out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
