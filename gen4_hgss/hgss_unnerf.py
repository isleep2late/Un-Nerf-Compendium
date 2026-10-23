#!/usr/bin/env python3
import argparse, hashlib, os, struct, sys

try:
    import ndspy.codeCompression as cc
    import ndspy.fnt
except ImportError:
    cc = None

NDSPY = "4.2.0"
FEATURES = ["banlist", "clauses", "souldew", "arceus", "formes"]

GAMES = {
    'IPKE': {
        "name": 'heartgold',
        "clean_sha1": '4fcded0e2713dc03929845de631d0932ea2b5a37',
        "out_sha1": '93f950590eff7bb492c10853d87998b1d668399e',
        "clean_name": 'Pokemon - HeartGold Version (USA).nds',
    },
    'IPGE': {
        "name": 'soulsilver',
        "clean_sha1": 'f8dc38ea20c17541a43b58c5e6d18c1732c7e582',
        "out_sha1": '587ecbb100f4e92528d3aa6e3a4b2f4ac7ded8f0',
        "clean_name": 'Pokemon - SoulSilver Version (USA).nds',
    },
}

NARC = ('a/0/0/1', 293, '57f7ba4d5d06172a044576d5648c20909f139fff')

SITES = [
    ('ban-1', 'banlist', 'arm9', 0x0204A62E, '0488fc5555724e248c9de079da1f1efb3681c9c3', '0020c046', 'PartyIsValidForFrontier'),
    ('ban-2', 'banlist', 'arm9', 0x0204F0F6, '99b83dc82644c3a5cf740ed3e85e318b6a83cb9b', '0020c046', 'BattleHall_DoesPartyContainEligibleMons'),
    ('ban-3', 'banlist', 'arm9', 0x0207B3AC, 'ea08005de040b131e13be16d5df93d5af5014ab9', '0020c046', 'sub_0207B364'),
    ('ban-4', 'banlist', 'arm9', 0x0207B438, '415ee706a7bd6177a5bf4e13ae1dbab20aa25797', '0020c046', 'sub_0207B418'),
    ('ban-5', 'banlist', 'arm9', 0x0207B4C0, '0776e518140dbc3e9346ee5ee3d2327a67bb0220', '0020c046', 'sub_0207B4A0'),
    ('clause-1', 'clauses', 'arm9', 0x0204A63A, 'b1516ffc1f685933d1d5bf8130fc7e5a8a8dd5c1', '07e0', 'PartyIsValidForFrontier'),
    ('clause-2', 'clauses', 'arm9', 0x0204A684, '0a7c5218b7a0897b51f9f7f28e25c48c38390f42', '0120c046', 'PartyIsValidForFrontier'),
    ('clause-3', 'clauses', 'arm9', 0x0204AB10, '9a025fc03a116b9781a903eed5492eab897e543b', '00207047', 'FrontierFieldSystem_PartyHasDuplicateSpeciesOrItems'),
    ('clause-4', 'clauses', 'arm9', 0x0207BA78, 'b5501a053606407b7fb7fafba194250c5245076c', '00207047', 'sub_0207BA78'),
    ('clause-5', 'clauses', 'arm9', 0x0207BB88, 'f83d1fc9ab70df706d8361e3198f5b39ac36fb3e', '00207047', 'sub_0207BB88'),
    ('souldew-1', 'souldew', 'ov12', 0x0225736C, '70dc1ff617c85adda9ed030fad3e9cef4ea2c7ff', 'c046', 'CalcMoveDamage'),
    ('souldew-2', 'souldew', 'ov12', 0x0225739A, '70dc1ff617c85adda9ed030fad3e9cef4ea2c7ff', 'c046', 'CalcMoveDamage'),
    ('arceus-1', 'arceus', 'arm9', 0x02071C28, '9beab5b858d60cf7b18815ac82d87c4631a8471d', '00227e380f2804d8944603a2105c6246704710007047c0460a0b0d0c0f010304020e060507101108' + '00' * 78, 'GetArceusTypeByHeldItemEffect'),
    ('arceus-2', 'arceus', 'arm9', 0x02071C00, '33fe0c0eb72d00c0b2da25a0e6d533e92e1121e7', '0121002206f0c0f8ff2200f00ef8ff2806d00090281c702100aafdf7a9f8c046', 'BoxMon_UpdateArceusForm'),
    ('arceus-3', 'arceus', 'arm9', 0x0206EBF0, 'eccaa11b774d3077694ab0390317b61455f9b104', '327ed20803f019f8', 'GetBoxMonDataInternal'),
    ('arceus-4', 'arceus', 'ov12', 0x02256CE2, 'e33b39528f0c485f2f5b13f9a5ca237f3a2949e5', '462189006958c0234b43f15cca06d20e1af69aff904210d0c046c046c046', 'Battler_CheckWeatherFormChange'),
    ('arceus-5', 'arceus', 'ov12', 0x02258570, 'c11d5ebec1b9a1c9c3327aa72fa8a90b6bcaac28', 'c046', 'Battler_GetType'),
    ('forme-1', 'formes', 'arm9', 0x02071CDC, 'f712bbd514484e7f71d3b19dbcd25bc8c48fd589', '0dd1', 'BoxMon_UpdateGiratinaForm'),
    ('forme-2', 'formes', 'arm9', 0x02071D92, '72eb4550881842fe15cc0caa852eb5b16c3e3e09', '0398002809d0c046c046', 'BoxMon_UpdateShayminForm'),
    ('forme-3', 'formes', 'ov12', 0x02256D76, '0a493543ed2c9330548e26865cbd5029a954b928', 'c046', 'Battler_CheckWeatherFormChange'),
    ('forme-4', 'formes', 'arm9', 0x0204D040, '1f7806ba3775ae35d38748686ebf7738c5c5321c', '1de0', 'ScrCmd_DaycareSanitizeMon'),
    ('forme-5', 'formes', 'ov1', 0x022013D4, '1f7806ba3775ae35d38748686ebf7738c5c5321c', '1de0', 'ScrCmd_CommSanitizeParty'),
    ('forme-6', 'formes', 'narc', 0x00000000, '163d4250c6bd6d162a7aedcd4eb4a4d378ed0d3a', 'e0000000', 'battle subscript CheckShayminForm'),
]


def u32(d, o):
    return struct.unpack_from("<I", d, o)[0]


def sha1(b):
    return hashlib.sha1(b).hexdigest()


def overlay_entry(d, oid):
    ovt, ovn = u32(d, 0x50), u32(d, 0x54) // 32
    if oid >= ovn:
        raise SystemExit("overlay %d not found" % oid)
    e = ovt + 32 * oid
    if u32(d, e) != oid:
        raise SystemExit("overlay table entry %d is not overlay %d" % (oid, u32(d, e)))
    return e


def file_range(d, fid):
    return struct.unpack_from("<2I", d, u32(d, 0x48) + 8 * fid)


def unblz(packed):
    n = len(packed)
    a, extra = struct.unpack_from("<II", packed, n - 8)
    buf = bytearray(packed) + bytearray(extra)
    top = n - (a & 0xFFFFFF)
    src = n - (a >> 24)
    dst = n + extra
    while src > top:
        src -= 1
        flags = buf[src]
        for _ in range(8):
            if flags & 0x80:
                b1, b2 = buf[src - 1], buf[src - 2]
                src -= 2
                disp = (((b1 << 8) | b2) & 0xFFF) + 2
                for _ in range((b1 >> 4) + 3):
                    dst -= 1
                    buf[dst] = buf[dst + 1 + disp]
            else:
                src -= 1
                dst -= 1
                buf[dst] = buf[src]
            flags = (flags << 1) & 0xFF
            if src <= top:
                break
    if dst != top:
        return None
    return bytes(buf)


def pad_to(comp, size):
    k = size - len(comp)
    if k < 0:
        return None
    if k == 0:
        return comp
    a, extra = struct.unpack_from("<II", comp, len(comp) - 8)
    hl, rl = a >> 24, a & 0xFFFFFF
    if hl + k > 0xFF or extra < k:
        return None
    return comp[:-8] + b"\xff" * k + struct.pack("<II", ((hl + k) << 24) | (rl + k), extra - k)


def unpack(packed, what):
    data = cc.decompress(packed)
    if unblz(packed) != data:
        raise SystemExit("%s: ndspy and in-place decompression disagree; this is not a retail dump" % what)
    return bytearray(data)


def check_packed(packed, data, what):
    if cc.decompress(packed) != data:
        raise SystemExit("%s: recompressed code does not decompress to the edited code; nothing written" % what)
    if unblz(packed) != data:
        raise SystemExit("%s: recompressed code does not survive in-place decompression; nothing written" % what)


def load(d, reg):
    if reg == 'arm9':
        off, size = u32(d, 0x20), u32(d, 0x2C)
        packed = bytes(d[off:off + size])
        return {"ram": u32(d, 0x28), "off": off, "packed": packed, "data": unpack(packed, 'arm9')}
    oid = int(reg[2:])
    e = overlay_entry(d, oid)
    ram, rsz, fid, cw = u32(d, e + 4), u32(d, e + 8), u32(d, e + 0x18), u32(d, e + 0x1C)
    if not cw & 0x01000000:
        raise SystemExit("overlay %d is not compressed; this is not a retail dump" % oid)
    s, en = file_range(d, fid)
    if (cw & 0xFFFFFF) != en - s:
        raise SystemExit("overlay %d: table size %X, FAT size %X" % (oid, cw & 0xFFFFFF, en - s))
    packed = bytes(d[s:en])
    data = unpack(packed, 'overlay %d' % oid)
    if len(data) != rsz:
        raise SystemExit("overlay %d decompresses to %X bytes, table says %X" % (oid, len(data), rsz))
    return {"ram": ram, "oid": oid, "entry": e, "fid": fid, "cw": cw, "off": s, "end": en, "packed": packed, "data": data}


def narc_member(d):
    path, idx = NARC[0], NARC[1]
    fnt, fntsz = u32(d, 0x40), u32(d, 0x44)
    fid = ndspy.fnt.load(bytes(d[fnt:fnt + fntsz])).idOf(path)
    if fid is None:
        raise SystemExit("%s not found" % path)
    s, e = file_range(d, fid)
    n = bytes(d[s:e])
    if n[:4] != b"NARC":
        raise SystemExit("%s is not a NARC" % path)
    hl = struct.unpack_from("<H", n, 0x0C)[0]
    if n[hl:hl + 4] != b"BTAF" or struct.unpack_from("<H", n, hl + 8)[0] <= idx:
        raise SystemExit("%s has no member %d" % (path, idx))
    ms, me = struct.unpack_from("<2I", n, hl + 12 + 8 * idx)
    bt = hl + u32(n, hl + 4)
    gm = bt + u32(n, bt + 4)
    if n[bt:bt + 4] != b"BTNF" or n[gm:gm + 4] != b"GMIF":
        raise SystemExit("%s: unexpected NARC layout" % path)
    return s + gm + 8 + ms, me - ms


def repack(d, reg, m, starts):
    data = bytes(m["data"])
    old = m["packed"]
    if reg == 'arm9':
        comp = cc.compress(data, True)
        new = pad_to(comp, len(old))
        if new is None:
            raise SystemExit("arm9 recompressed to %X bytes, more than the retail %X; this feature set does not fit in place; nothing written" % (len(comp), len(old)))
        if new[:0x4000] != old[:0x4000]:
            raise SystemExit("arm9: first 0x4000 bytes changed; nothing written")
        check_packed(new, data, 'arm9')
        d[m["off"]:m["off"] + len(new)] = new
        return "arm9: recompressed %X bytes, padded to the retail %X" % (len(comp), len(old))
    oid, s, en = m["oid"], m["off"], m["end"]
    comp = cc.compress(data, False)
    new = pad_to(comp, len(old))
    if new is not None:
        check_packed(new, data, 'overlay %d' % oid)
        d[s:en] = new
        return "overlay %d: recompressed %X bytes, padded to the retail %X" % (oid, len(comp), len(old))
    nxt = min(x for x in starts if x > s)
    if s + len(comp) > nxt:
        raise SystemExit("overlay %d recompressed to %X bytes and does not fit before the next file at %X; nothing written" % (oid, len(comp), nxt))
    if d[en:s + len(comp)] != b"\xff" * max(0, s + len(comp) - en):
        raise SystemExit("overlay %d: the space after it is not free padding; nothing written" % oid)
    check_packed(comp, data, 'overlay %d' % oid)
    d[s:s + len(comp)] = comp
    if s + len(comp) < en:
        d[s + len(comp):en] = b"\xff" * (en - s - len(comp))
    struct.pack_into("<I", d, m["entry"] + 0x1C, (m["cw"] & 0xFF000000) | len(comp))
    struct.pack_into("<I", d, u32(d, 0x48) + 8 * m["fid"] + 4, s + len(comp))
    return "overlay %d: recompressed %X bytes (retail %X) at the same offset %X, FAT end and overlay table size updated" % (oid, len(comp), len(old), s)


def where(reg, addr):
    if reg == 'narc':
        return "%s[%d]+%X" % (NARC[0], NARC[1], addr)
    return "%08X" % addr


def ndspy_version():
    try:
        from importlib.metadata import version
        return version("ndspy")
    except Exception:
        return "unknown"


def main():
    ap = argparse.ArgumentParser(description="Un-Nerf (UNTESTED beta) for Pokemon HeartGold / SoulSilver (USA)")
    ap.add_argument("rom", nargs="?")
    ap.add_argument("--out")
    ap.add_argument("--features", default=",".join(FEATURES))
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--allow-other-sha1", action="store_true")
    a = ap.parse_args()
    if a.list:
        for sid, feat, reg, addr, osha, new, func in SITES:
            print("%-9s %-8s %-5s %-9s %3d bytes  %s" % (sid, feat, reg, where(reg, addr), len(new) // 2, func))
        return 0
    if not a.rom:
        ap.error("give the ROM to patch")
    want = [f.strip() for f in a.features.split(",") if f.strip()]
    if not want:
        ap.error("no feature chosen; choose from %s" % ", ".join(FEATURES))
    for f in want:
        if f not in FEATURES:
            ap.error("unknown feature %r; choose from %s" % (f, ", ".join(FEATURES)))
    if cc is None:
        print("this script needs ndspy %s: pip install ndspy==%s" % (NDSPY, NDSPY))
        return 1
    out = a.out or os.path.splitext(a.rom)[0] + " [Un-Nerf UNTESTED].nds"
    if os.path.abspath(out) == os.path.abspath(a.rom):
        print("refusing to overwrite the input")
        return 1
    ver = ndspy_version()
    if ver != NDSPY:
        print("note: ndspy %s is installed; the published output SHA-1 was made with ndspy %s" % (ver, NDSPY))
    src = bytes(open(a.rom, "rb").read())
    d = bytearray(src)
    code = bytes(d[0x0C:0x10]).decode("ascii", "replace")
    if code not in GAMES:
        print("game code %r is not HeartGold (IPKE) or SoulSilver (IPGE) USA" % code)
        return 1
    g = GAMES[code]
    if d[0x1E] != 0:
        print("ROM revision is %d; this patch needs revision 0" % d[0x1E])
        return 1
    sha = sha1(src)
    if sha == g["out_sha1"]:
        print("this ROM is already patched")
        return 1
    if sha != g["clean_sha1"] and not a.allow_other_sha1:
        print("SHA-1 %s is not the clean %s (%s); pass --allow-other-sha1 to try anyway" % (sha, g["clean_name"], g["clean_sha1"]))
        return 1
    mods = {}
    plan = []
    for sid, feat, reg, addr, osha, new, func in SITES:
        if feat not in want:
            continue
        nb = bytes.fromhex(new)
        if reg == 'narc':
            base, size = narc_member(d)
            if addr + len(nb) > size:
                raise SystemExit("site %s outside its file" % sid)
            cur = bytes(d[base + addr:base + addr + len(nb)])
        else:
            if reg not in mods:
                mods[reg] = load(d, reg)
            m = mods[reg]
            o = addr - m["ram"]
            if o < 0 or o + len(nb) > len(m["data"]):
                raise SystemExit("address %08X is outside its code region" % addr)
            cur = bytes(m["data"][o:o + len(nb)])
        if cur == nb:
            print("site %s at %s is already patched; refusing a partly patched ROM" % (sid, where(reg, addr)))
            return 1
        if sha1(cur) != osha:
            print("site %s (%s at %s) does not hold the expected original bytes; nothing written" % (sid, func, where(reg, addr)))
            return 1
        if reg == 'narc' and sha1(bytes(d[base:base + size])) != NARC[2]:
            print("%s member %d is not the retail one; nothing written" % (NARC[0], NARC[1]))
            return 1
        plan.append((sid, reg, addr, nb, base + addr if reg == 'narc' else None))
    for sid, reg, addr, nb, fo in plan:
        if reg == 'narc':
            d[fo:fo + len(nb)] = nb
        else:
            m = mods[reg]
            o = addr - m["ram"]
            m["data"][o:o + len(nb)] = nb
    fat, fatsz = u32(src, 0x48), u32(src, 0x4C)
    starts = sorted(s for s, e in (struct.unpack_from("<2I", src, fat + 8 * i) for i in range(fatsz // 8)) if e > s)
    for reg in mods:
        print(repack(d, reg, mods[reg], starts))
    if d[:0x200] != src[:0x200]:
        raise SystemExit("header changed; nothing written")
    with open(out, "wb") as f:
        f.write(d)
    outsha = sha1(d)
    print("%s: %d sites written (%s)" % (g["name"], len(plan), ", ".join(want)))
    print("output %s" % out)
    print("sha1   %s" % outsha)
    if sha == g["clean_sha1"] and set(want) == set(FEATURES):
        if outsha == g["out_sha1"]:
            print("matches the Un-Nerf UNTESTED build")
        else:
            print("note: differs from the Un-Nerf UNTESTED build %s (ndspy %s)" % (g["out_sha1"], ver))
    return 0


if __name__ == "__main__":
    sys.exit(main())
