import argparse
import hashlib
import os
import struct
import sys

FRP_SHA1 = "9fe37a855782265c8567d5d19aebe6a8ebae9bd6"
OUT_SHA1 = "688f7b1c9e01fee19f2f719e4b2045afb23a28e7"
FEATURES = ["banlist", "clauses", "levelcap", "rules", "souldew", "arceus", "formes", "abilitylock"]
NEEDS = {"abilitylock": "clauses"}

SITES = [
    ("ban-1", "banlist", None, 0x02078804, "35c99044c73c97dbbde273394f883932eec32fe6", "00207047", "Pokemon_IsOnBattleFrontierBanlist"),
    ("ban-2", "banlist", None, 0x0204AF50, "46b92094a02de73cdedc91d6157436567cb3fdf3", "12e0", "BattleFrontier_MakeSeenBanlistSpeciesMsg"),
    ("clause-1", "clauses", None, 0x020808CC, "b5501a053606407b7fb7fafba194250c5245076c", "00207047", "CheckDuplicateValues"),
    ("clause-2", "clauses", None, 0x02080968, "f83d1fc9ab70df706d8361e3198f5b39ac36fb3e", "00207047", "CheckUniqueValues"),
    ("clause-3", "clauses", None, 0x020809DC, "f83d1fc9ab70df706d8361e3198f5b39ac36fb3e", "00207047", "CheckEqualityInArray"),
    ("clause-4", "clauses", None, 0x0204A410, "9a025fc03a116b9781a903eed5492eab897e543b", "00207047", "BattleTower_CheckDuplicateSpeciesAndHeldItems"),
    ("clause-5", "clauses", None, 0x0207A478, "76be44df55e62fe356b68e453702019cf57a9dc6", "20e0", "BattleRegulation_ValidatePartySelection"),
    ("clause-6", "clauses", None, 0x0207A4BC, "76be44df55e62fe356b68e453702019cf57a9dc6", "26e0", "BattleRegulation_ValidatePartySelection"),
    ("level-1", "levelcap", None, 0x0207A460, "76be44df55e62fe356b68e453702019cf57a9dc6", "0ae0", "BattleRegulation_ValidatePartySelection"),
    ("level-2", "levelcap", None, 0x0207A2EE, "82cffd02d824ac471c53d18ddd2a6d1d9a7cef77", "0ce0", "BattleRegulation_ValidatePokemon"),
    ("rules-1", "rules", None, 0x0207A31C, "0b9b6439bb1b9200ad56dd27ca14c28bb84b422f", "0120f8bd", "BattleRegulation_ValidatePokemon"),
    ("souldew-1", "souldew", 16, 0x0225A5F0, "ef79e5f73405df5d99bab45d163a90d0a326f694", "0021", "BattleSystem_CalcMoveDamage"),
    ("souldew-2", "souldew", 16, 0x0225A61E, "ef79e5f73405df5d99bab45d163a90d0a326f694", "0021", "BattleSystem_CalcMoveDamage"),
    ("arceus-1", "arceus", None, 0x02074AF2, "b3c359abf8fc70a907e8afb75ea8114605ce984c", "307e0006c40e15e0c046c046c046c046c046c046", "BoxPokemon_GetDataInternal"),
    ("arceus-2", "arceus", None, 0x0207796C, "adc4c5357b10e9997d1e991d77c7c5b91fb652e8", "00f00cf8002805d0", "BoxPokemon_SetArceusForm"),
    ("arceus-3", "arceus", 16, 0x02259F6A, "1bf040f9e1c1a234533be19479ebc112764d939f", "1df60dfd002818d0", "BattleSystem_TriggerFormChange"),
    ("arceus-4", "arceus", 16, 0x0225B786, "f836a3c0f4a1b8e7c21627f5f67c7c5c16708211", "1cf6fff8041c35d1c020704329180248085cc006c40e2de0c046662d0000c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046c046", "Battler_MonType"),
    ("forme-1", "formes", 16, 0x02259FDE, "fd52d5ed263687768bd86d3a3a5d65296df5eac2", "0fe0", "BattleSystem_TriggerFormChange"),
    ("forme-2", "formes", 5, 0x021F6DC2, "f712bbd514484e7f71d3b19dbcd25bc8c48fd589", "c046", "ScrCmd_TryRevertPokemonForm"),
    ("forme-3", "formes", 5, 0x021F6C9A, "4a8169c65703d6f796a55a3a7f4192ce2a8678eb", "6be0", "ScrCmd_TryRevertPartyPokemonForms"),
    ("forme-4", "formes", None, 0x02077A3C, "f712bbd514484e7f71d3b19dbcd25bc8c48fd589", "0dd1", "BoxPokemon_SetGiratinaForm"),
    ("forme-5", "formes", None, 0x02077BC0, "26e32161b9c4620aa289110550e2f614af78efee", "05e0", "Party_SetShayminLandForm"),
    ("forme-6", "formes", None, 0x020798B4, "18de89e06b6b22faf7c8200273b19a27eebf110d", "c046c046", "PCBoxes_TryStoreBoxMonInBox"),
    ("forme-7", "formes", None, 0x02079928, "8958ffd53131a586a8ae9a96b74843e1d4849206", "c046c046", "PCBoxes_TryStoreBoxMonAt"),
    ("forme-8", "formes", 88, 0x0223B9A8, "38bd1dea7acaada3af7cb1cc06db58e781621625", "c046c046", "ov88_0223B914"),
    ("forme-9", "formes", 94, 0x022425C4, "3533e9ebba1b73f2e3b4dd001bd3c545ff639609", "c046c046", "ov94_022425A8"),
    ("forme-10", "formes", 94, 0x022425E4, "31c840323be0233f8058393c6acb3361470469e8", "c046c046", "ov94_022425A8"),
    ("ability-0", "abilitylock", None, 0x020808D0, "d2152aa4f080da4b566170030a51da51bc32c2fc", "f8b5041c05210022f3f74afe051c201c70210022f3f744fe071c201c0a210022f3f73efe041c281c391c1822f5f766f8061c281c391c1922f5f760f8b44203d0002803d0844201d10120f8bd0020f8bd70b5041c151cfff7d3ff061c201c70212a1cf4f795f9002e02d0201cf7f782fb70bd70b5041c151cfff7c2ff061c201c70212a1cf4f7ecf8002e02d0201cf7f76dfb70bd", "CheckDuplicateValues"),
    ("ability-1", "abilitylock", None, 0x02077A4C, "7aafd7c1cca3882b6e12f20bc202dfa0741a811b", "08f068ffc046c046c046", "BoxPokemon_SetGiratinaForm"),
    ("ability-2", "abilitylock", None, 0x02077A82, "d6fef0b733256538b161800da4179c91365cc3b8", "08f04dffc046c046c046", "Pokemon_SetGiratinaOriginForm"),
    ("ability-3", "abilitylock", None, 0x02077B02, "c22be2e9600aadd8b1ea9065ef7b53e32766242b", "08f00dffc046c046c046", "BoxPokemon_SetShayminForm"),
    ("ability-4", "abilitylock", None, 0x02077D02, "c4c7fa59c5378d1fb8860349f8b6b4b47ca1b954", "08f01efec046c046c046", "Pokemon_SetRotomForm"),
]


def region_offset(rom, overlay, addr, length):
    arm9_off, arm9_entry, arm9_ram, arm9_size = struct.unpack_from("<IIII", rom, 0x20)
    fat_off, fat_size = struct.unpack_from("<II", rom, 0x48)
    ov9_off, ov9_size = struct.unpack_from("<II", rom, 0x50)
    if overlay is None:
        start, size, base = arm9_off, arm9_size, arm9_ram
    else:
        if overlay * 32 >= ov9_size:
            raise ValueError("overlay %d missing" % overlay)
        e = struct.unpack_from("<8I", rom, ov9_off + overlay * 32)
        if e[0] != overlay:
            raise ValueError("overlay table entry %d is not overlay %d" % (overlay, e[0]))
        if e[7] & 0x01000000:
            raise ValueError("overlay %d is compressed" % overlay)
        fs, fe = struct.unpack_from("<II", rom, fat_off + e[6] * 8)
        start, size, base = fs, fe - fs, e[1]
    off = addr - base
    if off < 0 or off + length > size:
        raise ValueError("site 0x%08X outside its region" % addr)
    fo = start + off
    if fo < 0x8000:
        raise ValueError("site 0x%08X falls in the header or secure area" % addr)
    return fo


def main():
    ap = argparse.ArgumentParser(description="Un-Nerf (UNTESTED beta) for Pokemon Following Renegade Platinum v2.0")
    ap.add_argument("rom", nargs="?")
    ap.add_argument("--out")
    ap.add_argument("--features", default=",".join(FEATURES))
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--allow-other-sha1", action="store_true")
    a = ap.parse_args()
    if a.list:
        for sid, feat, ov, addr, old, new, func in SITES:
            where = "arm9" if ov is None else "overlay %d" % ov
            print("%-10s %-9s %-11s 0x%08X %3d bytes  %s" % (sid, feat, where, addr, len(new) // 2, func))
        return 0
    if not a.rom:
        ap.error("give the ROM to patch")
    feats = [f.strip() for f in a.features.split(",") if f.strip()]
    bad = [f for f in feats if f not in FEATURES]
    if bad:
        ap.error("unknown feature(s): " + ",".join(bad))
    for f, need in NEEDS.items():
        if f in feats and need not in feats:
            ap.error("%s needs %s (its code lives in space that %s frees)" % (f, need, need))
    rom = bytearray(open(a.rom, "rb").read())
    sha1 = hashlib.sha1(rom).hexdigest()
    if rom[0x0C:0x10] != b"CPUE":
        print("not a US Platinum-based ROM (game code %r)" % bytes(rom[0x0C:0x10]))
        return 1
    if sha1 != FRP_SHA1 and not a.allow_other_sha1:
        if sha1 == OUT_SHA1:
            print("this ROM is already patched")
        else:
            print("SHA-1 %s is not Following Renegade Platinum v2.0 (%s); pass --allow-other-sha1 to try anyway" % (sha1, FRP_SHA1))
        return 1
    plan = []
    for sid, feat, ov, addr, old, new, func in SITES:
        if feat not in feats:
            continue
        new_b = bytes.fromhex(new)
        fo = region_offset(rom, ov, addr, len(new_b))
        cur = bytes(rom[fo:fo + len(new_b)])
        if cur == new_b:
            print("%s already patched; refusing a partly patched ROM" % sid)
            return 1
        if hashlib.sha1(cur).hexdigest() != old:
            print("%s (%s at 0x%08X): bytes differ from what this patch expects; nothing written" % (sid, func, addr))
            return 1
        plan.append((sid, fo, new_b))
    for sid, fo, new_b in plan:
        rom[fo:fo + len(new_b)] = new_b
    out = a.out
    if not out:
        stem, ext = os.path.splitext(a.rom)
        out = stem + " [Un-Nerf UNTESTED]" + (ext or ".nds")
    if os.path.abspath(out) == os.path.abspath(a.rom):
        print("refusing to overwrite the input")
        return 1
    with open(out, "wb") as f:
        f.write(rom)
    print("wrote %s (%d sites, SHA-1 %s)" % (out, len(plan), hashlib.sha1(rom).hexdigest()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
