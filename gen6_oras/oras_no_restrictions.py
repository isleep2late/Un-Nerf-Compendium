#!/usr/bin/env python3
r"""
Omega Ruby / Alpha Sapphire -- remove ALL Battle Maison entry restrictions, in one pass.

This single tool lifts everything the Maison enforces:
  * the banned-Pokemon list (legendaries, Soul Dew/item bans)   [RomFS rule GARC]
  * the SPECIES CLAUSE (no duplicate species/formes)            [RomFS rule-flags @GARC 0x26]
  * the ITEM CLAUSE (no duplicate held items)                   [RomFS rule-flags @GARC 0x26]
  * the TEAM-SIZE limit and other entry rules                   [RomFS rule-flags @GARC 0x26]
  * the 510 EV-TOTAL cap (maxed-EV mons become eligible)        [ExeFS .code 0x1E9734]
    (+ the online Battle-Spot EV cap: AS 0x4474B8 / OR 0x4474C0, picked by Title ID)

How: it zeroes the Maison rule-config records in the RomFS rule GARC (which clears the 0xFFFF
rule-flags word that gates the clauses/limits) AND flips the EV-cap literal(s) in the executable
(ExeFS .code). Then it fixes every hash a loader/installer checks: the RomFS IVFC master slot(s),
the NCCH RomFS + ExeFS superblock hashes, the ExeFS .code hash, and -- for a .cia -- the TMD content
hash. Works on a decrypted .cia OR .3ds (NCSD), for both games. Original file untouched; writes a new
*_NoRestrictions file. Pure standard library, self-verifying. (No re-encryption needed -- emulators
and CFW run decrypted content.)

  python oras_no_restrictions.py "Pokemon Alpha Sapphire.3ds"
  python oras_no_restrictions.py game.cia --inplace
  python oras_no_restrictions.py game.cia --verify     # report current values, write nothing
"""
import struct, hashlib, os, shutil, sys, argparse
def u16(d,o): return struct.unpack_from("<H",d,o)[0]
def u32(d,o): return struct.unpack_from("<I",d,o)[0]
def u64(d,o): return struct.unpack_from("<Q",d,o)[0]
def align(x,a): return (x+a-1)&~(a-1)

# ---- EV caps (ExeFS .code, vaddr base 0x100000) ----
OLD,NEW=510,1530; CODE_VBASE=0x100000
ELIG_VADDR=0x1E9734                  # Maison eligibility EV-total cap (identical OR/AS)
BATTLESPOT={0x000400000011C500:0x4474B8, 0x000400000011C400:0x4474C0}  # AS / OR online Battle-Spot cap

# ---- RomFS Maison rule GARC (file "0", CRAG 0x8170) ----
GARC_SIZE=0x8170
HEAD=bytes.fromhex("435241471c000000fffe00040400000064040000708100005c0200004f544146")
ZERO_RUNS=[(38, 39), (44, 44), (48, 48), (52, 52), (56, 56), (60, 60), (64, 64), (68, 68), (72, 72), (76, 76), (80, 80), (84, 84), (88, 88), (92, 92), (96, 96), (100, 100), (105, 105), (108, 109), (112, 113), (116, 117), (120, 121), (124, 125), (128, 129), (132, 133), (136, 137), (140, 141), (144, 145), (148, 149), (152, 153), (156, 157), (160, 161), (164, 165), (169, 169), (172, 173), (176, 177), (180, 181), (184, 185), (188, 189), (192, 193), (196, 197), (200, 201), (204, 205), (208, 209), (212, 213), (216, 217), (220, 221), (224, 225), (228, 229), (233, 233), (236, 237), (240, 241), (244, 245), (248, 249), (252, 255), (4172, 4172), (4185, 4185), (4201, 4202), (4214, 4215), (4234, 4235), (4243, 4244), (4776, 4776), (4789, 4789), (4805, 4806), (4818, 4819), (4838, 4839), (4847, 4848), (5380, 5380), (5393, 5393), (5409, 5410), (5422, 5423), (5442, 5443), (5451, 5452), (5984, 5984), (5997, 5997), (6013, 6014), (6026, 6027), (6046, 6047), (6055, 6056), (6588, 6588), (6601, 6601), (6617, 6618), (6630, 6631), (6650, 6651), (6659, 6660), (12628, 12628), (12641, 12641), (12657, 12658), (12670, 12671), (12690, 12691), (12699, 12700), (12894, 12894), (13232, 13232), (13245, 13245), (13261, 13262), (13274, 13275), (13294, 13295), (13303, 13304), (13498, 13498), (13836, 13836), (13849, 13849), (13865, 13866), (13878, 13879), (13898, 13899), (13907, 13908), (14102, 14102), (14440, 14440), (14453, 14453), (14469, 14470), (14482, 14483), (14502, 14503), (14511, 14512), (14706, 14706), (15044, 15044), (15057, 15057), (15073, 15074), (15086, 15087), (15106, 15107), (15115, 15116), (15310, 15310), (15648, 15648), (15661, 15661), (15677, 15678), (15690, 15691), (15710, 15711), (15719, 15720), (15914, 15914), (16028, 16028), (16252, 16252), (16265, 16265), (16281, 16282), (16294, 16295), (16314, 16315), (16323, 16324), (16518, 16518), (16856, 16856), (16869, 16869), (16885, 16886), (16898, 16899), (16918, 16919), (16927, 16928), (17122, 17122), (17460, 17460), (17473, 17473), (17489, 17490), (17502, 17503), (17522, 17523), (17531, 17532), (17726, 17726), (18064, 18064), (18077, 18077), (18093, 18094), (18106, 18107), (18126, 18127), (18135, 18136), (18330, 18330), (18668, 18668), (18681, 18681), (18697, 18698), (18710, 18711), (18730, 18731), (18739, 18740), (18934, 18934), (19272, 19272), (19285, 19285), (19301, 19302), (19314, 19315), (19334, 19335), (19343, 19344), (19538, 19538), (19876, 19876), (19889, 19889), (19905, 19906), (19918, 19919), (19938, 19939), (19947, 19948), (20142, 20142), (20480, 20480), (20493, 20493), (20509, 20510), (20522, 20523), (20542, 20543), (20551, 20552), (20746, 20746), (21084, 21084), (21097, 21097), (21113, 21114), (21126, 21127), (21146, 21147), (21155, 21156), (21350, 21350), (21688, 21688), (21701, 21701), (21717, 21718), (21730, 21731), (21750, 21751), (21759, 21760), (21954, 21954), (22292, 22292), (22305, 22305), (22321, 22322), (22334, 22335), (22354, 22355), (22363, 22364), (22558, 22558), (22896, 22896), (22909, 22909), (22925, 22926), (22938, 22939), (22958, 22959), (22967, 22968), (23162, 23162), (23500, 23500), (23513, 23513), (23529, 23530), (23542, 23543), (23562, 23563), (23571, 23572), (23766, 23766), (27124, 27124), (27137, 27137), (27153, 27154), (27166, 27167), (27186, 27187), (27195, 27196), (27728, 27728), (27741, 27741), (27757, 27758), (27770, 27771), (27790, 27791), (27799, 27800), (28332, 28332), (28345, 28345), (28361, 28362), (28374, 28375), (28394, 28395), (28403, 28404), (28936, 28936), (28949, 28949), (28965, 28966), (28978, 28979), (28998, 28999), (29007, 29008), (29540, 29540), (29553, 29553), (29569, 29570), (29582, 29583), (29602, 29603), (29611, 29612)]
BLOCK=0x1000; HSZ=0x20; PER=BLOCK//HSZ

def locate(f):
    f.seek(0); sig=f.read(0x200)
    if sig[0x100:0x104]==b"NCSD":
        ncch=u32(sig,0x120)*0x200; is_cia=False; tmd_off=tmd_sz=0
    else:
        f.seek(0); head=f.read(0x2020)
        hs,_t,_v,cert,tik,tmd,meta=struct.unpack_from("<IHHIIII",head,0)
        a=align
        cert_off=a(hs,64); tik_off=a(cert_off+cert,64); tmd_off=a(tik_off+tik,64); ncch=a(tmd_off+tmd,64)
        is_cia=True; tmd_sz=tmd
    f.seek(ncch); h=f.read(0x200)
    if h[0x100:0x104]!=b"NCCH": raise SystemExit("NCCH not found (decrypted .cia/.3ds required).")
    romfs_abs=ncch+u32(h,0x1B0)*0x200
    f.seek(romfs_abs); ivfc=f.read(0x60)
    if ivfc[:4]!=b"IVFC": raise SystemExit("RomFS IVFC not found.")
    master=u32(ivfc,8); l3=romfs_abs+align(0x60+master,0x1000)
    f.seek(l3); hh=struct.unpack("<IIIIIIIIII",f.read(0x28))
    return dict(ncch=ncch,is_cia=is_cia,tmd_off=tmd_off,tmd_sz=tmd_sz,romfs_abs=romfs_abs,
                master=master,l3=l3,fileMetaOff=hh[7],fileMetaSz=hh[8],fileDataOff=hh[9])

def find_garc(f,info):
    f.seek(info["l3"]+info["fileMetaOff"]); meta=f.read(info["fileMetaSz"]); p=0
    while p+0x20<=len(meta):
        doff=u64(meta,p+8); dsz=u64(meta,p+0x10); nlen=u32(meta,p+0x1C)
        nm=meta[p+0x20:p+0x20+nlen].decode("utf-16le","replace")
        if nm=="0" and dsz==GARC_SIZE:
            ab=info["l3"]+info["fileDataOff"]+doff; f.seek(ab)
            if f.read(0x20)==HEAD: return ab
        p+=0x20+align(nlen,4)
    return None

def cms(f,info,k):
    l1=bytearray()
    for jb in range(k*PER,(k+1)*PER):
        f.seek(info["l3"]+jb*PER*BLOCK); data=f.read(PER*BLOCK); l2=bytearray()
        for nb in range(PER):
            blk=data[nb*BLOCK:(nb+1)*BLOCK]
            if len(blk)<BLOCK: blk=blk+b"\x00"*(BLOCK-len(blk))
            l2+=hashlib.sha256(blk).digest()
        l1+=hashlib.sha256(bytes(l2)).digest()
    return hashlib.sha256(bytes(l1)).digest()

def exefs(f,ncch):
    f.seek(ncch); h=f.read(0x200)
    exo=u32(h,0x1A0)*0x200; exhr=u32(h,0x1A8)*0x200; tid=u64(h,0x118)
    f.seek(ncch+exo); eh=bytearray(f.read(0x200)); cfo=cfsz=None
    for i in range(10):
        if eh[i*0x10:i*0x10+8].rstrip(b"\0")==b".code": cfo,cfsz=struct.unpack_from("<II",eh,i*0x10+8)
    return dict(exo=exo,exhr=exhr,eh=eh,code_abs=ncch+exo+0x200+cfo,code_sz=cfsz,tid=tid)

def sha_region(f,off,size):
    h=hashlib.sha256(); f.seek(off); rem=size
    while rem>0:
        b=f.read(min(8<<20,rem))
        if not b: break
        h.update(b); rem-=len(b)
    return h.digest()

def ev_vaddrs(cr):
    vas=[ELIG_VADDR]
    bs=BATTLESPOT.get(cr["tid"])
    if bs is not None: vas.append(bs)
    return vas

def main():
    ap=argparse.ArgumentParser(); ap.add_argument("rom"); ap.add_argument("--out"); ap.add_argument("--inplace",action="store_true")
    ap.add_argument("--verify",action="store_true",help="report current values, write nothing")
    a=ap.parse_args()
    if not os.path.exists(a.rom): raise SystemExit("file not found: "+a.rom)
    if a.verify:
        with open(a.rom,"rb") as f:
            info=locate(f); cr=exefs(f,info["ncch"]); g=find_garc(f,info)
            print(f"  title id = {cr['tid']:016X}  ({'cia' if info['is_cia'] else '3ds'})")
            if g: f.seek(g+0x26); print(f"  rule-flags @GARC 0x26 = 0x{u16(f.read(2),0):04X}  (0=restrictions OFF, FFFF=ON)")
            else: print("  Maison rule GARC not found (already patched?)")
            for va in ev_vaddrs(cr):
                f.seek(cr["code_abs"]+(va-CODE_VBASE)); print(f"  EV literal 0x{va:X} = {u32(f.read(4),0)}")
        return
    out=a.rom if a.inplace else (a.out or os.path.splitext(a.rom)[0]+"_NoRestrictions"+os.path.splitext(a.rom)[1])
    if not a.inplace and out!=a.rom:
        print("[copy] duplicating ROM (%.2f GB)..."%(os.path.getsize(a.rom)/1e9)); shutil.copyfile(a.rom,out)
    with open(out,"r+b") as f:
        info=locate(f); cr=exefs(f,info["ncch"])
        # ---- ExeFS: EV caps ----
        for va in ev_vaddrs(cr):
            pos=cr["code_abs"]+(va-CODE_VBASE); f.seek(pos); cur=u32(f.read(4),0)
            if cur==NEW: print(f"[ev]  0x{va:X}: already {NEW}")
            elif cur==OLD: f.seek(pos); f.write(struct.pack("<I",NEW)); print(f"[ev]  0x{va:X}: {OLD} -> {NEW}")
            else: print(f"[ev]  0x{va:X}: ={cur} (not the EV literal here; SKIP)")
        code_hash=sha_region(f,cr["code_abs"],cr["code_sz"])          # compute THEN write (helpers re-seek!)
        eh=cr["eh"]; eh[0x200-0x20:0x200]=code_hash
        f.seek(info["ncch"]+cr["exo"]); f.write(bytes(eh))
        sup=sha_region(f,info["ncch"]+cr["exo"],cr["exhr"]); f.seek(info["ncch"]+0x1C0); f.write(sup)
        # ---- RomFS: ban list + clauses + team-size (rule-config zeroing) ----
        g=find_garc(f,info)
        if g is None:
            print("[rom] WARNING: Maison rule GARC not found (already patched?). EV cap still applied.")
        else:
            f.seek(g); buf=bytearray(f.read(GARC_SIZE)); nb=0
            for s,e in ZERO_RUNS:
                for j in range(s,e+1):
                    if buf[j]!=0: buf[j]=0; nb+=1
            f.seek(g); f.write(buf)
            slots=sorted({((g-info["l3"])//BLOCK)//PER//PER,((g+GARC_SIZE-1-info["l3"])//BLOCK)//PER//PER})
            for k in slots:
                mh=cms(f,info,k); f.seek(info["romfs_abs"]+0x60+k*HSZ); f.write(mh)   # compute THEN write
            f.flush()
            f.seek(info["romfs_abs"]); ipm=f.read(0x60+info["master"])
            rh=hashlib.sha256(ipm).digest(); f.seek(info["ncch"]+0x1E0); f.write(rh)
            print(f"[rom] zeroed {nb} rule-config bytes (ban+clauses+team size); IVFC slot(s) {slots} + NCCH RomFS hash")
        f.flush()
        # ---- .cia only: rehash content0 for the TMD so it installs ----
        if info["is_cia"]:
            f.seek(info["tmd_off"]); td=bytearray(f.read(info["tmd_sz"])); th=0x140
            base=th+0xC4+64*0x24; c0=u64(struct.pack(">Q",struct.unpack_from(">Q",td,base+0x08)[0]),0)
            c0_size=struct.unpack_from(">Q",td,base+0x08)[0]
            print("[cia] rehashing content0 for the TMD (%.2f GB, one-time)..."%(c0_size/1e9))
            c0_hash=sha_region(f,info["ncch"],c0_size); td[base+0x10:base+0x30]=c0_hash
            ci=th+0xC4; idx_off,cmd_cnt=struct.unpack_from(">HH",td,ci)
            td[ci+0x04:ci+0x24]=hashlib.sha256(bytes(td[base+idx_off*0x30:base+(idx_off+max(cmd_cnt,1))*0x30])).digest()
            td[th+0xA4:th+0xC4]=hashlib.sha256(bytes(td[th+0xC4:th+0xC4+64*0x24])).digest()
            f.seek(info["tmd_off"]); f.write(bytes(td))
        f.flush()
        # ---- verify ----
        g=find_garc(f,info)
        if g: f.seek(g+0x26); rf=u16(f.read(2),0); print(f"[verify] rule-flags @0x26 = 0x{rf:04X} {'OK(off)' if rf==0 else 'STILL ON'}")
        for va in ev_vaddrs(cr):
            f.seek(cr["code_abs"]+(va-CODE_VBASE)); print(f"[verify] EV 0x{va:X} = {u32(f.read(4),0)}")
    print(f"[done] ALL restrictions removed -> {os.path.basename(out)}")
    print("[note] Test in a fresh battle after a clean boot, not a loaded save state.")
    if not info["is_cia"]:
        print("[note] .3ds: File > Load File. Saves are per-Title-ID, so your progress carries over.")
if __name__=="__main__": main()
