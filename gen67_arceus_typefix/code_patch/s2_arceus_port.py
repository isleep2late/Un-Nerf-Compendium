#!/usr/bin/env python3
"""Config-driven Arceus/Silvally form-typing + persistence patch (works for US & UM; same scheme
ports to ORAS with an ORAS config). Edits:
  - form persistence: persist site  mov r0,#1 -> mov r0,r4
  - gate removal: each getter's Multitype/RKS gate -> route Arceus/Silvally to the handler for ANY ability
  - plate-or-form handler in the .text code cave, hooked into both getters' Arceus & Silvally branches
Relative offsets inside GetType1/GetType2 are identical across US/UM (verified by signature)."""
import struct
BASE=0x100000

# Per-game addresses (derived by signature matching).
UM={"name":"UltraMoon","cave":0x5B99F8,"gethelditem":0x4ADFA4,"getformno":0x4ADF74,
    "plate":0x3233A4,"mem":0x323434,"gt1":0x4AF288,"gt2":0x4AF31C,"persist":0x3236D8}
US={"name":"UltraSun", "cave":0x5B99F0,"gethelditem":0x4ADF9C,"getformno":0x4ADF6C,
    "plate":0x3233A4,"mem":0x323434,"gt1":0x4AF280,"gt2":0x4AF314,"persist":0x3236D8}

# relative offsets inside a GetType function (from its push)
OFF_GATE_BNE=0x30   # bne (not-Multitype) -> nop
OFF_GATE_BEQ=0x50   # beq (RKS) -> b (always to Silvally branch)
OFF_ARC_HOOK=0x34   # Arceus branch  ldr r0,[r4,#0xc]  -> b arceus_cave
OFF_SIL_HOOK=0x7C   # Silvally branch ldr r0,[r4,#0xc] -> b silvally_cave

def bl(frm,to): return 0xEB000000|(((to-(frm+8))>>2)&0xFFFFFF)
def b (frm,to): return 0xEA000000|(((to-(frm+8))>>2)&0xFFFFFF)

def cave_words(addr,cfg,table):
    return [0xE594000C, bl(addr+0x04,cfg["gethelditem"]), bl(addr+0x08,table),
            0xE3500000, 0x18BD8070, 0xE594000C, bl(addr+0x18,cfg["getformno"]), 0xE8BD8070]

def edits(cfg):
    ac=cfg["cave"]; sc=cfg["cave"]+0x20
    e={cfg["persist"]:0xE1A00004}
    for g in (cfg["gt1"],cfg["gt2"]):
        e[g+OFF_GATE_BNE]=0xE1A00000           # bne -> nop  (Arceus: any ability -> handler)
        e[g+OFF_GATE_BEQ]=0xEA000009           # beq -> b    (Silvally: any ability -> handler)
        e[g+OFF_ARC_HOOK]=b(g+OFF_ARC_HOOK,ac)
        e[g+OFF_SIL_HOOK]=b(g+OFF_SIL_HOOK,sc)
    for i,w in enumerate(cave_words(ac,cfg,cfg["plate"])): e[ac+i*4]=w
    for i,w in enumerate(cave_words(sc,cfg,cfg["mem"])):   e[sc+i*4]=w
    return e

def apply(code,cfg):
    code=bytearray(code)
    # validate originals
    assert struct.unpack_from("<I",code,cfg["persist"]-BASE)[0]==0xE3A00001,"persist not mov r0,#1"
    for g in (cfg["gt1"],cfg["gt2"]):
        assert struct.unpack_from("<I",code,(g+OFF_ARC_HOOK)-BASE)[0]==0xE594000C,"arc hook bad @0x%X"%(g+OFF_ARC_HOOK)
        assert struct.unpack_from("<I",code,(g+OFF_SIL_HOOK)-BASE)[0]==0xE594000C,"sil hook bad @0x%X"%(g+OFF_SIL_HOOK)
        assert (struct.unpack_from("<I",code,(g+OFF_GATE_BNE)-BASE)[0]>>24)==0x1A,"gate bne bad @0x%X"%(g+OFF_GATE_BNE)
        assert (struct.unpack_from("<I",code,(g+OFF_GATE_BEQ)-BASE)[0]>>24)==0x0A,"gate beq bad @0x%X"%(g+OFF_GATE_BEQ)
    for off in range(cfg["cave"],cfg["cave"]+0x40,4):
        assert struct.unpack_from("<I",code,off-BASE)[0]==0,"cave not zero @0x%X"%off
    for va,w in edits(cfg).items(): struct.pack_into("<I",code,va-BASE,w)
    return bytes(code)

if __name__=="__main__":
    import sys
    # sanity: confirm this reproduces the proven UltraMoon edits from s2_arceus_final
    sys.path.insert(0,"Arceus_Unnerf_Output")
    import s2_arceus_final as AF
    a=AF.all_edits(); b2=edits(UM)
    same=(a==b2)
    print("UM edits match s2_arceus_final:",same)
    if not same:
        print(" only in final:",{hex(k):hex(v) for k,v in a.items() if a.get(k)!=b2.get(k)})
        print(" only in port :",{hex(k):hex(v) for k,v in b2.items() if a.get(k)!=b2.get(k)})
