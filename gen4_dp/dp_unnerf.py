#!/usr/bin/env python3
import argparse, hashlib, os, struct

FEATURES = ["banlist", "clauses", "levelcap", "souldew", "arceus"]

GAMES = {
    'ADAE': {
        "name": 'diamond',
        "clean_sha1": 'a46233d8b79a69ea87aa295a0efad5237d02841e',
        "clean_name": 'Pokemon - Diamond Version (USA) (Rev 5).nds',
        "sites": [
            ('banlist', 'ban-1', None, 0x0206A964, 4, '35c99044c73c97dbbde273394f883932eec32fe6', '00207047'),
            ('banlist', 'ban-2', 6, 0x02244276, 2, '46b92094a02de73cdedc91d6157436567cb3fdf3', '12e0'),
            ('clauses', 'clause-1', None, 0x0207174C, 4, 'b5501a053606407b7fb7fafba194250c5245076c', '00207047'),
            ('clauses', 'clause-2', 6, 0x02243780, 4, '9a025fc03a116b9781a903eed5492eab897e543b', '00207047'),
            ('clauses', 'clause-3', None, 0x0206BD36, 2, 'a829a1c9e041600f10047d1e85aa9b3c30573244', '1be0'),
            ('clauses', 'clause-4', None, 0x0206BD7A, 2, '89488f59ac5323b3fc1ff2a81a3ec4c94019db04', '21e0'),
            ('clauses', 'clause-5', 6, 0x02243184, 4, 'fe9c4b87a46934cfa90b5d51e4e8b8af7dcad062', '01207047'),
            ('levelcap', 'level-1', None, 0x0206BD20, 2, '769e644c81df6de59920f13c7d1a645e25ecf217', '04e0'),
            ('levelcap', 'level-2', None, 0x0206BBB8, 2, 'd1bfb0a076bcd77eff2726acb392002aff453f68', '01e0'),
            ('souldew', 'souldew-1', 11, 0x0224B684, 2, '70dc1ff617c85adda9ed030fad3e9cef4ea2c7ff', 'c046'),
            ('souldew', 'souldew-2', 11, 0x0224B6B2, 2, '70dc1ff617c85adda9ed030fad3e9cef4ea2c7ff', 'c046'),
            ('arceus', 'arceus-1', None, 0x020677E6, 20, '3fd681e4a9d6e7543c4323096ac2f82c11a76522', '307e0006c40e15e0c046c046c046c046c046c046'),
            ('arceus', 'arceus-2', None, 0x02069F08, 8, 'adc4c5357b10e9997d1e991d77c7c5b91fb652e8', '00f00cf8002805d0'),
            ('arceus', 'arceus-3', 11, 0x0224B1F0, 8, 'c8d56b96ae4a648aff75692d66d79691f7092450', '1ef698fe002815d0'),
            ('arceus', 'arceus-4', 11, 0x0224C792, 114, 'ff2bee2b9dfdb02c4838064160cf82c198eb4147', '1df6c7fb041c35d1c02070432818b52189012631405cc006c40e2be0c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046'),
        ],
    },
    'APAE': {
        "name": 'pearl',
        "clean_sha1": '99083bf15ec7c6b81b4ba241ee10abd9e80999ac',
        "clean_name": 'Pokemon - Pearl Version (USA) (Rev 5).nds',
        "sites": [
            ('banlist', 'ban-1', None, 0x0206A964, 4, '35c99044c73c97dbbde273394f883932eec32fe6', '00207047'),
            ('banlist', 'ban-2', 6, 0x02244282, 2, '46b92094a02de73cdedc91d6157436567cb3fdf3', '12e0'),
            ('clauses', 'clause-1', None, 0x0207174C, 4, 'b5501a053606407b7fb7fafba194250c5245076c', '00207047'),
            ('clauses', 'clause-2', 6, 0x0224378C, 4, '9a025fc03a116b9781a903eed5492eab897e543b', '00207047'),
            ('clauses', 'clause-3', None, 0x0206BD36, 2, 'a829a1c9e041600f10047d1e85aa9b3c30573244', '1be0'),
            ('clauses', 'clause-4', None, 0x0206BD7A, 2, '89488f59ac5323b3fc1ff2a81a3ec4c94019db04', '21e0'),
            ('clauses', 'clause-5', 6, 0x02243190, 4, 'fe9c4b87a46934cfa90b5d51e4e8b8af7dcad062', '01207047'),
            ('levelcap', 'level-1', None, 0x0206BD20, 2, '769e644c81df6de59920f13c7d1a645e25ecf217', '04e0'),
            ('levelcap', 'level-2', None, 0x0206BBB8, 2, 'd1bfb0a076bcd77eff2726acb392002aff453f68', '01e0'),
            ('souldew', 'souldew-1', 11, 0x0224B684, 2, '70dc1ff617c85adda9ed030fad3e9cef4ea2c7ff', 'c046'),
            ('souldew', 'souldew-2', 11, 0x0224B6B2, 2, '70dc1ff617c85adda9ed030fad3e9cef4ea2c7ff', 'c046'),
            ('arceus', 'arceus-1', None, 0x020677E6, 20, '3fd681e4a9d6e7543c4323096ac2f82c11a76522', '307e0006c40e15e0c046c046c046c046c046c046'),
            ('arceus', 'arceus-2', None, 0x02069F08, 8, 'adc4c5357b10e9997d1e991d77c7c5b91fb652e8', '00f00cf8002805d0'),
            ('arceus', 'arceus-3', 11, 0x0224B1F0, 8, 'c8d56b96ae4a648aff75692d66d79691f7092450', '1ef698fe002815d0'),
            ('arceus', 'arceus-4', 11, 0x0224C792, 114, 'ff2bee2b9dfdb02c4838064160cf82c198eb4147', '1df6c7fb041c35d1c02070432818b52189012631405cc006c40e2be0c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046'),
        ],
    },
}

def u32(d, o):
    return struct.unpack_from("<I", d, o)[0]


def region(d, ov):
    if ov is None:
        return u32(d, 0x28), u32(d, 0x20), u32(d, 0x2C)
    fat, ovt, ovn = u32(d, 0x48), u32(d, 0x50), u32(d, 0x54) // 32
    for i in range(ovn):
        oid, ram, rsz, bss, s0, s1, fid, fl = struct.unpack_from("<8I", d, ovt + 32 * i)
        if oid == ov:
            if fl & 0x01000000:
                raise SystemExit("overlay %d is compressed; this is not a retail dump" % ov)
            st, en = struct.unpack_from("<2I", d, fat + 8 * fid)
            return ram, st, en - st
    raise SystemExit("overlay %d not found" % ov)


def offset(d, ov, addr, size):
    ram, off, sz = region(d, ov)
    if not (ram <= addr and addr + size <= ram + sz):
        raise SystemExit("address %08X is outside its code region" % addr)
    return off + addr - ram


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("rom")
    ap.add_argument("--out")
    ap.add_argument("--features", default=",".join(FEATURES))
    ap.add_argument("--list", action="store_true")
    a = ap.parse_args()
    want = [f.strip() for f in a.features.split(",") if f.strip()]
    for f in want:
        if f not in FEATURES:
            raise SystemExit("unknown feature %r; choose from %s" % (f, ", ".join(FEATURES)))
    d = bytearray(open(a.rom, "rb").read())
    code = bytes(d[0x0C:0x10]).decode("ascii", "replace")
    if code not in GAMES:
        raise SystemExit("game code %r is not Diamond (ADAE) or Pearl (APAE) USA" % code)
    g = GAMES[code]
    if a.list:
        for s in g["sites"]:
            print("%-9s %-10s %-5s %08X %3d bytes" % (s[0], s[1], "arm9" if s[2] is None else "ov%d" % s[2], s[3], s[4]))
        return
    if d[0x1E] != 5:
        raise SystemExit("ROM revision is %d; this patch needs Rev 5" % d[0x1E])
    sha = hashlib.sha1(d).hexdigest()
    if sha != g["clean_sha1"]:
        print("note: input SHA-1 %s is not the clean %s (%s); every site is still checked" % (sha, g["clean_name"], g["clean_sha1"]))
    todo = []
    for feat, sid, ov, addr, size, osha, new in g["sites"]:
        if feat not in want:
            continue
        o = offset(d, ov, addr, size)
        cur = bytes(d[o:o + size])
        nb = bytes.fromhex(new)
        if cur == nb:
            raise SystemExit("site %s at %08X is already patched" % (sid, addr))
        if hashlib.sha1(cur).hexdigest() != osha:
            raise SystemExit("site %s at %08X does not hold the expected original bytes; nothing written" % (sid, addr))
        todo.append((sid, o, nb))
    for sid, o, nb in todo:
        d[o:o + len(nb)] = nb
    out = a.out or os.path.splitext(a.rom)[0] + " [Un-Nerf UNTESTED].nds"
    if os.path.abspath(out) == os.path.abspath(a.rom):
        raise SystemExit("refusing to overwrite the input")
    open(out, "wb").write(d)
    print("%s: %d sites written (%s)" % (g["name"], len(todo), ", ".join(want)))
    print("output %s" % out)
    print("sha1   %s" % hashlib.sha1(d).hexdigest())


if __name__ == "__main__":
    main()
