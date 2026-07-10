#!/usr/bin/env python3
r"""
Black 2 / White 2 -- remove ALL Battle Subway + Battle Institute + PWT restrictions
(legendary banlist + species clause + item clause) while KEEPING the legal party-size
limit (no crash) and the PWT cup index (no bracket freeze). Length-preserving in-place
edit of regulation NARC a/1/0/6; pure stdlib. Works on Black 2 and White 2.

Each restriction file in a/1/0/6 is a fixed 188-byte regulation structure:
  0x02, 0x03  party size to bring (3 single / 4 double / 6 triple) - the legal LIMIT
  0x04        0x32 = Lv50 cap
  0x05        facility id (02 Institute, 03 Subway, 04 PWT)
  0x08        SPECIES clause (0x00 enforced, 0x01 duplicates allowed)
  0x09        ITEM clause    (0x00 enforced, 0x01 duplicates allowed)
  0x1C-0x77   species banlist bitfield (MeroMero's legendary ban bits)
  0xBA        per-mode / cup index (PWT bracket loader needs it - do not touch)

Fix per banning file: zero 0x1C..0x77, set 0x08=01 and 0x09=01, leave party + cup alone.

  python bw2_nobanlist.py "Pokemon Black 2.nds"
  python bw2_nobanlist.py game.nds --out custom.nds --inplace
"""
import struct, os, shutil, argparse

def u16(d, o):
    return struct.unpack_from("<H", d, o)[0]

def u32(d, o):
    return struct.unpack_from("<I", d, o)[0]

SIG = [(0x1C, 0xC0), (0x29, 0x0E), (0x39, 0xC0), (0x3A, 0x07),
       (0x46, 0x98), (0x47, 0x7E), (0x5A, 0xD8), (0x5B, 0x03)]

def find_a106(buf):
    fnt_off = u32(buf, 0x40)
    fat_off = u32(buf, 0x48)
    fnt = buf[fnt_off:fnt_off + u32(buf, 0x44)]
    def fat(fid):
        return struct.unpack_from("<II", buf, fat_off + fid * 8)
    def de(did):
        idx = did & 0xFFF
        so = u32(fnt, idx * 8)
        fid = u16(fnt, idx * 8 + 4)
        p = so
        out = {}
        while True:
            t = fnt[p]
            p += 1
            if t == 0:
                break
            ln = t & 0x7F
            nm = fnt[p:p + ln].decode("ascii", "replace")
            p += ln
            if t & 0x80:
                out[nm] = ("dir", u16(fnt, p))
                p += 2
            else:
                out[nm] = ("file", fid)
                fid += 1
        return out
    cur = 0xF000
    for part in "a/1/0/6".split("/"):
        e = de(cur)
        if part not in e:
            raise SystemExit("a/1/0/6 not found -- is this a Black2/White2 .nds?")
        typ, val = e[part]
        if typ == "file":
            s, en = fat(val)
            return s, en - s
        cur = val
    raise SystemExit("a/1/0/6 is not a file")

def narc_fat(buf, base):
    if buf[base:base + 4] != b"NARC":
        raise SystemExit("a/1/0/6 is not a NARC")
    p = base + 0x10
    assert buf[p:p + 4] == b"BTAF"
    cnt = u16(buf, p + 8)
    ent = p + 0xC
    fat = [(u32(buf, ent + i * 8), u32(buf, ent + i * 8 + 4)) for i in range(cnt)]
    p += u32(buf, p + 4)
    p += u32(buf, p + 4)
    img = p + 8
    return img, fat

def patch(buf):
    a_off, a_len = find_a106(buf)
    img, fat = narc_fat(buf, a_off)
    n_files = 0
    n_bytes = 0
    for idx, ent in enumerate(fat):
        s, e = ent
        if e - s != 188:
            continue
        base = img + s
        if not all(buf[base + o] == v for o, v in SIG):
            continue
        for o in range(0x1C, 0x78):
            if buf[base + o] != 0:
                buf[base + o] = 0
                n_bytes += 1
        if buf[base + 0x08] != 0x01:
            buf[base + 0x08] = 0x01
            n_bytes += 1
        if buf[base + 0x09] != 0x01:
            buf[base + 0x09] = 0x01
            n_bytes += 1
        n_files += 1
    return n_files, n_bytes

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("nds")
    ap.add_argument("--out")
    ap.add_argument("--inplace", action="store_true")
    a = ap.parse_args()
    if not os.path.exists(a.nds):
        raise SystemExit("file not found: " + a.nds)
    out = a.nds if a.inplace else (a.out or os.path.splitext(a.nds)[0] + "_norestrictions.nds")
    if not a.inplace and out != a.nds:
        shutil.copyfile(a.nds, out)
    with open(out, "r+b") as f:
        buf = bytearray(f.read())
        nf, nb = patch(buf)
        if nf == 0:
            raise SystemExit("[abort] no banlist sub-files matched -- unmodified B2/W2 .nds expected.")
        f.seek(0)
        f.write(buf)
    print("[done] %d bytes across %d regulation files -> %s" % (nb, nf, os.path.basename(out)))
    print("[note] Subway+Institute+PWT: legends unbanned, species+item clause off, party limit & cup index kept.")

if __name__ == "__main__":
    main()
