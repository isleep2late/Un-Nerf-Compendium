#!/usr/bin/env python3
"""USUM: NOP the Poké Resort item-form reset so item-derived formes (Giratina-Origin without the
Griseous Orb, Arceus, Genesect, Silvally) are not re-normalized to the held-item form when a mon is
placed in the Poké Resort. (Normal party/box/save already preserve them; the Resort is the one vector
that recomputes the form from the held item.) Auto-located by signature:

    mov r8,r0 ; mov r1,r4 ; mov r0,r7 ; bl ChangeFormNo ; mov r1,r8
    E1A08000    E1A01004    E1A00007    EB......          E1A01008

NOP the bl. Patches the .code in a built .3ds/.cia in place and re-fixes the ExeFS hashes."""
import sys, struct, shutil
sys.path.insert(0,"Arceus_Unnerf_Output/Un-Nerf-Compendium/Un-Nerf-Compendium-main/gen67_formepersist")
sys.path.insert(0,"Arceus_Unnerf_Output")
import formepersist as fp
from s2_lib import get_code
BASE=0x100000
SIG_PRE = bytes.fromhex("00 80 A0 E1 04 10 A0 E1 07 00 A0 E1".replace(" ",""))   # mov r8,r0;mov r1,r4;mov r0,r7
SIG_POST= bytes.fromhex("08 10 A0 E1")                                            # mov r1,r8

def find_resort_changeform(code):
    i=code.find(SIG_PRE); hits=[]
    while i!=-1:
        blpos=i+12
        w=struct.unpack_from("<I",code,blpos)[0]
        if (w>>24)==0xEB and code[blpos+4:blpos+8]==SIG_POST:
            hits.append(BASE+blpos)
        i=code.find(SIG_PRE,i+1)
    return hits

def patch(path):
    f=open(path,"r+b"); info=fp.locate(f); cr=fp.exefs(f,info["ncch"]); ca=cr["code_abs"]
    f.seek(ca); code=bytearray(f.read(cr["code_sz"]))
    hits=find_resort_changeform(code)
    assert len(hits)==1, "expected 1 Resort site, found %s"%[hex(h) for h in hits]
    site=hits[0]
    print("  Resort item-form ChangeFormNo @0x%X -> NOP"%site)
    struct.pack_into("<I",code,site-BASE,0xE320F000)
    f.seek(ca); f.write(bytes(code)); f.flush()
    code_hash=fp.sha_file(f,ca,cr["code_sz"]); cr["eh"][0x1E0:0x200]=code_hash
    f.seek(info["ncch"]+cr["exo"]); f.write(bytes(cr["eh"])); f.flush()
    sb=fp.sha_file(f,info["ncch"]+cr["exo"],cr["exhr"]); f.seek(info["ncch"]+0x1C0); f.write(sb); f.flush()
    # TMD for .cia
    if path.endswith(".cia"):
        th=0x140; bb=th+0xC4+64*0x24
        f.seek(info["tmd_off"]); td=bytearray(f.read(info["tmd_sz"]))
        c0=struct.unpack_from(">Q",td,bb+0x08)[0]; td[bb+0x10:bb+0x30]=fp.sha_file(f,info["ncch"],c0)
        ci=th+0xC4; idx,cmd=struct.unpack_from(">HH",td,ci)
        td[ci+0x04:ci+0x24]=__import__("hashlib").sha256(bytes(td[bb+idx*0x30:bb+(idx+max(cmd,1))*0x30])).digest()
        td[th+0xA4:th+0xC4]=__import__("hashlib").sha256(bytes(td[th+0xC4:th+0xC4+64*0x24])).digest()
        f.seek(info["tmd_off"]); f.write(bytes(td)); f.flush()
    f.close()
    # verify
    c2,_,_=get_code(path); ok=struct.unpack_from("<I",c2,site-BASE)[0]==0xE320F000
    print("  [%s] patched + rehashed; NOP present=%s"%(path.split('/')[-1],ok))

if __name__=="__main__":
    for p in sys.argv[1:]:
        print(">>>",p); patch(p)
