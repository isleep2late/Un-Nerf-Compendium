#!/usr/bin/env python3
r"""
The Un-Nerf Compendium -- USUM patcher
======================================
Patches an Ultra Sun / Ultra Moon .cia to remove competitive nerfs and (optionally)
the Battle Tree ban list. Pure Python 3 standard library -- no installs, no tools.

Patches (each works on BOTH Ultra Sun and Ultra Moon):
  * nbl          -- "No Restrictions": unbans the Battle Tree's forbidden Pokemon AND
                    removes the Species Clause (no duplicate species) and Item Clause
                    (no duplicate held items).
  * prankster    -- removes Gen-7 Prankster's "no effect on Dark types" rule.
  * galewings    -- removes Gen-7 Gale Wings' "only at full HP" rule.
  * parentalbond -- restores Gen-6 Parental Bond (2nd hit 0.5x, not the Gen-7 0.25x).
  * souldew      -- restores Gen-6 Soul Dew (+50pct Sp.Atk and Sp.Def for Latios/Latias).
  * all          -- applies all of the above.

Usage:
  python unnerf.py --cia "Pokemon Ultra Moon.cia" --mode all
  python unnerf.py --cia game.cia --mode parentalbond --out game_pb.cia
  python unnerf.py --cia game.cia --mode all --verify     (dry-run, no writes)

It edits a few bytes then recomputes only the IVFC hash-tree slot(s) the edit touches
plus the NCCH RomFS master hash. Signatures/TMD are not enforced by Citra/Azahar/Lime3DS.
"""
from __future__ import annotations
import argparse, hashlib, os, shutil, struct, sys, time

TID_US = "00040000001b5000"
TID_UM = "00040000001b5100"

# ---- Universal Battle.cro byte patches (identical offsets in US and UM) ----
# (offset_in_Battle.cro, expected_original_bytes, patched_bytes, label)
PRANKSTER_PATCH    = (0x00024B14, bytes.fromhex("d1ffff0a"), bytes.fromhex("d1ffffea"), "Prankster (no Dark immunity)")
GALEWINGS_PATCH    = (0x000DA514, bytes.fromhex("0900000a"), bytes.fromhex("00f020e3"), "Gale Wings (priority at any HP)")
PARENTALBOND_PATCH = (0x00024EAC, bytes.fromhex("010ba013"), bytes.fromhex("020ba013"), "Parental Bond (2nd hit 0.5x)")

# ---- Soul Dew: restore Gen-6 +50pct Sp.Atk/Sp.Def for Latios/Latias via code injection ----
# A 184-byte handler is written into the .text->.rodata page-padding code cave, and Soul Dew's
# existing handler entry is repointed to it with one branch. Battle.cro is byte-identical in US
# and UM, so these are the same in both. (offsets within Battle.cro)
SOULDEW_CAVE_OFF  = 0x000FC980
SOULDEW_HANDLER   = bytes.fromhex("70402de90150a0e10240a0e10300a0e3702cfeeb040050e10f00001a0410a0e10500a0e13b56feebbc00d0e15f0f50e3011c40127d1051121b00001a1e00a0e3642cfeeb020050e31700001a7040bde83500a0e354109fe5ca19feea0400a0e35c2cfeeb040050e10f00001a0410a0e10500a0e12756feebbc00d0e15f0f50e3011c40127d1051120700001a1e00a0e3502cfeeb020050e30300001a7040bde83500a0e308109fe5b619feea7080bde800180000ab0a0000")
SOULDEW_ENTRY_OFF = 0x000BBA10
SOULDEW_ENTRY_OLD = bytes.fromhex("70402de9")  # push {r4,r5,r6,lr}
SOULDEW_ENTRY_NEW = bytes.fromhex("da0301ea")  # b <cave>

# ---- Species/Item Clause removal: flags inside the Battle Tree rule table a/1/4/1 ----
# Each of the 14 facility rule records carries Species Clause @ +0x0E and Item Clause @ +0x0F
# (01 = on). These FILE-relative offsets are identical in Ultra Sun and Ultra Moon.
# a/1/4/1 is located dynamically (file "1", 30372 bytes, GARC). Each byte: 01 -> 00.
A141_NAME = "1"; A141_SIZE = 30372
CLAUSE_REL_OFFSETS = [
    0x006F2,0x006F3, 0x014EA,0x014EB, 0x022E2,0x022E3, 0x030DA,0x030DB,
    0x03A2A,0x03A2B, 0x03ED2,0x03ED3, 0x0437A,0x0437B, 0x04822,0x04823,
    0x04CCA,0x04CCB, 0x05172,0x05173, 0x0561A,0x0561B, 0x05AC2,0x05AC3,
    0x05F6A,0x05F6B, 0x06412,0x06413,
]

# ---- In-game description text edits (per-mode), in the English message archive a/0/3/2 ----
# (identical file in US and UM). Located dynamically by name "2" + size. Requires gametext.py.
A032_NAME = "2"; A032_SIZE = 2793100
GALEWINGS_DESC = (102, 177, "Gives priority to the Pokémon’s\nFlying-type moves.")
SOULDEW_DESC   = (39, 225, "A wondrous orb to be held by either Latios or\nLatias. It raises its Sp. Atk and\nSp. Def stats.")

# ---- No-Ban-List edits: (absolute_cia_offset, original_bytes_hex). New = zeros. ----
NBL_TABLE = {
TID_UM: [
 (0x92129C32,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x92129C70,"d80300000000000000f00300000000000000e00187"),
 (0x9212AA2A,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212AA68,"d80300000000000000f00300000000000000e00187"),
 (0x9212B822,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212B860,"d80300000000000000f00300000000000000e00187"),
 (0x9212C61A,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212C658,"d80300000000000000f00300000000000000e00187"),
 (0x9212DD62,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212DDA0,"d80300000000000000f00300000000000000e00187"),
 (0x9212E20A,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212E248,"d80300000000000000f00300000000000000e00187"),
 (0x9212E6B2,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212E6F0,"d80300000000000000f00300000000000000e00187"),
 (0x9212EB5A,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212EB98,"d80300000000000000f00300000000000000e00187"),
 (0x9212F002,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212F040,"d80300000000000000f00300000000000000e00187"),
 (0x9212F4AA,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212F4E8,"d80300000000000000f00300000000000000e00187"),
 (0x9212F952,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212F990,"d80300000000000000f00300000000000000e00187"),
],
TID_US: [
 (0x921260A2,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x921260E0,"d80300000000000000f00300000000000000e00187"),
 (0x92126E9A,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x92126ED8,"d80300000000000000f00300000000000000e00187"),
 (0x92127C92,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x92127CD0,"d80300000000000000f00300000000000000e00187"),
 (0x92128A8A,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x92128AC8,"d80300000000000000f00300000000000000e00187"),
 (0x9212A1D2,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212A210,"d80300000000000000f00300000000000000e00187"),
 (0x9212A67A,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212A6B8,"d80300000000000000f00300000000000000e00187"),
 (0x9212AB22,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212AB60,"d80300000000000000f00300000000000000e00187"),
 (0x9212AFCA,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212B008,"d80300000000000000f00300000000000000e00187"),
 (0x9212B472,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212B4B0,"d80300000000000000f00300000000000000e00187"),
 (0x9212B91A,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212B958,"d80300000000000000f00300000000000000e00187"),
 (0x9212BDC2,"c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e"),(0x9212BE00,"d80300000000000000f00300000000000000e00187"),
],
}

BLOCK = 0x1000; HSZ = 0x20; PER = BLOCK // HSZ

def align(x,a): return (x+a-1)&~(a-1)
def u32le(b,o): return struct.unpack_from("<I",b,o)[0]
def u64le(b,o): return struct.unpack_from("<Q",b,o)[0]

def parse(f):
    head=f.read(0x2020)
    _hs,_t,_v,cert,tk,tmd,meta=struct.unpack_from("<IHHIIII",head,0)
    o_cert=align(0x2020,64); o_tk=align(o_cert+cert,64); o_tmd=align(o_tk+tk,64); o_content=align(o_tmd+tmd,64)
    f.seek(o_content); ncch=f.read(0x200)
    if ncch[0x100:0x104]!=b"NCCH": raise SystemExit("Not a valid CIA/NCCH (is this an Ultra Sun/Moon .cia?)")
    tid=ncch[0x108:0x110][::-1].hex()
    romfs_abs=o_content+u32le(ncch,0x1B0)*0x200
    f.seek(romfs_abs); ivfc=f.read(0x60)
    if ivfc[:4]!=b"IVFC": raise SystemExit("RomFS IVFC header not found.")
    master_size=u32le(ivfc,0x08)
    l3_abs=romfs_abs+align(0x60+master_size,0x1000)
    f.seek(l3_abs); l3h=f.read(0x28)
    _,_,_,_,_,_,_,fmoff,fmlen,fdoff=struct.unpack("<IIIIIIIIII",l3h)
    f.seek(l3_abs+fmoff); fm=f.read(fmlen); off=0; bcro=None; a141=None; a032=None
    while off+0x20<=len(fm):
        e=fm[off:off+0x20]; doff=u64le(e,8); dsz=u64le(e,0x10); nlen=u32le(e,0x1C)
        nm=fm[off+0x20:off+0x20+nlen].decode("utf-16le","replace")
        if nm=="Battle.cro": bcro=l3_abs+fdoff+doff
        elif nm==A141_NAME and dsz==A141_SIZE: a141=l3_abs+fdoff+doff
        elif nm==A032_NAME and dsz==A032_SIZE: a032=l3_abs+fdoff+doff
        off+=0x20+align(nlen,4)
    return dict(content_off=o_content, tid=tid, romfs_abs=romfs_abs, master_size=master_size,
                l3_abs=l3_abs, battle_abs=bcro, a141_abs=a141, a032_abs=a032)

def master_slot_for(info, cia_off):
    return ((cia_off - info["l3_abs"])//BLOCK)//PER//PER

def recompute_master_slot(f, info, k):
    l1_block=bytearray()
    for jb in range(k*PER,(k+1)*PER):
        f.seek(info["l3_abs"]+jb*PER*BLOCK); data=f.read(PER*BLOCK)
        l2=bytearray()
        for n in range(PER):
            blk=data[n*BLOCK:(n+1)*BLOCK]
            if len(blk)<BLOCK: blk=blk+b"\x00"*(BLOCK-len(blk))
            l2+=hashlib.sha256(blk).digest()
        l1_block+=hashlib.sha256(bytes(l2)).digest()
    return hashlib.sha256(bytes(l1_block)).digest()

def build_edits(info, mode):
    """Return list of (cia_offset, expected_old_bytes, new_bytes, label)."""
    edits=[]
    if mode in ("nbl","all"):
        tab=NBL_TABLE.get(info["tid"])
        if not tab: raise SystemExit(f"No NBL table for title {info['tid']} (only US/UM supported).")
        for off,oldhex in tab:
            old=bytes.fromhex(oldhex); edits.append((off,old,b"\x00"*len(old),"No-Ban-List record"))
        # Species Clause + Item Clause removal (flags in the Battle Tree rule table a/1/4/1)
        if info.get("a141_abs") is None:
            raise SystemExit("Battle Tree rule table a/1/4/1 not found -- cannot remove clauses.")
        for rel in CLAUSE_REL_OFFSETS:
            edits.append((info["a141_abs"]+rel, b"\x01", b"\x00", "Species/Item Clause flag"))
    if info["battle_abs"] is None and mode in ("prankster","galewings","parentalbond","souldew","all"):
        raise SystemExit("Battle.cro not found in RomFS -- cannot apply ability patches.")
    if mode in ("prankster","all"):
        o,old,new,lbl=PRANKSTER_PATCH; edits.append((info["battle_abs"]+o,old,new,lbl))
    if mode in ("galewings","all"):
        o,old,new,lbl=GALEWINGS_PATCH; edits.append((info["battle_abs"]+o,old,new,lbl))
    if mode in ("parentalbond","all"):
        o,old,new,lbl=PARENTALBOND_PATCH; edits.append((info["battle_abs"]+o,old,new,lbl))
    if mode in ("souldew","all"):
        ba=info["battle_abs"]
        edits.append((ba+SOULDEW_CAVE_OFF, b"\x00"*len(SOULDEW_HANDLER), SOULDEW_HANDLER, "Soul Dew handler (code injection)"))
        edits.append((ba+SOULDEW_ENTRY_OFF, SOULDEW_ENTRY_OLD, SOULDEW_ENTRY_NEW, "Soul Dew repoint"))
    return edits

def apply_descriptions(f, info, mode, slots):
    """Rewrite the in-game ability/item descriptions in a/0/3/2 (same-size repack)."""
    specs=[]
    if mode in ("galewings","all"): specs.append(GALEWINGS_DESC)
    if mode in ("souldew","all"):   specs.append(SOULDEW_DESC)
    if not specs: return
    if info.get("a032_abs") is None:
        raise SystemExit("Text archive a/0/3/2 not found -- cannot edit in-game descriptions.")
    try:
        import gametext
    except Exception:
        raise SystemExit("gametext.py is required for description edits -- keep it next to unnerf.py.")
    f.seek(info["a032_abs"]); cur=f.read(A032_SIZE)
    subs=gametext.garc_subfiles(cur); by_bank={}
    for bank,line,text in specs: by_bank.setdefault(bank,{})[line]=text
    repl={b:gametext.edit_textfile(subs[b],lns) for b,lns in by_bank.items()}
    newg=gametext.garc_repack(cur,repl,pad_to=A032_SIZE)
    if newg==cur:
        print("[desc] in-game descriptions already current."); return
    f.seek(info["a032_abs"]); f.write(newg)
    a=info["a032_abs"]
    for k in range(master_slot_for(info,a), master_slot_for(info,a+A032_SIZE-1)+1): slots.add(k)
    print(f"[desc] rewrote {len(specs)} in-game description(s).")

def run(cia, out, mode, verify, inplace):
    t0=time.time()
    target = cia if (verify or inplace) else out
    if not verify and not inplace:
        if os.path.exists(out) and os.path.getsize(out)==os.path.getsize(cia):
            print(f"[copy] '{os.path.basename(out)}' already exists with matching size -- reusing it.")
        else:
            print(f"[copy] {os.path.basename(cia)} -> {os.path.basename(out)} ({os.path.getsize(cia)/1e9:.2f} GB)...")
            shutil.copyfile(cia,out)
    with open(cia,"rb") as f: info=parse(f)
    title={TID_US:"Ultra Sun",TID_UM:"Ultra Moon"}.get(info["tid"],"UNKNOWN ("+info["tid"]+")")
    print(f"[info] Detected: Pokemon {title}   Battle.cro @0x{(info['battle_abs'] or 0):X}")
    edits=build_edits(info, mode)
    with open(target,"r+b") as f:
        already=0
        for off,old,new,lbl in edits:
            f.seek(off); cur=f.read(len(old))
            if cur==new: already+=1; continue
            if cur!=old:
                raise SystemExit(f"[abort] Unexpected bytes at 0x{off:X} ({lbl}).\n"
                                 f"        got {cur.hex()}, expected {old.hex()}.\n"
                                 f"        Is this an unmodified US/UM .cia? Nothing was written.")
        print(f"[check] {len(edits)} edit site(s) verified ({already} already patched).")
        if mode in ("galewings","souldew","all") and info.get("a032_abs") is None:
            raise SystemExit("Text archive a/0/3/2 not found -- cannot edit in-game descriptions.")
        if verify:
            print("[verify] dry-run OK -- no changes written."); return
        slots=set()
        for off,old,new,lbl in edits:
            f.seek(off); f.write(new)
            for k in range(master_slot_for(info,off), master_slot_for(info,off+len(new)-1)+1):
                slots.add(k)
        apply_descriptions(f, info, mode, slots)
        f.flush()
        print(f"[patch] applied {len(edits)} edit(s); {len(slots)} IVFC slot(s) to refresh: {sorted(slots)}")
        for k in sorted(slots):
            h=recompute_master_slot(f,info,k)
            f.seek(info["romfs_abs"]+0x60+k*HSZ); f.write(h)
        f.flush()
        f.seek(info["romfs_abs"]); ivfc_plus=f.read(0x60+info["master_size"])
        f.seek(info["content_off"]+0x1E0); f.write(hashlib.sha256(ivfc_plus).digest())
        f.flush()
    print(f"[done] {mode.upper()} patch complete in {time.time()-t0:.1f}s -> {os.path.basename(target)}")
    print("[note] Re-install this .cia in your emulator and test in a FRESH battle (not a save state).")

def main():
    ap=argparse.ArgumentParser(description="The Un-Nerf Compendium - USUM patcher")
    ap.add_argument("--cia",required=True,help="path to the Ultra Sun/Moon .cia")
    ap.add_argument("--mode",required=True,choices=["nbl","prankster","galewings","parentalbond","souldew","all"])
    ap.add_argument("--out",help="output .cia (default: <name>_<mode>.cia next to input)")
    ap.add_argument("--verify",action="store_true",help="dry-run: verify only, no writes")
    ap.add_argument("--inplace",action="store_true",help="patch the input file directly (no copy)")
    a=ap.parse_args()
    if not os.path.exists(a.cia): raise SystemExit(f"File not found: {a.cia}")
    out=a.out or os.path.splitext(a.cia)[0]+f"_{a.mode}.cia"
    run(a.cia,out,a.mode,a.verify,a.inplace)

if __name__=="__main__": main()
