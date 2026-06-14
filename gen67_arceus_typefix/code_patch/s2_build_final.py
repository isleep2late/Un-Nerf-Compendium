#!/usr/bin/env python3
"""Build the FINAL USUM Arceus/Silvally ROM on top of NO-NERF UltraMoon:
  - code.bin: form-persistence + plate-or-form type handler (s2_arceus_final)
  - Battle.cro: Protean species-list -> 0xFFFF
Rehash ExeFS/NCCH/RomFS/TMD (hashes computed into vars first), then convert to .3ds + verify."""
import sys, struct, hashlib, shutil
sys.path.insert(0,"Arceus_Unnerf_Output/Un-Nerf-Compendium/Un-Nerf-Compendium-main/gen67_formepersist")
sys.path.insert(0,"Arceus_Unnerf_Output/Un-Nerf-Compendium/Un-Nerf-Compendium-main")
sys.path.insert(0,"Arceus_Unnerf_Output")
import formepersist as fp, unnerf as un, s2_arceus_final as AF
BASE=0x100000
def build(src,dst):
    shutil.copy(src,dst)
    f=open(dst,"r+b"); info=fp.locate(f); cr=fp.exefs(f,info["ncch"]); ca=cr["code_abs"]
    f.seek(ca); code=f.read(cr["code_sz"])
    patched=AF.apply(code)                      # validates sites + applies form-persist+cave+hooks
    f.seek(ca); f.write(patched); f.flush()
    code_hash=fp.sha_file(f,ca,cr["code_sz"])
    cr["eh"][0x1E0:0x200]=code_hash; f.seek(info["ncch"]+cr["exo"]); f.write(bytes(cr["eh"])); f.flush()
    sb=fp.sha_file(f,info["ncch"]+cr["exo"],cr["exhr"]); f.seek(info["ncch"]+0x1C0); f.write(sb); f.flush()
    print("(1) code.bin: form-persist + plate-or-form handler applied; ExeFS rehashed")
    # Battle.cro Protean species-list
    f.seek(0); ri=un.parse(f); ab=ri["battle_abs"]+0x102670
    f.seek(ab); assert f.read(4)==bytes.fromhex("ed010503")
    f.seek(ab); f.write(bytes.fromhex("ffffffff"))
    slots={un.master_slot_for(ri,ab),un.master_slot_for(ri,ab+3)}
    for k in sorted(slots):
        h=un.recompute_master_slot(f,ri,k); f.seek(ri["romfs_abs"]+0x60+k*0x20); f.write(h)
    f.seek(ri["romfs_abs"]); ivfc_plus=f.read(0x60+ri["master_size"]); rh=hashlib.sha256(ivfc_plus).digest()
    f.seek(ri["content_off"]+0x1E0); f.write(rh); f.flush()
    print("(2) Battle.cro Protean species-list patched; RomFS rehashed")
    # TMD
    th=0x140; b=th+0xC4+64*0x24
    f.seek(info["tmd_off"]); td=bytearray(f.read(info["tmd_sz"]))
    c0=struct.unpack_from(">Q",td,b+0x08)[0]
    td[b+0x10:b+0x30]=fp.sha_file(f,info["ncch"],c0)
    ci=th+0xC4; idx,cmd=struct.unpack_from(">HH",td,ci)
    td[ci+0x04:ci+0x24]=hashlib.sha256(bytes(td[b+idx*0x30:b+(idx+max(cmd,1))*0x30])).digest()
    td[th+0xA4:th+0xC4]=hashlib.sha256(bytes(td[th+0xC4:th+0xC4+64*0x24])).digest()
    f.seek(info["tmd_off"]); f.write(bytes(td)); f.flush(); f.close()
    print("(3) TMD refreshed ->",dst)

if __name__=="__main__":
    build("POKEMON NO NERFS/UltraMoon_UNNERFED.cia",
          "Arceus_Unnerf_Output/patched_roms/UltraMoon_ArceusFinal.cia")
    import s2_cia_to_3ds as c
    c.convert("Arceus_Unnerf_Output/patched_roms/UltraMoon_ArceusFinal.cia",
              "Arceus_Unnerf_Output/patched_roms/UltraMoon_ArceusFinal.3ds")
    # hash validity
    def check(path):
        f=open(path,"rb"); h=f.read(0x200); ncch=struct.unpack_from("<I",h,0x120)[0]*0x200
        f.seek(ncch); n=f.read(0x200)
        exo=struct.unpack_from("<I",n,0x1A0)[0]*0x200; exhr=struct.unpack_from("<I",n,0x1A8)[0]*0x200
        romfs=struct.unpack_from("<I",n,0x1B0)[0]*0x200
        f.seek(ncch+exo); exefs_ok=hashlib.sha256(f.read(exhr)).digest()==n[0x1C0:0x1E0]
        f.seek(ncch+romfs); ivfc=f.read(0x60); msz=struct.unpack_from("<I",ivfc,8)[0]
        f.seek(ncch+romfs); romfs_ok=hashlib.sha256(f.read(0x60+msz)).digest()==n[0x1E0:0x200]
        print("HASH CHECK ArceusFinal.3ds: ExeFS=%s RomFS=%s"%(exefs_ok,romfs_ok))
    check("Arceus_Unnerf_Output/patched_roms/UltraMoon_ArceusFinal.3ds")
