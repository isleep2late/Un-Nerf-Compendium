#!/usr/bin/env python3
"""
Gen 6/7 Forme Persistence patcher  (UnNerf Compendium)
======================================================
Stops Pokemon X/Y, OR/AS, S/M, US/UM from RESETTING battle-only / restricted formes
when you load a save (and in several other out-of-battle contexts).  After patching, a
forme you set with PKHeX (Mega, Primal, Hoopa-Unbound, Shaymin-Sky, Furfrou trim,
Necrozma Ultra-Burst, Zygarde-Complete, ...) STAYS on your party through save+reload.

It NOPs the out-of-battle `pml::pokepara::CoreParam::ChangeFormNo(base)` revert calls and
leaves the forme SETTERS intact (Mega Evolve, Prison Bottle, terrain changes, Mystery Gift),
so you can still change formes normally.

USAGE
-----
  python formepersist.py  YOUR_GAME.cia            # patch in place (.cia or decrypted .3ds)
  python formepersist.py  YOUR_GAME.cia --verify
  python formepersist.py  YOUR_GAME.cia --full     # + verified extended forme table (US/UM, OR/AS)

The file must be a DECRYPTED dump (content0 is an unencrypted NCCH).  The Mega/Primal fix is
auto-located by instruction signature (works on X/Y, OR/AS, S/M, US/UM, any region/version).
The script re-fixes the ExeFS .code hash, ExeFS superblock hash and (for .cia) TMD content hash.
"""
import struct, hashlib, sys

NOP = 0xE1A00000   # mov r0,r0

# verified EXTRA revert sites (vaddrs of `bl ChangeFormNo` to NOP) for the --full patch.
# Each entry is validated at runtime (must actually be a bl ChangeFormNo) so it self-skips on
# a sister game / different region whose addresses are shifted.
FULL_TABLE = {
 "USUM": [0x4450fc,0x445108,0x445114,0x44525c,0x445740,0x444474,0x444f2c,0x444f58,
          0x444fa4,0x44532c,0x4453b8,0x4453ec,0x445420,0x4454ac,0x445534,0x45bbc0],
 "ORAS": [0x444fd8,0x4464d0,0x46d9f0,0x46da24,0x46e63c,0x46e670,0x46e754,0x46e7dc,
          0x46e874,0x46e88c,0x46eb20,0x46ece8,0x46ebc4],
}

def u32(b,o): return struct.unpack_from("<I",b,o)[0]
def w(code,o): return struct.unpack_from("<I",code,o)[0]

def locate(f):
    f.seek(0); sig=f.read(0x200)
    if sig[0x100:0x104]==b"NCSD":
        return dict(ncch=u32(sig,0x120)*0x200, is_cia=False, tmd_off=0, tmd_sz=0)
    f.seek(0); head=f.read(0x2020)
    hs,_t,_v,cert,tik,tmd,meta=struct.unpack_from("<IHHIIII",head,0)
    a=lambda x:(x+63)&~63; tmd_off=a(a(a(hs)+cert)+tik); cont=a(tmd_off+tmd)
    return dict(ncch=cont, is_cia=True, tmd_off=tmd_off, tmd_sz=tmd)

def exefs(f,ncch):
    f.seek(ncch); h=f.read(0x200)
    assert h[0x100:0x104]==b"NCCH","content0 is not a decrypted NCCH"
    exo=u32(h,0x1A0)*0x200; exhr=u32(h,0x1A8)*0x200
    f.seek(ncch+exo); eh=bytearray(f.read(0x200)); cfo=cfsz=None
    for i in range(10):
        if eh[i*0x10:i*0x10+8].rstrip(b"\0")==b".code": cfo,cfsz=struct.unpack_from("<II",eh,i*0x10+8)
    return dict(exo=exo,exhr=exhr,eh=eh,code_abs=ncch+exo+0x200+cfo,code_sz=cfsz)

def sha_file(f,off,size):
    h=hashlib.sha256(); f.seek(off); rem=size
    while rem>0:
        b=f.read(min(16<<20,rem))
        if not b: break
        h.update(b); rem-=len(b)
    return h.digest()

def find_reset_party(code):
    BASE=0x100000
    for o in range(0,len(code)-16,4):
        a,b,c,d=w(code,o),w(code,o+4),w(code,o+8),w(code,o+12)
        if (a>>24)!=0xEB: continue
        if (b&0xFFF00FFF)!=0xE2800001 or ((b>>16)&0xF)!=((b>>12)&0xF): continue
        if (c&0xFFF0F000)!=0xE1500000 or ((c>>16)&0xF)!=((b>>16)&0xF): continue
        if (d>>24)!=0xBA: continue
        off=d&0xFFFFFF; off-=0x1000000 if off&0x800000 else 0
        if off>=0: continue
        im=a&0xFFFFFF; im-=0x1000000 if im&0x800000 else 0
        cfn=(BASE+o)+8+im*4
        bs=((BASE+o+12)+8+off*4)-BASE; be=o+4
        if bs<0 or be-bs>0x200: continue
        sites=[BASE+k for k in range(bs,be,4)
               if (w(code,k)>>24)==0xEB and (BASE+k)+8+((w(code,k)&0xFFFFFF)-(0x1000000 if w(code,k)&0x800000 else 0))*4==cfn]
        cmp1=sum(1 for k in range(bs,be,4) if w(code,k)==0xE3500001)
        if len(sites)>=3 and cmp1>=2:
            return cfn, sites[:3]
    return None, []

def find_safe_condition_reverts(code, cfn):
    BASE=0x100000; out=[]
    for o in range(0,len(code)-4,4):
        x=w(code,o)
        if (x>>24)!=0xEB: continue
        im=x&0xFFFFFF; im-=0x1000000 if im&0x800000 else 0
        if (BASE+o)+8+im*4!=cfn: continue
        win=[w(code,o-4*k) for k in range(1,12) if o-4*k>=0]
        if 0xE3A01000 in win[:5] and any(v==0xE3500001 for v in win[:9]):
            out.append(BASE+o)
    return out

def is_bl_to(code, va, cfn):
    fo=va-0x100000
    if not (0<=fo<len(code)): return False
    x=w(code,fo)
    if (x>>24)!=0xEB: return False
    im=x&0xFFFFFF; im-=0x1000000 if im&0x800000 else 0
    return (0x100000+fo)+8+im*4==cfn

def detect_game(cfn):
    if cfn==0x323d28: return "USUM"
    if cfn==0x3b41cc: return "ORAS"
    return None

def main():
    if len(sys.argv)<2:
        print(__doc__); return
    p=sys.argv[1]; ver="--verify" in sys.argv; full="--full" in sys.argv
    f=open(p,"rb" if ver else "r+b")
    info=locate(f); cr=exefs(f,info["ncch"]); ca=cr["code_abs"]
    f.seek(ca); code=f.read(cr["code_sz"])
    cfn,mega=find_reset_party(code)
    if not cfn:
        print("Could not locate ChangeFormNo / ResetPartyBattleForms.")
        print("(Already patched, or not a supported gen6/7 build.)"); return
    sites=set(mega) | set(find_safe_condition_reverts(code,cfn))
    if full:
        g=detect_game(cfn); tbl=FULL_TABLE.get(g,[])
        matched=[va for va in tbl if is_bl_to(code,va,cfn)]
        for va in matched: sites.add(va)
        if tbl and len(matched)==len(tbl):
            print("  [--full] %s extended table matched (%d sites)"%(g,len(matched)))
        elif matched:
            print("  [--full] %d/%d table sites matched (sub-version differs); using auto set + matches"%(len(matched),len(tbl)))
        else:
            print("  [--full] table did not match this version; auto-located set only")
    sites=sorted(sites)
    if ver:
        npatched=sum(1 for va in sites if w(code,va-0x100000)==NOP)
        print("ChangeFormNo=0x%x ; %d revert sites; %d already NOP"%(cfn,len(sites),npatched))
        f.close(); return
    nch=0
    for va in sites:
        fo=va-0x100000; cur=w(code,fo)
        if cur==NOP: continue
        if (cur>>24)!=0xEB:
            print("  skip 0x%X (not a bl: %08X)"%(va,cur)); continue
        f.seek(ca+fo); f.write(struct.pack("<I",NOP)); nch+=1
    f.flush()
    code_hash=sha_file(f,ca,cr["code_sz"])
    cr["eh"][0x1E0:0x200]=code_hash; f.seek(info["ncch"]+cr["exo"]); f.write(bytes(cr["eh"])); f.flush()
    sb=sha_file(f,info["ncch"]+cr["exo"],cr["exhr"]); f.seek(info["ncch"]+0x1C0); f.write(sb); f.flush()
    print("Patched %d forme-revert sites."%nch)
    if info["is_cia"]:
        th=0x140; base=th+0xC4+64*0x24
        f.seek(info["tmd_off"]); td=bytearray(f.read(info["tmd_sz"]))
        c0=struct.unpack_from(">Q",td,base+0x08)[0]
        td[base+0x10:base+0x30]=sha_file(f,info["ncch"],c0)
        ci=th+0xC4; idx,cmd=struct.unpack_from(">HH",td,ci)
        td[ci+0x04:ci+0x24]=hashlib.sha256(bytes(td[base+idx*0x30:base+(idx+max(cmd,1))*0x30])).digest()
        td[th+0xA4:th+0xC4]=hashlib.sha256(bytes(td[th+0xC4:th+0xC4+64*0x24])).digest()
        f.seek(info["tmd_off"]); f.write(bytes(td)); f.flush()
        print("TMD content hash fixed (.cia ready to install).")
    f.close()
    print("Done. Re-install / re-load the game in your emulator.")

if __name__=="__main__":
    main()
