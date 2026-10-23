#!/usr/bin/env python3
import argparse
import os
import struct
import sys

SIG = [(0x1C, 0xC0), (0x29, 0x0E), (0x39, 0xC0), (0x3A, 0x07),
       (0x46, 0x98), (0x47, 0x7E), (0x5A, 0xD8), (0x5B, 0x03)]
RECORD = 188
BAN_START = 0x1C
BAN_END = 0x78
SPECIES_CLAUSE = 0x08
ITEM_CLAUSE = 0x09
GAMES = {b"IRBO": "Black", b"IRAO": "White"}


def u16(d, o):
    return struct.unpack_from("<H", d, o)[0]


def u32(d, o):
    return struct.unpack_from("<I", d, o)[0]


def find_file(buf, path):
    fnt = u32(buf, 0x40)
    fat = u32(buf, 0x48)
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
            s, e = struct.unpack_from("<II", buf, fat + hit[1] * 8)
            return s, e - s
        cur = hit[1]
    raise SystemExit("%s is a directory, not a file" % path)


def narc_fat(buf, base):
    if bytes(buf[base:base + 4]) != b"NARC":
        raise SystemExit("the regulation file is not a NARC")
    p = base + u16(buf, base + 0xC)
    if bytes(buf[p:p + 4]) != b"BTAF":
        raise SystemExit("the regulation NARC has no BTAF block")
    cnt = u16(buf, p + 8)
    fat = [(u32(buf, p + 12 + i * 8), u32(buf, p + 16 + i * 8)) for i in range(cnt)]
    p += u32(buf, p + 4)
    p += u32(buf, p + 4)
    if bytes(buf[p:p + 4]) != b"GMIF":
        raise SystemExit("the regulation NARC has no GMIF block")
    return p + 8, fat


def patch(buf, path, want_records, want_banning):
    a_off, a_len = find_file(buf, path)
    img, fat = narc_fat(buf, a_off)
    if want_records is not None and len(fat) != want_records:
        raise SystemExit("%s holds %d records, expected %d: not the clean game this tool is for"
                         % (path, len(fat), want_records))
    hits = []
    for idx, (s, e) in enumerate(fat):
        if e - s != RECORD:
            continue
        base = img + s
        if all(buf[base + o] == v for o, v in SIG):
            hits.append(base)
    if want_banning is not None and len(hits) != want_banning:
        raise SystemExit("%d banning records match the signature, expected %d: not the clean game this tool is for"
                         % (len(hits), want_banning))
    n_bytes = 0
    for base in hits:
        for o in range(BAN_START, BAN_END):
            if buf[base + o] != 0:
                buf[base + o] = 0
                n_bytes += 1
        for o in (SPECIES_CLAUSE, ITEM_CLAUSE):
            if buf[base + o] != 0x01:
                buf[base + o] = 0x01
                n_bytes += 1
    return len(hits), n_bytes


def main():
    ap = argparse.ArgumentParser(description="Pokemon Black / White: remove the Battle Subway and link-rule "
                                             "ban list and the Species / Item Clauses (regulation NARC a/1/0/5), "
                                             "in place and length-preserving.")
    ap.add_argument("nds")
    ap.add_argument("--out")
    ap.add_argument("--inplace", action="store_true")
    ap.add_argument("--narc", default="a/1/0/5")
    ap.add_argument("--records", type=int, default=29)
    ap.add_argument("--banning", type=int, default=18)
    ap.add_argument("--any-count", action="store_true",
                    help="skip the record and banning-record count checks")
    a = ap.parse_args()
    if not os.path.exists(a.nds):
        raise SystemExit("file not found: " + a.nds)
    with open(a.nds, "rb") as f:
        head = f.read(0x10)
    code = head[0x0C:0x10]
    if code not in GAMES and not a.any_count:
        raise SystemExit("game code %r is not Pokemon Black (IRBO) or White (IRAO)" % code)
    out = a.nds if a.inplace else (a.out or os.path.splitext(a.nds)[0] + "_nobanlist.nds")
    with open(a.nds, "rb") as f:
        buf = bytearray(f.read())
    want_r = None if a.any_count else a.records
    want_b = None if a.any_count else a.banning
    nf, nb = patch(buf, a.narc, want_r, want_b)
    if nf == 0:
        raise SystemExit("no banning record matched: a clean Pokemon Black / White .nds is expected")
    with open(out, "wb") as f:
        f.write(buf)
    print("%s: %d bytes changed across %d regulation records -> %s"
          % (GAMES.get(code, "?"), nb, nf, os.path.basename(out)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
