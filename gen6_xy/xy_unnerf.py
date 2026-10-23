#!/usr/bin/env python3
import argparse, hashlib, os, struct, sys, zlib

FEATURES = ("evcap", "xerneas", "arceus", "formepersist")
CODE = {
    "v1.0": dict(size=5156864, cave=0x53bedc, edits=[
        ("evcap", 0x0D5A50, "fe010000", "fa050000"),
        ("arceus", 0x29BD14, "79005003", "0000a0e1"),
        ("arceus", 0x29BD40, "00f020e3", "658006ea"),
        ("arceus", 0x29C060, "0100a0e3", "0000a0e1"),
        ("xerneas", 0x29C4DC, "0c2096e5", "0010a0e1"),
        ("xerneas", 0x29C4E0, "0010a0e1", "00209de5"),
        ("xerneas", 0x29C4E4, "0200a0e1", "0c0096e5"),
        ("xerneas", 0x29C4E8, "622ff7eb", "b30f52e3"),
        ("xerneas", 0x29C4EC, "0c0096e5", "612ff71b"),
        ("xerneas", 0x29C4F0, "00f020e3", "0c0096e5"),
        ("xerneas", 0x29C600, "1c2ff7eb", "b30f56e3"),
        ("xerneas", 0x29C604, "0c0094e5", "1b2ff71b"),
        ("xerneas", 0x29C608, "00f020e3", "0c0094e5"),
        ("formepersist", 0x33EA4C, "4876fdeb", "0000a0e1"),
        ("formepersist", 0x33EA84, "3a76fdea", "1eff2fe1"),
        ("formepersist", 0x33F5E8, "6173fdeb", "0000a0e1"),
        ("formepersist", 0x33F61C, "5473fdeb", "0000a0e1"),
        ("formepersist", 0x33F68C, "3873fdeb", "0000a0e1"),
        ("formepersist", 0x33F6FC, "1c73fdeb", "0000a0e1"),
        ("formepersist", 0x33F708, "d46dfdeb", "0000a0e1"),
        ("formepersist", 0x33F714, "306efdeb", "0000a0e1"),
        ("formepersist", 0x33F728, "1173fdeb", "0000a0e1"),
        ("formepersist", 0x33F824, "d272fdeb", "0000a0e1"),
        ("formepersist", 0x33F830, "8a6dfdeb", "0000a0e1"),
        ("formepersist", 0x33F83C, "e66dfdeb", "0000a0e1"),
        ("formepersist", 0x33F928, "9172fdeb", "0000a0e1"),
        ("formepersist", 0x33F9CC, "6872fdeb", "0000a0e1"),
        ("formepersist", 0x33FAEC, "2072fdeb", "0000a0e1"),
        ("formepersist", 0x33FD80, "7b71fdeb", "0000a0e1"),
        ("arceus", 0x3A1A58, "79005003", "0000a0e1"),
        ("arceus", 0x3A1A84, "00f020e3", "146902ea"),
        ("arceus", 0x43BEDC, "00000000", "08d04de2"),
        ("arceus", 0x43BEE0, "00000000", "0c0094e5"),
        ("arceus", 0x43BEE4, "00000000", "6693fdeb"),
        ("arceus", 0x43BEE8, "00000000", "0010a0e1"),
        ("arceus", 0x43BEEC, "00000000", "1e0ea0e3"),
        ("arceus", 0x43BEF0, "00000000", "0d0080e2"),
        ("arceus", 0x43BEF4, "00000000", "0d20a0e1"),
        ("arceus", 0x43BEF8, "00000000", "1180f9eb"),
        ("arceus", 0x43BEFC, "00000000", "08d08de2"),
        ("arceus", 0x43BF00, "00000000", "000050e3"),
        ("arceus", 0x43BF04, "00000000", "7080bd18"),
        ("arceus", 0x43BF08, "00000000", "0c0094e5"),
        ("arceus", 0x43BF0C, "00000000", "255cf0eb"),
        ("arceus", 0x43BF10, "00000000", "7080bde8"),
    ]),
    "v1.5": dict(size=5169152, cave=0x53ed4c, edits=[
        ("evcap", 0x0D5D48, "fe010000", "fa050000"),
        ("arceus", 0x29CF0C, "79005003", "0000a0e1"),
        ("arceus", 0x29CF38, "00f020e3", "838706ea"),
        ("arceus", 0x29D258, "0100a0e3", "0000a0e1"),
        ("xerneas", 0x29D6D4, "0c2096e5", "0010a0e1"),
        ("xerneas", 0x29D6D8, "0010a0e1", "00209de5"),
        ("xerneas", 0x29D6DC, "0200a0e1", "0c0096e5"),
        ("xerneas", 0x29D6E0, "6d2bf7eb", "b30f52e3"),
        ("xerneas", 0x29D6E4, "0c0096e5", "6c2bf71b"),
        ("xerneas", 0x29D6E8, "00f020e3", "0c0096e5"),
        ("xerneas", 0x29D7F8, "272bf7eb", "b30f56e3"),
        ("xerneas", 0x29D7FC, "0c0094e5", "262bf71b"),
        ("xerneas", 0x29D800, "00f020e3", "0c0094e5"),
        ("formepersist", 0x34049C, "3274fdeb", "0000a0e1"),
        ("formepersist", 0x3404D4, "2474fdea", "1eff2fe1"),
        ("formepersist", 0x341038, "4b71fdeb", "0000a0e1"),
        ("formepersist", 0x34106C, "3e71fdeb", "0000a0e1"),
        ("formepersist", 0x3410DC, "2271fdeb", "0000a0e1"),
        ("formepersist", 0x34114C, "0671fdeb", "0000a0e1"),
        ("formepersist", 0x341158, "be6bfdeb", "0000a0e1"),
        ("formepersist", 0x341164, "1a6cfdeb", "0000a0e1"),
        ("formepersist", 0x341178, "fb70fdeb", "0000a0e1"),
        ("formepersist", 0x341274, "bc70fdeb", "0000a0e1"),
        ("formepersist", 0x341280, "746bfdeb", "0000a0e1"),
        ("formepersist", 0x34128C, "d06bfdeb", "0000a0e1"),
        ("formepersist", 0x341378, "7b70fdeb", "0000a0e1"),
        ("formepersist", 0x34141C, "5270fdeb", "0000a0e1"),
        ("formepersist", 0x34153C, "0a70fdeb", "0000a0e1"),
        ("formepersist", 0x3417D0, "656ffdeb", "0000a0e1"),
        ("arceus", 0x3A34DC, "79005003", "0000a0e1"),
        ("arceus", 0x3A3508, "00f020e3", "0f6e02ea"),
        ("arceus", 0x43ED4C, "00000000", "08d04de2"),
        ("arceus", 0x43ED50, "00000000", "0c0094e5"),
        ("arceus", 0x43ED54, "00000000", "6b8efdeb"),
        ("arceus", 0x43ED58, "00000000", "0010a0e1"),
        ("arceus", 0x43ED5C, "00000000", "1e0ea0e3"),
        ("arceus", 0x43ED60, "00000000", "0d0080e2"),
        ("arceus", 0x43ED64, "00000000", "0d20a0e1"),
        ("arceus", 0x43ED68, "00000000", "f378f9eb"),
        ("arceus", 0x43ED6C, "00000000", "08d08de2"),
        ("arceus", 0x43ED70, "00000000", "000050e3"),
        ("arceus", 0x43ED74, "00000000", "7080bd18"),
        ("arceus", 0x43ED78, "00000000", "0c0094e5"),
        ("arceus", 0x43ED7C, "00000000", "4051f0eb"),
        ("arceus", 0x43ED80, "00000000", "7080bde8"),
    ]),
}
KNOWN = {
    ("v1.0", 0x3778C475): "Pokemon X 1.0 (cartridge .code)",
    ("v1.0", 0x46BDAD57): "Pokemon Y 1.0 (cartridge .code)",
    ("v1.5", 0xBA7B14F7): "Pokemon X 1.5 update .code",
    ("v1.5", 0x3E056C1E): "Pokemon Y 1.5 update .code",
}
MAISON_SIZE = 23776
MAISON_SHA1 = "8126e28efa78ad0e76d6fd6d9627cf63be2f6c36"
MAISON_OUT_SHA1 = "bf08110d3e5bdf367a31d9dda7e63ccd660c5d55"
MAISON_HEAD = bytes.fromhex("435241471c000000fffe00040400000038030000e05c00005c0200004f544146")
HEADER_CLEAR = (0x26, 0xC4)
DATA_OFF = 0x338
REC_SIZE = 604
RECORDS = 38
SPECIES_BITS = (10, 101)
SOUL_DEW_BYTE = 294
EXTRA = [(24, 408)]


def crc(b):
    return zlib.crc32(b) & 0xFFFFFFFF


def detect(code):
    for ver, t in CODE.items():
        if len(code) != t["size"]:
            continue
        ok = True
        for _f, off, old, new in t["edits"]:
            cur = code[off:off + 4].hex()
            if cur not in (old, new):
                ok = False
                break
        if ok:
            return ver
    return None


def patch_code(code, features):
    ver = detect(code)
    if ver is None:
        raise SystemExit("[abort] this .code is not Pokemon X/Y 1.0 or 1.5, or a site this tool patches holds unexpected bytes. Nothing was written.")
    out = bytearray(code)
    done = {f: 0 for f in features}
    already = {f: 0 for f in features}
    for f, off, old, new in CODE[ver]["edits"]:
        if f not in features:
            continue
        cur = code[off:off + 4].hex()
        if cur == new:
            already[f] += 1
            continue
        out[off:off + 4] = bytes.fromhex(new)
        done[f] += 1
    return ver, bytes(out), done, already


def ccitt(d):
    c = 0xFFFF
    for x in d:
        c ^= x << 8
        for _ in range(8):
            c = ((c << 1) ^ 0x1021) & 0xFFFF if c & 0x8000 else (c << 1) & 0xFFFF
    return c


def patch_maison(data):
    h = hashlib.sha1(data).hexdigest()
    if h == MAISON_OUT_SHA1:
        return data, 0
    if len(data) != MAISON_SIZE or data[:0x20] != MAISON_HEAD or h != MAISON_SHA1:
        raise SystemExit("[abort] this is not the clean X/Y Battle Maison rule file a/1/8/9 (size %d, SHA-1 %s). Nothing was written." % (len(data), h))
    for r in range(RECORDS):
        s = DATA_OFF + r * REC_SIZE
        if ccitt(data[s:s + REC_SIZE - 2]) != struct.unpack_from("<H", data, s + REC_SIZE - 2)[0]:
            raise SystemExit("[abort] record %d of a/1/8/9 fails its checksum; the file is damaged. Nothing was written." % r)
    out = bytearray(data)
    for j in range(*HEADER_CLEAR):
        out[j] = 0
    for r in range(RECORDS):
        s = DATA_OFF + r * REC_SIZE
        for j in range(*SPECIES_BITS):
            out[s + j] = 0
        out[s + SOUL_DEW_BYTE] = 0
    for r, j in EXTRA:
        out[DATA_OFF + r * REC_SIZE + j] = 0
    n = sum(1 for a, b in zip(data, out) if a != b)
    return bytes(out), n


def main():
    ap = argparse.ArgumentParser(description="Pokemon X / Y Un-Nerf (UNTESTED beta): patches files you extracted from your own decrypted dump. "
                                 "'code' patches the decompressed ExeFS .code (1.0 cartridge or 1.5 update); 'maison' patches the RomFS file a/1/8/9. "
                                 "To patch a whole decrypted .3ds in one go, use complete_unnerf.py instead.")
    ap.add_argument("kind", choices=("code", "maison"))
    ap.add_argument("file", help="the extracted file (it is only read)")
    ap.add_argument("--out", help="output file (default: <file>.unnerf)")
    ap.add_argument("--features", default=",".join(FEATURES), help="code features, comma separated: " + ",".join(FEATURES))
    ap.add_argument("--verify", action="store_true", help="check that the patch applies, write nothing")
    a = ap.parse_args()
    data = open(a.file, "rb").read()
    if a.kind == "code":
        feats = [f.strip() for f in a.features.split(",") if f.strip()]
        bad = [f for f in feats if f not in FEATURES]
        if bad:
            raise SystemExit("unknown feature(s): " + ", ".join(bad))
        ver, out, done, already = patch_code(data, feats)
        name = KNOWN.get((ver, crc(data)), "a %s .code that differs from the clean one outside the patched sites (a hack?)" % ver)
        print("[info] %s, CRC32 %08X" % (name, crc(data)))
        for f in feats:
            print("[code] %-13s %2d words written, %2d already patched" % (f, done[f], already[f]))
    else:
        out, n = patch_maison(data)
        print("[maison] %d bytes cleared (ban list, Soul Dew ban, rule table header as in the OR/AS set)" % n)
    print("[result] CRC32 %08X SHA-1 %s" % (crc(out), hashlib.sha1(out).hexdigest()))
    if a.verify:
        print("[verify] nothing written")
        return
    dst = a.out or a.file + ".unnerf"
    if os.path.exists(dst) and os.path.samefile(dst, a.file):
        raise SystemExit("the output must be a different file from the input")
    open(dst, "wb").write(out)
    print("[done] %s" % dst)


if __name__ == "__main__":
    main()
