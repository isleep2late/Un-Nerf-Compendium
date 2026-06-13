#!/usr/bin/env python3
"""ORAS (gen6) Arceus form-typing + persistence. No Silvally (gen7). Edits code.bin in the .3ds:
  - persist: 0x3B3EB8  mov r0,#1 -> nop  (ItemToForm returns form-as-flag: 0=no plate => no reset)
  - gate removal: NOP the cmpeq r0,#0x79 in each getter so ANY-ability Arceus reaches the handler
  - plate-or-form handler in the .text code cave, reusing ItemToForm:
        item=GetHeldItem(); chg,form = ItemToForm(Arceus,item); return form if chg else GetFormNo()
Hooks both getters' Arceus branches to it. Unicorn-verified. (.3ds: ExeFS rehash, no TMD.)"""
import sys, struct, hashlib, shutil
sys.path.insert(0,"Arceus_Unnerf_Output/Un-Nerf-Compendium/Un-Nerf-Compendium-main/gen67_formepersist")
sys.path.insert(0,"Arceus_Unnerf_Output")
import formepersist as fp
from s2_lib import get_code
BASE=0x100000

# --- per-game addresses (derived/verified by disassembly) ---
AS={"name":"AlphaSapphire","cave":0x5795FC,"gethelditem":0x4D244C,"getformno":0x1532A8,
    "itemtoform":0x3B3D98,"persist":0x3B3EB8,
    "gt1_gate":0x3B3B6C,"gt1_hook":0x3B3B98,"gt2_gate":0x4D3220,"gt2_hook":0x4D324C}
OR={"name":"OmegaRuby","cave":0x579604,"gethelditem":0x4D2454,"getformno":0x1532A8,
    "itemtoform":0x3B3D98,"persist":0x3B3EB8,
    "gt1_gate":0x3B3B6C,"gt1_hook":0x3B3B98,"gt2_gate":0x4D3228,"gt2_hook":0x4D3254}

def bl(frm,to): return 0xEB000000|(((to-(frm+8))>>2)&0xFFFFFF)
def b (frm,to): return 0xEA000000|(((to-(frm+8))>>2)&0xFFFFFF)

def cave_words(c,cfg):
    # item=GetHeldItem(); f=ItemToForm(493,item,&scratch);  (persist-nop => r0=form, 0 if no plate)
    # return f if f!=0 (plate held => Multitype type) else GetFormNo() (pkhex form's type).
    # 493 loaded as 0x1E0+0xD (ARM11=ARMv6, no movw).
    return [0xE24DD008,                       # sub sp,sp,#8   scratch
            0xE594000C,                       # ldr r0,[r4,#0xc]  data
            bl(c+0x08,cfg["gethelditem"]),    # bl GetHeldItem -> r0=item
            0xE1A01000,                       # mov r1,r0      item
            0xE3A00E1E,                       # mov r0,#0x1E0
            0xE280000D,                       # add r0,r0,#0xD => 493 (Arceus)
            0xE1A0200D,                       # mov r2,sp      &scratch
            bl(c+0x1C,cfg["itemtoform"]),     # bl ItemToForm -> r0=form (0 if no plate)
            0xE28DD008,                       # add sp,sp,#8
            0xE3500000,                       # cmp r0,#0
            0x18BD8070,                       # popne {r4,r5,r6,pc}  plate type
            0xE594000C,                       # ldr r0,[r4,#0xc]
            bl(c+0x30,cfg["getformno"]),      # bl GetFormNo -> r0=form
            0xE8BD8070]                       # pop {r4,r5,r6,pc}  form's type

def edits(cfg):
    c=cfg["cave"]; e={cfg["persist"]:0xE1A00000,
                      cfg["gt1_gate"]:0xE1A00000, cfg["gt2_gate"]:0xE1A00000,
                      cfg["gt1_hook"]:b(cfg["gt1_hook"],c), cfg["gt2_hook"]:b(cfg["gt2_hook"],c)}
    for i,w in enumerate(cave_words(c,cfg)): e[c+i*4]=w
    return e

def apply(code,cfg):
    code=bytearray(code)
    assert struct.unpack_from("<I",code,cfg["persist"]-BASE)[0]==0xE3A00001,"persist not mov r0,#1"
    for g in (cfg["gt1_gate"],cfg["gt2_gate"]):
        assert struct.unpack_from("<I",code,g-BASE)[0]==0x03500079,"gate not cmpeq r0,#0x79 @0x%X (%08X)"%(g,struct.unpack_from("<I",code,g-BASE)[0])
    for h in (cfg["gt1_hook"],cfg["gt2_hook"]):
        assert struct.unpack_from("<I",code,h-BASE)[0]==0xE320F000,"hook not nop @0x%X"%h
    for off in range(cfg["cave"],cfg["cave"]+0x40,4):
        assert struct.unpack_from("<I",code,off-BASE)[0]==0,"cave not zero @0x%X"%off
    for va,w in edits(cfg).items(): struct.pack_into("<I",code,va-BASE,w)
    return bytes(code)

def verify(patched,cfg):
    from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE,UcError
    from unicorn.arm_const import UC_ARM_REG_R0,UC_ARM_REG_R4,UC_ARM_REG_SP,UC_ARM_REG_LR,UC_ARM_REG_PC
    T=["Normal","Fighting","Flying","Poison","Ground","Rock","Bug","Ghost","Steel","Fire","Water","Grass","Electric","Psychic","Ice","Dragon","Dark","Fairy"]
    csz=(len(patched)+0xFFF)&~0xFFF
    def cave(item,form):
        mu=Uc(UC_ARCH_ARM,UC_MODE_ARM); mu.mem_map(BASE,csz); mu.mem_write(BASE,bytes(patched))
        mu.mem_map(0x900000,0x40000); mu.reg_write(UC_ARM_REG_SP,0x930000); mu.reg_write(UC_ARM_REG_R4,0x920000)
        mu.reg_write(UC_ARM_REG_LR,0xDEAD0000)
        def h(u,a,sz,ud):
            if a==cfg["gethelditem"]: u.reg_write(UC_ARM_REG_R0,item); u.reg_write(UC_ARM_REG_PC,u.reg_read(UC_ARM_REG_LR))
            elif a==cfg["getformno"]: u.reg_write(UC_ARM_REG_R0,form); u.reg_write(UC_ARM_REG_PC,u.reg_read(UC_ARM_REG_LR))
        mu.hook_add(UC_HOOK_CODE,h,begin=cfg["gethelditem"],end=cfg["gethelditem"])
        mu.hook_add(UC_HOOK_CODE,h,begin=cfg["getformno"],end=cfg["getformno"])
        try: mu.emu_start(cfg["cave"],0xDEAD0000,count=6000)
        except UcError: pass
        return mu.reg_read(UC_ARM_REG_R0)
    print("  [verify %s] Arceus form=Ghost: noplate=%s Earth=%s Spooky=%s KingsRock=%s"%(cfg["name"],
        T[cave(0,7)],T[cave(0x131,7)],T[cave(0x136,7)],T[cave(0x4A,7)]))

def build(src,cfg,out3ds):
    dst=out3ds
    shutil.copy(src,dst)
    f=open(dst,"r+b"); info=fp.locate(f); cr=fp.exefs(f,info["ncch"]); ca=cr["code_abs"]
    f.seek(ca); code=f.read(cr["code_sz"])
    patched=apply(code,cfg); verify(patched,cfg)
    f.seek(ca); f.write(patched); f.flush()
    code_hash=fp.sha_file(f,ca,cr["code_sz"]); cr["eh"][0x1E0:0x200]=code_hash
    f.seek(info["ncch"]+cr["exo"]); f.write(bytes(cr["eh"])); f.flush()
    sb=fp.sha_file(f,info["ncch"]+cr["exo"],cr["exhr"]); f.seek(info["ncch"]+0x1C0); f.write(sb); f.flush(); f.close()
    # hash check
    c2,_,_=get_code(dst); ok=all(struct.unpack_from("<I",c2,va-BASE)[0]==w for va,w in edits(cfg).items())
    print("  [%s] built %s (edits present: %s)"%(cfg["name"],dst,ok))

if __name__=="__main__":
    build("POKEMON NO NERFS/AlphaSapphire_UNNERFED.3ds", AS,
          "Arceus_Unnerf_Output/patched_roms/AlphaSapphire_ArceusFormType.3ds")
    build("POKEMON NO NERFS/OmegaRuby_UNNERFED.3ds", OR,
          "Arceus_Unnerf_Output/patched_roms/OmegaRuby_ArceusFormType.3ds")
