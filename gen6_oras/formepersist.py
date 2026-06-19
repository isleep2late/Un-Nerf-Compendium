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
  python formepersist.py  YOUR_GAME.cia --full     # + verified extended forme table (OR/AS, US/UM)

The file must be a DECRYPTED dump (content0 is an unencrypted NCCH).  The Mega/Primal fix is
auto-located by instruction signature (works on X/Y, OR/AS, S/M, US/UM, any region/version).
The script re-fixes the ExeFS .code hash, ExeFS superblock hash and (for .cia) TMD content hash.

v2 changes (2026-06): added verified revert tables for BOTH sister games of each pair
(Omega Ruby and Ultra Sun were previously covered only by the auto-located subset, so the
public table half-patched them).  Sisters share the same ChangeFormNo address, so the table
is chosen by Title ID and then re-validated instruction-by-instruction.  Also adds the
USUM-only Hoopa "extra reset" fix: in US/UM the Hoopa-Unbound -> Confined revert is a 3-call
sequence (ChangeFormNo + two more CoreParam setters); --full now NOPs all three so Hoopa
actually persists on the PC/box path.
"""
import struct, hashlib, sys

NOP = 0xE1A00000   # mov r0,r0
BASE = 0x100000

# ---------------------------------------------------------------------------
# Verified `bl ChangeFormNo` revert sites, keyed by Title ID.  All four were
# derived by index-aligning each sister against its already-correct partner and
# validated against retail dumps (each is a genuine `mov r1,#0 ; bl ChangeFormNo`).
#   Omega Ruby   = Alpha Sapphire + 8   (16 sites)
#   Ultra Sun    = Ultra Moon     - 4   (17 sites)
# ---------------------------------------------------------------------------
TID_TABLE = {
 0x000400000011C500: ("Alpha Sapphire", [  # ORAS, cfn 0x3B41CC
     0x444FD8,0x4464D0,0x46D9F0,0x46DA24,0x46E63C,0x46E670,0x46E754,0x46E7DC,
     0x46E874,0x46E88C,0x46EB20,0x46EBC4,0x46ECE8,0x46F058,0x46F08C,0x46F0C0]),
 0x000400000011C400: ("Omega Ruby", [
     0x444FE0,0x4464D8,0x46D9F8,0x46DA2C,0x46E644,0x46E678,0x46E75C,0x46E7E4,
     0x46E87C,0x46E894,0x46EB28,0x46EBCC,0x46ECF0,0x46F060,0x46F094,0x46F0C8]),
 0x00040000001B5100: ("Ultra Moon", [     # USUM, cfn 0x323D28
     0x444474,0x444C38,0x444C6C,0x444CA0,0x444F2C,0x444F58,0x444FA4,0x4450FC,
     0x44525C,0x44532C,0x4453B8,0x4453EC,0x445420,0x4454AC,0x445534,0x445740,0x45BBC0]),
 0x00040000001B5000: ("Ultra Sun", [
     0x444470,0x444C34,0x444C68,0x444C9C,0x444F28,0x444F54,0x444FA0,0x4450F8,
     0x445258,0x445328,0x4453B4,0x4453E8,0x44541C,0x4454A8,0x445530,0x44573C,0x45BBBC]),
}
# Known ChangeFormNo address per family — lets the script keep working on a ROM whose
# Mega/Primal loop is ALREADY NOP'd (the auto-locate signature then fails), e.g. when you
# want to add the Hoopa extra-reset fix to a build you patched with an older version.
TID_CFN = {
 0x000400000011C500:0x3B41CC, 0x000400000011C400:0x3B41CC,   # OR/AS
 0x00040000001B5100:0x323D28, 0x00040000001B5000:0x323D28,   # US/UM
}

# NOTE: Hoopa "extra reset" calls are no longer hardcoded — `find_forme_destructive_resets()`
# auto-locates the full Hoopa destructive-reset blocks (ChangeFormNo + adjacent recurring
# extra-setters) on every version, including the OR/AS blocks the old table missed.

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

def title_id(f, ncch):
    f.seek(ncch); h=f.read(0x200)
    return struct.unpack_from("<Q",h,0x118)[0]   # NCCH header TitleID

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
    out=[]
    for o in range(0,len(code)-4,4):
        x=w(code,o)
        if (x>>24)!=0xEB: continue
        im=x&0xFFFFFF; im-=0x1000000 if im&0x800000 else 0
        if (BASE+o)+8+im*4!=cfn: continue
        win=[w(code,o-4*k) for k in range(1,12) if o-4*k>=0]
        if 0xE3A01000 in win[:5] and any(v==0xE3500001 for v in win[:9]):
            out.append(BASE+o)
    return out

def _is_mov(code,o):                 # ARM mov rD,#imm or mov rD,rS
    if not (0<=o<len(code)-3): return False
    x=w(code,o); return (x&0x0FE00000) in (0x03A00000,0x01A00000)

def _trailing_setters(code,o,cfn):
    """(site,target) for the `mov r1,.. ; mov r0,.. ; bl <T!=cfn>` calls right after a CFN bl."""
    out=[]; p=o+4
    for _ in range(3):
        if _is_mov(code,p) and _is_mov(code,p+4):
            t=bl_target(code,BASE+p+8)
            if t is not None and t!=cfn: out.append((BASE+p+8,t)); p+=12; continue
        break
    return out

def find_forme_destructive_resets(code, cfn):
    """Hoopa-style DESTRUCTIVE reset = `bl ChangeFormNo` (revert to base) immediately trailed by
    extra CoreParam setters that recur across multiple such blocks AND live next to ChangeFormNo.
    The old per-game table missed these entirely for OR/AS (Hoopa never persisted). Returns the
    full set of call sites to NOP. Version-independent (auto-located)."""
    from collections import Counter
    callers=[]
    for o in range(0,len(code)-4,4):
        if bl_target(code,BASE+o)==cfn: callers.append(BASE+o)
    freq=Counter(); trail={}
    for va in callers:
        ts=_trailing_setters(code,va-BASE,cfn); trail[va]=ts
        for _,t in ts: freq[t]+=1
    setters={t for t,c in freq.items() if c>=2 and abs(t-cfn)<0x4000}
    sites=[]
    for va in callers:
        hit=[(s,t) for s,t in trail[va] if t in setters]
        if hit: sites.append(va); sites+=[s for s,_ in hit]
    return sorted(set(sites))

def bl_target(code, va):
    fo=va-BASE
    if not (0<=fo<len(code)): return None
    x=w(code,fo)
    if (x>>24)!=0xEB: return None
    im=x&0xFFFFFF; im-=0x1000000 if im&0x800000 else 0
    return (BASE+fo)+8+im*4

def is_bl_to(code, va, cfn):
    return bl_target(code, va)==cfn

def site_ok(code, va, cfn):
    """A table site is valid if it is a live `bl ChangeFormNo` OR already NOP'd
    (so the script also works on a partially/previously patched ROM)."""
    fo=va-BASE
    if not (0<=fo<len(code)): return False
    return w(code,fo)==NOP or is_bl_to(code,va,cfn)

def pick_table(code, cfn, tid):
    """Return (label, validated_revert_sites, full_table) for this build."""
    # 1) prefer the Title-ID table (accept live bl OR already-NOP'd sites)
    if tid in TID_TABLE:
        label, tbl = TID_TABLE[tid]
        matched=[va for va in tbl if site_ok(code,va,cfn)]
        if len(matched)==len(tbl):
            return label, matched, tbl
        # Title ID known but addresses don't validate (region/revision shift) -> fall through
    # 2) otherwise pick whichever table validates best (sisters share cfn)
    best=(None,[],[])
    for tid_k,(label,tbl) in TID_TABLE.items():
        matched=[va for va in tbl if is_bl_to(code,va,cfn)]
        if len(matched)>len(best[1]): best=(label,matched,tbl)
    return best

def main():
    if len(sys.argv)<2:
        print(__doc__); return
    p=sys.argv[1]; ver="--verify" in sys.argv; full="--full" in sys.argv
    f=open(p,"rb" if ver else "r+b")
    info=locate(f); cr=exefs(f,info["ncch"]); ca=cr["code_abs"]
    tid=title_id(f, info["ncch"])
    f.seek(ca); code=f.read(cr["code_sz"])
    cfn,mega=find_reset_party(code)
    if not cfn:
        # Mega/Primal loop already NOP'd (signature gone). Recover cfn from the Title ID so
        # the script can still verify, or add the Hoopa extra-reset fix to an older build.
        cfn=TID_CFN.get(tid)
        if not cfn:
            print("Could not locate ChangeFormNo / ResetPartyBattleForms.")
            print("(Already patched and unknown Title ID, or not a supported gen6/7 build.)"); return
        mega=[]
        print("  [info] auto-locate signature gone (already patched) -> using known ChangeFormNo 0x%X for Title 0x%016X"%(cfn,tid))
    sites=set(mega) | set(find_safe_condition_reverts(code,cfn))
    extra=[]   # non-ChangeFormNo NOPs: the recurring Hoopa destructive extra-setters
    if full:
        label, matched, tbl = pick_table(code, cfn, tid)
        for va in matched: sites.add(va)
        if tbl and len(matched)==len(tbl):
            print("  [--full] %s table matched (%d sites)"%(label,len(matched)))
        elif matched:
            print("  [--full] %s: %d/%d table sites matched (sub-version differs); auto set + matches"%(label,len(matched),len(tbl)))
        else:
            print("  [--full] no table matched this version; auto-located set only")
        # Hoopa-style DESTRUCTIVE resets (ChangeFormNo + adjacent recurring extra-setters),
        # auto-located & version-independent. The old per-game table missed these entirely for
        # OR/AS, so Hoopa never persisted there (it reverts on PC/box/field/party-load).
        dr=find_forme_destructive_resets(code,cfn)
        for s in dr:
            if bl_target(code,s)==cfn: sites.add(s)   # the ChangeFormNo(base) call
            else: extra.append(s)                      # an extra Hoopa-data setter call
        if dr: print("  [--full] +%d Hoopa destructive-reset call(s) auto-located"%len(dr))
    sites=sorted(sites)
    if ver:
        npatched=sum(1 for va in sites if w(code,va-BASE)==NOP)
        ex_np=sum(1 for s in extra if w(code,s-BASE)==NOP)
        print("TitleID=0x%016X ; ChangeFormNo=0x%x"%(tid,cfn))
        print("%d revert sites; %d already NOP"%(len(sites),npatched))
        if extra: print("%d Hoopa extra-reset sites; %d already NOP"%(len(extra),ex_np))
        f.close(); return
    n_rev=n_ext=0
    for va in sites:
        fo=va-BASE; cur=w(code,fo)
        if cur==NOP: continue
        if (cur>>24)!=0xEB:
            print("  skip 0x%X (not a bl: %08X)"%(va,cur)); continue
        f.seek(ca+fo); f.write(struct.pack("<I",NOP)); n_rev+=1
    for site in extra:
        fo=site-BASE
        if w(code,fo)==NOP: continue
        if (w(code,fo)>>24)!=0xEB: continue
        f.seek(ca+fo); f.write(struct.pack("<I",NOP)); n_ext+=1
    nch=n_rev+n_ext
    f.flush()
    code_hash=sha_file(f,ca,cr["code_sz"])
    cr["eh"][0x1E0:0x200]=code_hash; f.seek(info["ncch"]+cr["exo"]); f.write(bytes(cr["eh"])); f.flush()
    sb=sha_file(f,info["ncch"]+cr["exo"],cr["exhr"]); f.seek(info["ncch"]+0x1C0); f.write(sb); f.flush()
    print("Patched %d sites (%d forme-revert + %d Hoopa extra)."%(nch,n_rev,n_ext))
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
