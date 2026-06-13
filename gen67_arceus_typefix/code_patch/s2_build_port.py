#!/usr/bin/env python3
"""Build the Arceus/Silvally fix for a given game config + source CIA, convert to .3ds, verify."""
import sys, struct, hashlib, shutil
sys.path.insert(0,"Arceus_Unnerf_Output/Un-Nerf-Compendium/Un-Nerf-Compendium-main/gen67_formepersist")
sys.path.insert(0,"Arceus_Unnerf_Output/Un-Nerf-Compendium/Un-Nerf-Compendium-main")
sys.path.insert(0,"Arceus_Unnerf_Output")
import formepersist as fp, unnerf as un, s2_arceus_port as P, s2_cia_to_3ds as C
BASE=0x100000

def unicorn_verify(patched,cfg):
    from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE,UcError
    from unicorn.arm_const import UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R4,UC_ARM_REG_SP,UC_ARM_REG_LR,UC_ARM_REG_PC
    TYPES=["Normal","Fighting","Flying","Poison","Ground","Rock","Bug","Ghost","Steel","Fire","Water","Grass","Electric","Psychic","Ice","Dragon","Dark","Fairy"]
    csz=(len(patched)+0xFFF)&~0xFFF
    GETMONSNO=cfg["gethelditem"]-0x18F8 if False else None
    # find GetMonsNo/GetAbility used by GetType: they are the bl at gt+0x0C and gt+0x18
    g=cfg["gt1"]
    def blt(va):
        w=struct.unpack_from("<I",patched,va-BASE)[0]; im=w&0xFFFFFF; im-=0x1000000 if im&0x800000 else 0
        return (va+8)+im*4
    GETMONS=blt(g+0x0C); GETAB=blt(g+0x18)
    def getType1(species,ability,item,form):
        mu=Uc(UC_ARCH_ARM,UC_MODE_ARM); mu.mem_map(BASE,csz); mu.mem_write(BASE,bytes(patched))
        mu.mem_map(0x900000,0x40000); mu.reg_write(UC_ARM_REG_SP,0x930000); mu.reg_write(UC_ARM_REG_R0,0x920000)
        mu.reg_write(UC_ARM_REG_LR,0xDEAD0000)
        ret={GETMONS:species,GETAB:ability,cfg["gethelditem"]:item,cfg["getformno"]:form}
        def h(u,a,sz,ud):
            if a in ret: u.reg_write(UC_ARM_REG_R0,ret[a]); u.reg_write(UC_ARM_REG_PC,u.reg_read(UC_ARM_REG_LR))
        for fn in ret: mu.hook_add(UC_HOOK_CODE,h,begin=fn,end=fn)
        try: mu.emu_start(g,0xDEAD0000,count=4000)
        except UcError: pass
        return mu.reg_read(UC_ARM_REG_R0)
    def itf(species,item):
        mu=Uc(UC_ARCH_ARM,UC_MODE_ARM); mu.mem_map(BASE,csz); mu.mem_write(BASE,bytes(patched))
        mu.mem_map(0x900000,0x40000); mu.reg_write(UC_ARM_REG_SP,0x930000); OUT=0x920000
        mu.reg_write(UC_ARM_REG_R0,species); mu.reg_write(UC_ARM_REG_R1,item); mu.reg_write(UC_ARM_REG_R2,OUT)
        mu.reg_write(UC_ARM_REG_LR,0xDEAD0000)
        try: mu.emu_start(0x32360C,0xDEAD0000,count=5000)
        except UcError: pass
        return mu.reg_read(UC_ARM_REG_R0)
    tn=lambda f: TYPES[f] if f<18 else "?%d"%f
    print("  [verify] Protean Arceus form=Ghost: no-plate=%s EarthPlate=%s SpookyPlate=%s KingsRock=%s"%(
        tn(getType1(493,0xA8,0,7)),tn(getType1(493,0xA8,0x131,7)),tn(getType1(493,0xA8,0x136,7)),tn(getType1(493,0xA8,0x4A,7))))
    print("  [verify] form-persist ItemToForm: Arceus+noplate r0=%d (0=persist), Arceus+Earth r0=%d"%(itf(493,0),itf(493,0x131)))

def build(src,cfg,out3ds):
    dst=out3ds.replace(".3ds",".cia")
    shutil.copy(src,dst)
    f=open(dst,"r+b"); info=fp.locate(f); cr=fp.exefs(f,info["ncch"]); ca=cr["code_abs"]
    f.seek(ca); code=f.read(cr["code_sz"])
    patched=P.apply(code,cfg)
    unicorn_verify(patched,cfg)
    f.seek(ca); f.write(patched); f.flush()
    code_hash=fp.sha_file(f,ca,cr["code_sz"]); cr["eh"][0x1E0:0x200]=code_hash
    f.seek(info["ncch"]+cr["exo"]); f.write(bytes(cr["eh"])); f.flush()
    sb=fp.sha_file(f,info["ncch"]+cr["exo"],cr["exhr"]); f.seek(info["ncch"]+0x1C0); f.write(sb); f.flush()
    # Battle.cro Protean species-list (same Battle.cro offset; located dynamically)
    f.seek(0); ri=un.parse(f); ab=ri["battle_abs"]+0x102670
    f.seek(ab); assert f.read(4)==bytes.fromhex("ed010503"),"Battle.cro species list mismatch"
    f.seek(ab); f.write(bytes.fromhex("ffffffff"))
    slots={un.master_slot_for(ri,ab),un.master_slot_for(ri,ab+3)}
    for k in sorted(slots):
        h=un.recompute_master_slot(f,ri,k); f.seek(ri["romfs_abs"]+0x60+k*0x20); f.write(h)
    f.seek(ri["romfs_abs"]); ivfc_plus=f.read(0x60+ri["master_size"]); rh=hashlib.sha256(ivfc_plus).digest()
    f.seek(ri["content_off"]+0x1E0); f.write(rh); f.flush()
    th=0x140; bb=th+0xC4+64*0x24
    f.seek(info["tmd_off"]); td=bytearray(f.read(info["tmd_sz"]))
    c0=struct.unpack_from(">Q",td,bb+0x08)[0]; td[bb+0x10:bb+0x30]=fp.sha_file(f,info["ncch"],c0)
    ci=th+0xC4; idx,cmd=struct.unpack_from(">HH",td,ci)
    td[ci+0x04:ci+0x24]=hashlib.sha256(bytes(td[bb+idx*0x30:bb+(idx+max(cmd,1))*0x30])).digest()
    td[th+0xA4:th+0xC4]=hashlib.sha256(bytes(td[th+0xC4:th+0xC4+64*0x24])).digest()
    f.seek(info["tmd_off"]); f.write(bytes(td)); f.flush(); f.close()
    C.convert(dst,out3ds)
    # hash check
    g=open(out3ds,"rb"); h=g.read(0x200); ncch=struct.unpack_from("<I",h,0x120)[0]*0x200
    g.seek(ncch); n=g.read(0x200); exo=struct.unpack_from("<I",n,0x1A0)[0]*0x200; exhr=struct.unpack_from("<I",n,0x1A8)[0]*0x200; rf=struct.unpack_from("<I",n,0x1B0)[0]*0x200
    g.seek(ncch+exo); ek=hashlib.sha256(g.read(exhr)).digest()==n[0x1C0:0x1E0]
    g.seek(ncch+rf); iv=g.read(0x60); msz=struct.unpack_from("<I",iv,8)[0]; g.seek(ncch+rf); rk=hashlib.sha256(g.read(0x60+msz)).digest()==n[0x1E0:0x200]
    print("  [%s] hashes ExeFS=%s RomFS=%s -> %s"%(cfg["name"],ek,rk,out3ds)); g.close()

if __name__=="__main__":
    build("POKEMON NO NERFS/UltraSun_UNNERFED.cia", P.US,
          "Arceus_Unnerf_Output/patched_roms/UltraSun_ArceusFinal.3ds")
