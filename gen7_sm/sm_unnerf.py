#!/usr/bin/env python3
import argparse, hashlib, json, os, struct, sys, zlib

FEATURES = ["nbl", "prankster", "galewings", "parentalbond", "souldew", "hyperspacefury", "darkvoid", "formepersist", "resort", "arceus"]
FILES = {"code": "ExeFS code.bin", "battle": "RomFS Battle.cro", "tree": "RomFS a/1/3/7", "text": "RomFS a/0/3/2"}
OUTNAME = {"code": "code.bin", "battle": "Battle.cro", "tree": "a-1-3-7", "text": "a-0-3-2"}
CLEAN = {
    "code": {"Sun 1.0": (5713920, 'DE47EF73', '271b0c77432c339fdd6086475646a9a00586214e'), "Moon 1.0": (5713920, '02948BA5', '90b2a20adcf67a0508f7380381e4f32b2d9255d3'), "Sun 1.2 update": (5722112, '2A28CFAD', 'b53cddb1ef1c87513d70652c7488e254a21d2a30'), "Moon 1.2 update": (5722112, '8E4AE24C', 'ff3a56730c2b5e389ae11849a48ee7691d77e8a5')},
    "battle": {"Sun/Moon 1.0": (1273856, 'D582C507', '233500e8a8a06e785dbc5f9dd80d376d3343eeff'), "Sun/Moon 1.2 update": (1277952, '9EE998A6', 'b3288d341f92bfd08ca8daececc83a4c9cfa5cd1')},
    "tree": {"Sun/Moon 1.0": (29160, 'FCB8926D', '6d0192e7a403fab3658592e9f90cf486ce444730')},
    "text": {"Sun/Moon 1.0": (2342124, '7C6240E6', '40567be024b3e41cae82c6470d5d9643bd1fed5c'), "Sun/Moon 1.2 update": (2342128, 'A04DCEDE', '330aa26009c7f09997426f1107ac73839cac4b81')},
}
SIZES = {"code": {"1.0": 5713920, "1.2": 5722112}, "battle": {"1.0": 1273856, "1.2": 1277952}, "tree": {"1.0": 29160}}
EDITS = {
    "1.0": {
        "nbl": {
            "tree": [
                (0x0006DE, "0101", "0000"),
                (0x00075E, "c0", "00"),
                (0x00076B, "0e", "00"),
                (0x00077B, "c007", "0000"),
                (0x000788, "987e", "0000"),
                (0x00079C, "d803", "0000"),
                (0x0007A5, "f003", "0000"),
                (0x0007AE, "e00107", "000000"),
                (0x0014D6, "0101", "0000"),
                (0x001556, "c0", "00"),
                (0x001563, "0e", "00"),
                (0x001573, "c007", "0000"),
                (0x001580, "987e", "0000"),
                (0x001594, "d803", "0000"),
                (0x00159D, "f003", "0000"),
                (0x0015A6, "e00107", "000000"),
                (0x0022CE, "0101", "0000"),
                (0x00234E, "c0", "00"),
                (0x00235B, "0e", "00"),
                (0x00236B, "c007", "0000"),
                (0x002378, "987e", "0000"),
                (0x00238C, "d803", "0000"),
                (0x002395, "f003", "0000"),
                (0x00239E, "e00107", "000000"),
                (0x0030C6, "0101", "0000"),
                (0x003146, "c0", "00"),
                (0x003153, "0e", "00"),
                (0x003163, "c007", "0000"),
                (0x003170, "987e", "0000"),
                (0x003184, "d803", "0000"),
                (0x00318D, "f003", "0000"),
                (0x003196, "e00107", "000000"),
                (0x003A16, "0101", "0000"),
                (0x003A96, "c0", "00"),
                (0x003AA3, "0e", "00"),
                (0x003AB3, "c007", "0000"),
                (0x003AC0, "987e", "0000"),
                (0x003AD4, "d803", "0000"),
                (0x003ADD, "f003", "0000"),
                (0x003AE6, "e00107", "000000"),
                (0x003EBE, "0101", "0000"),
                (0x003F3E, "c0", "00"),
                (0x003F4B, "0e", "00"),
                (0x003F5B, "c007", "0000"),
                (0x003F68, "987e", "0000"),
                (0x003F7C, "d803", "0000"),
                (0x003F85, "f003", "0000"),
                (0x003F8E, "e00107", "000000"),
                (0x004366, "0101", "0000"),
                (0x0043E6, "c0", "00"),
                (0x0043F3, "0e", "00"),
                (0x004403, "c007", "0000"),
                (0x004410, "987e", "0000"),
                (0x004424, "d803", "0000"),
                (0x00442D, "f003", "0000"),
                (0x004436, "e00107", "000000"),
                (0x00480E, "0101", "0000"),
                (0x00488E, "c0", "00"),
                (0x00489B, "0e", "00"),
                (0x0048AB, "c007", "0000"),
                (0x0048B8, "987e", "0000"),
                (0x0048CC, "d803", "0000"),
                (0x0048D5, "f003", "0000"),
                (0x0048DE, "e00107", "000000"),
                (0x004CB6, "0101", "0000"),
                (0x004D36, "c0", "00"),
                (0x004D43, "0e", "00"),
                (0x004D53, "c007", "0000"),
                (0x004D60, "987e", "0000"),
                (0x004D74, "d803", "0000"),
                (0x004D7D, "f003", "0000"),
                (0x004D86, "e00107", "000000"),
                (0x00515E, "0101", "0000"),
                (0x0051DE, "c0", "00"),
                (0x0051EB, "0e", "00"),
                (0x0051FB, "c007", "0000"),
                (0x005208, "987e", "0000"),
                (0x00521C, "d803", "0000"),
                (0x005225, "f003", "0000"),
                (0x00522E, "e00107", "000000"),
                (0x005606, "0101", "0000"),
                (0x005686, "c0", "00"),
                (0x005693, "0e", "00"),
                (0x0056A3, "c007", "0000"),
                (0x0056B0, "987e", "0000"),
                (0x0056C4, "d803", "0000"),
                (0x0056CD, "f003", "0000"),
                (0x0056D6, "e00107", "000000"),
                (0x005AAE, "0101", "0000"),
                (0x005B2E, "c0", "00"),
                (0x005B3B, "0e", "00"),
                (0x005B4B, "c007", "0000"),
                (0x005B58, "987e", "0000"),
                (0x005B6C, "d803", "0000"),
                (0x005B75, "f003", "0000"),
                (0x005B7E, "e00107", "000000"),
                (0x005F56, "0101", "0000"),
                (0x005FD6, "c0", "00"),
                (0x005FE3, "0e", "00"),
                (0x005FF3, "c007", "0000"),
                (0x006000, "987e", "0000"),
                (0x006014, "d803", "0000"),
                (0x00601D, "f003", "0000"),
                (0x006026, "e00107", "000000"),
                (0x0063FE, "0101", "0000"),
                (0x00647E, "c0", "00"),
                (0x00648B, "0e", "00"),
                (0x00649B, "c007", "0000"),
                (0x0064A8, "987e", "0000"),
                (0x0064BC, "d803", "0000"),
                (0x0064C5, "f003", "0000"),
                (0x0064CE, "e00107", "000000"),
            ],
        },
        "prankster": {
            "battle": [
                (0x024288, "d1ffff0a", "d1ffffea"),
            ],
        },
        "galewings": {
            "battle": [
                (0x0D6208, "0900000a", "00f020e3"),
            ],
        },
        "parentalbond": {
            "battle": [
                (0x024440, "010ba013", "020ba013"),
            ],
        },
        "souldew": {
            "battle": [
                (0x0F7E00, "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", "70402de90150a0e10240a0e10300a0e3fc36feeb040050e10f00001a0410a0e10500a0e18260feebbc00d0e15f0f50e3011c40127d1051121b00001a1e00a0e3f036feeb020050e31700001a7040bde83500a0e354109fe53e24feea0400a0e3e836feeb040050e10f00001a0410a0e10500a0e16e60feebbc00d0e15f0f50e3011c40127d1051120700001a1e00a0e3dc36feeb020050e30300001a7040bde83500a0e308109fe52a24feea7080bde800180000ab0a0000"),
                (0x0B79C0, "70402de9", "0e0101ea"),
            ],
        },
        "hyperspacefury": {
            "battle": [
                (0x0C4250, "1100001a", "110000ea"),
            ],
        },
        "darkvoid": {
            "battle": [
                (0x0C05AC, "0a00001a", "0a0000ea"),
            ],
        },
        "formepersist": {
            "code": [
                (0x330614, "4cadfbeb", "0000a0e1"),
                (0x330DD8, "5babfbeb", "0000a0e1"),
                (0x330E0C, "4eabfbeb", "0000a0e1"),
                (0x330E40, "41abfbeb", "0000a0e1"),
                (0x3310CC, "9eaafbeb", "0000a0e1"),
                (0x3310F8, "93aafbeb", "0000a0e1"),
                (0x331144, "80aafbeb", "0000a0e1"),
                (0x33129C, "2aaafbeb", "0000a0e1"),
                (0x3313FC, "d2a9fbeb", "0000a0e1"),
                (0x3314CC, "9ea9fbeb", "0000a0e1"),
                (0x33156C, "76a9fbeb", "0000a0e1"),
                (0x3315F4, "54a9fbeb", "0000a0e1"),
                (0x3317FC, "d2a8fbeb", "0000a0e1"),
                (0x345838, "c358fbeb", "0000a0e1"),
                (0x331150, "99a3fbeb", "0000a0e1"),
                (0x33115C, "7bb0fbeb", "0000a0e1"),
                (0x3312A8, "43a3fbeb", "0000a0e1"),
                (0x3312B4, "25b0fbeb", "0000a0e1"),
            ],
        },
        "resort": {
            "code": [
                (0x1D9ABC, "220801eb", "00f020e3"),
            ],
        },
        "arceus": {
            "battle": [
                (0x0FD568, "ed010503", "ffffffff"),
            ],
            "code": [
                (0x21B4FC, "0100a0e3", "0400a0e1"),
                (0x393224, "0700001a", "0000a0e1"),
                (0x393228, "0c0094e5", "5c0904ea"),
                (0x393244, "0900000a", "090000ea"),
                (0x393270, "0c0094e5", "520904ea"),
                (0x3932B8, "0700001a", "0000a0e1"),
                (0x3932BC, "0c0094e5", "370904ea"),
                (0x3932D8, "0900000a", "090000ea"),
                (0x393304, "0c0094e5", "2d0904ea"),
                (0x4957A0, "00000000", "0c0094e5"),
                (0x4957A4, "00000000", "dff1fbeb"),
                (0x4957A8, "00000000", "8616f6eb"),
                (0x4957AC, "00000000", "000050e3"),
                (0x4957B0, "00000000", "7080bd18"),
                (0x4957B4, "00000000", "0c0094e5"),
                (0x4957B8, "00000000", "cef1fbeb"),
                (0x4957BC, "00000000", "7080bde8"),
                (0x4957C0, "00000000", "0c0094e5"),
                (0x4957C4, "00000000", "d7f1fbeb"),
                (0x4957C8, "00000000", "a216f6eb"),
                (0x4957CC, "00000000", "000050e3"),
                (0x4957D0, "00000000", "7080bd18"),
                (0x4957D4, "00000000", "0c0094e5"),
                (0x4957D8, "00000000", "c6f1fbeb"),
                (0x4957DC, "00000000", "7080bde8"),
            ],
        },
    },
    "1.2": {
        "prankster": {
            "battle": [
                (0x024288, "d1ffff0a", "d1ffffea"),
            ],
        },
        "galewings": {
            "battle": [
                (0x0D672C, "0900000a", "00f020e3"),
            ],
        },
        "parentalbond": {
            "battle": [
                (0x024440, "010ba013", "020ba013"),
            ],
        },
        "souldew": {
            "battle": [
                (0x0F8400, "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", "70402de90150a0e10240a0e10300a0e37036feeb040050e10f00001a0410a0e10500a0e10960feebbc00d0e15f0f50e3011c40127d1051121b00001a1e00a0e36436feeb020050e31700001a7040bde83500a0e354109fe5ad23feea0400a0e35c36feeb040050e10f00001a0410a0e10500a0e1f55ffeebbc00d0e15f0f50e3011c40127d1051120700001a1e00a0e35036feeb020050e30300001a7040bde83500a0e308109fe59923feea7080bde800180000ab0a0000"),
                (0x0B7EA0, "70402de9", "560101ea"),
            ],
        },
        "hyperspacefury": {
            "battle": [
                (0x0C4730, "1100001a", "110000ea"),
            ],
        },
        "darkvoid": {
            "battle": [
                (0x0C0A8C, "0a00001a", "0a0000ea"),
            ],
        },
        "formepersist": {
            "code": [
                (0x332034, "85a8fbeb", "0000a0e1"),
                (0x332888, "70a6fbeb", "0000a0e1"),
                (0x3328BC, "63a6fbeb", "0000a0e1"),
                (0x3328F0, "56a6fbeb", "0000a0e1"),
                (0x332B7C, "b3a5fbeb", "0000a0e1"),
                (0x332BA8, "a8a5fbeb", "0000a0e1"),
                (0x332BF4, "95a5fbeb", "0000a0e1"),
                (0x332D4C, "3fa5fbeb", "0000a0e1"),
                (0x332EAC, "e7a4fbeb", "0000a0e1"),
                (0x332F7C, "b3a4fbeb", "0000a0e1"),
                (0x33301C, "8ba4fbeb", "0000a0e1"),
                (0x3330A4, "69a4fbeb", "0000a0e1"),
                (0x3332AC, "e7a3fbeb", "0000a0e1"),
                (0x347474, "7553fbeb", "0000a0e1"),
                (0x332C00, "ae9efbeb", "0000a0e1"),
                (0x332C0C, "90abfbeb", "0000a0e1"),
                (0x332D58, "589efbeb", "0000a0e1"),
                (0x332D64, "3aabfbeb", "0000a0e1"),
            ],
        },
        "resort": {
            "code": [
                (0x1D9D30, "460901eb", "00f020e3"),
            ],
        },
        "arceus": {
            "battle": [
                (0x0FE568, "ed010503", "ffffffff"),
            ],
            "code": [
                (0x21BC00, "0100a0e3", "0400a0e1"),
                (0x394EAC, "0700001a", "0000a0e1"),
                (0x394EB0, "0c0094e5", "fa0904ea"),
                (0x394ECC, "0900000a", "090000ea"),
                (0x394EF8, "0c0094e5", "f00904ea"),
                (0x394F40, "0700001a", "0000a0e1"),
                (0x394F44, "0c0094e5", "d50904ea"),
                (0x394F60, "0900000a", "090000ea"),
                (0x394F8C, "0c0094e5", "cb0904ea"),
                (0x4976A0, "00000000", "0c0094e5"),
                (0x4976A4, "00000000", "41f1fbeb"),
                (0x4976A8, "00000000", "8710f6eb"),
                (0x4976AC, "00000000", "000050e3"),
                (0x4976B0, "00000000", "7080bd18"),
                (0x4976B4, "00000000", "0c0094e5"),
                (0x4976B8, "00000000", "30f1fbeb"),
                (0x4976BC, "00000000", "7080bde8"),
                (0x4976C0, "00000000", "0c0094e5"),
                (0x4976C4, "00000000", "39f1fbeb"),
                (0x4976C8, "00000000", "a310f6eb"),
                (0x4976CC, "00000000", "000050e3"),
                (0x4976D0, "00000000", "7080bd18"),
                (0x4976D4, "00000000", "0c0094e5"),
                (0x4976D8, "00000000", "28f1fbeb"),
                (0x4976DC, "00000000", "7080bde8"),
            ],
        },
    },
}
TEXT = {
    "galewings": (97, 177, "Gives priority to Flying-type moves when the \nPok\u00e9mon\u2019s HP is full.", "Gives priority to the Pok\u00e9mon\u2019s\nFlying-type moves."),
    "souldew": (35, 225, "A wondrous orb to be held by either Latios or\nLatias. It raises the power of Psychic- and\nDragon-type moves.", "A wondrous orb to be held by either Latios or\nLatias. It raises its Sp. Atk and\nSp. Def stats."),
}

KEY_BASE = 0x7C89
KEY_ADV = 0x2983


def u16(b, o): return struct.unpack_from("<H", b, o)[0]
def u32(b, o): return struct.unpack_from("<I", b, o)[0]
def crc(b): return "%08X" % (zlib.crc32(b) & 0xFFFFFFFF)
def sha1(b): return hashlib.sha1(b).hexdigest()


class Refused(Exception):
    pass


def garc_subfile(d, index):
    if d[:4] != b"CRAG":
        raise Refused("a/0/3/2 is not a GARC archive")
    data_off = u32(d, 0x10)
    p = u32(d, 4)
    p += u32(d, p + 4)
    if d[p:p + 4] != b"BTAF":
        raise Refused("a/0/3/2 has no FATB block")
    cnt = u32(d, p + 8)
    q = p + 0x0C
    for i in range(cnt):
        fl = u32(d, q)
        q += 4
        subs = []
        for bit in range(32):
            if fl & (1 << bit):
                subs.append((u32(d, q), u32(d, q + 4)))
                q += 12
        if i == index:
            if not subs:
                raise Refused("a/0/3/2 bank %d is empty" % index)
            return data_off + subs[0][0], data_off + subs[0][1]
    raise Refused("a/0/3/2 has no bank %d" % index)


def words_of(s):
    return [ord(ch) for ch in s] + [0]


def line_slot(d, bank, line):
    start, end = garc_subfile(d, bank)
    f = d[start:end]
    lines = u16(f, 2)
    if line >= lines:
        raise Refused("a/0/3/2 bank %d has %d lines, line %d does not exist" % (bank, lines, line))
    secoff = u32(f, 0xC)
    ent = secoff + 4 + line * 8
    off = u32(f, ent)
    ln = u16(f, ent + 4)
    return start, secoff, ent, off, ln


def line_words(d, bank, line):
    start, secoff, ent, off, ln = line_slot(d, bank, line)
    key = (KEY_BASE + line * KEY_ADV) & 0xFFFF
    pos = start + secoff + off
    out = []
    for i in range(ln):
        out.append(u16(d, pos + 2 * i) ^ key)
        key = ((key << 3) | (key >> 13)) & 0xFFFF
    return out


def set_line(d, bank, line, text):
    start, secoff, ent, off, ln = line_slot(d, bank, line)
    w = words_of(text)
    if len(w) > ln:
        raise Refused("the new text for bank %d line %d is longer than the slot" % (bank, line))
    key = (KEY_BASE + line * KEY_ADV) & 0xFFFF
    pos = start + secoff + off
    enc = bytearray()
    for c in w:
        enc += struct.pack("<H", (c ^ key) & 0xFFFF)
        key = ((key << 3) | (key >> 13)) & 0xFFFF
    enc += b"\0" * (2 * (ln - len(w)))
    d[pos:pos + 2 * ln] = enc
    struct.pack_into("<H", d, start + ent + 4, len(w))


def version_of(target, data):
    if target == "text":
        return "any"
    for ver, size in SIZES[target].items():
        if len(data) == size:
            return ver
    raise Refused("%s has %d bytes, which is neither the 1.0 nor the 1.2 file" % (FILES[target], len(data)))


def plan(mode):
    feats = FEATURES if mode == "all" else [mode]
    targets = set()
    texts = []
    for ft in feats:
        for ver in EDITS:
            targets.update(t for t, rows in EDITS[ver].get(ft, {}).items() if rows)
        if ft in TEXT:
            texts.append((ft,) + TEXT[ft])
    if texts:
        targets.add("text")
    return feats, targets, texts


def rows_for(feats, target, ver):
    out = []
    if ver == "any":
        return out
    for ft in feats:
        for off, old, new in EDITS[ver].get(ft, {}).get(target, []):
            out.append((off, bytes.fromhex(old), bytes.fromhex(new), ft))
    return out


def check_file(target, data, quiet):
    want = CLEAN.get(target)
    if want is None:
        return "?"
    for label, (size, c, s) in want.items():
        if len(data) == size and sha1(data) == s:
            return label
    return None


def run(a):
    feats, need, texts = plan(a.mode)
    paths = dict(code=a.code, battle=a.battle, tree=a.tree, text=a.text)
    missing = [FILES[k] for k in sorted(need) if not paths.get(k)]
    if missing:
        raise Refused("mode %s needs: %s" % (a.mode, ", ".join(missing)))
    report = dict(mode=a.mode, features=feats, files={})
    results = {}
    for k in sorted(need):
        src = paths[k]
        data = bytearray(open(src, "rb").read())
        ident = check_file(k, bytes(data), True)
        ver = version_of(k, data)
        rows = rows_for(feats, k, ver)
        done = 0
        for off, old, new, ft in rows:
            cur = bytes(data[off:off + len(old)])
            if cur == new:
                done += 1
                continue
            if cur != old:
                raise Refused("%s (%s): offset 0x%X has %s, expected %s (%s). Is this the clean Sun/Moon %s file? Nothing was written."
                              % (FILES[k], src, off, cur.hex(), old.hex(), ft, ver))
        for off, old, new, ft in rows:
            data[off:off + len(new)] = new
        tdone = []
        if k == "text":
            for ft, bank, line, old_text, new_text in texts:
                cur = line_words(bytes(data), bank, line)
                if cur == words_of(new_text):
                    tdone.append((ft, "already"))
                    continue
                if cur != words_of(old_text):
                    raise Refused("a/0/3/2 bank %d line %d does not hold the expected %s text. Nothing was written." % (bank, line, ft))
                set_line(data, bank, line, new_text)
                tdone.append((ft, "rewritten"))
        results[k] = (src, bytes(data))
        report["files"][FILES[k]] = dict(input=src, input_sha1=sha1(open(src, "rb").read()), input_is=ident or "not a known clean file", version=ver,
                                       sites=len(rows), already=done, text=tdone, output_sha1=sha1(bytes(data)), size=len(data))
    if a.verify:
        print(json.dumps(report, indent=1))
        print("[verify] every site matched; nothing written")
        return report
    for k, (src, data) in results.items():
        if a.inplace:
            dst = src
        else:
            os.makedirs(a.out_dir, exist_ok=True)
            dst = os.path.join(a.out_dir, OUTNAME[k])
            if os.path.exists(dst) and os.path.samefile(dst, src):
                raise Refused("the output %s is the input; use --inplace or another --out-dir" % dst)
        with open(dst + ".part", "wb") as f:
            f.write(data)
        os.replace(dst + ".part", dst)
        report["files"][FILES[k]]["written"] = dst
    print(json.dumps(report, indent=1))
    return report


def main(argv=None):
    ap = argparse.ArgumentParser(description="Un-Nerf Compendium - Pokemon Sun / Moon patcher (1.0 cartridge files and the 1.2 update files). Rewrites your own extracted files (ExeFS code.bin and the RomFS files Battle.cro, a/1/3/7, a/0/3/2). UNTESTED BETA.")
    ap.add_argument("--mode", required=True, choices=FEATURES + ["all"])
    ap.add_argument("--code", help="ExeFS code.bin (decompressed) of Sun or Moon, 1.0 or the 1.2 update")
    ap.add_argument("--battle", help="RomFS Battle.cro")
    ap.add_argument("--tree", help="RomFS a/1/3/7 (Battle Tree rules; the 1.2 update does not replace it)")
    ap.add_argument("--text", help="RomFS a/0/3/2 (English text)")
    ap.add_argument("--out-dir", default="unnerf_out", help="where the patched files go (default: ./unnerf_out)")
    ap.add_argument("--inplace", action="store_true", help="overwrite the input files instead")
    ap.add_argument("--verify", action="store_true", help="check every site, write nothing")
    a = ap.parse_args(argv)
    try:
        run(a)
    except Refused as e:
        print("[abort] %s" % e, file=sys.stderr)
        return 3
    return 0


if __name__ == "__main__":
    sys.exit(main())
