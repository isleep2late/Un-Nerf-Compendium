#!/usr/bin/env python3
"""Stage 2 shared helpers: extract .code from a decrypted 3DS dump (.3ds NCSD or .cia),
locate ChangeFormNo via the ResetPartyBattleForms loop signature, and enumerate every
`bl ChangeFormNo` caller. Read-only."""
import struct
NOP=0xE1A00000; BASE=0x100000
def u32(b,o): return struct.unpack_from("<I",b,o)[0]
def w(c,o): return struct.unpack_from("<I",c,o)[0]
def locate(f):
    f.seek(0); s=f.read(0x200)
    if s[0x100:0x104]==b"NCSD": return u32(s,0x120)*0x200
    f.seek(0); h=f.read(0x2020); hs,_t,_v,cert,tik,tmd,meta=struct.unpack_from("<IHHIIII",h,0)
    a=lambda x:(x+63)&~63; t=a(a(a(hs)+cert)+tik); return a(t+tmd)
def get_code(path):
    f=open(path,"rb"); ncch=locate(f); f.seek(ncch); hdr=f.read(0x200)
    if hdr[0x100:0x104]!=b"NCCH": raise RuntimeError("content0 NOT decrypted NCCH: %r"%hdr[0x100:0x104])
    exo=u32(hdr,0x1A0)*0x200; f.seek(ncch+exo); eh=f.read(0x200); cfo=cfsz=None
    for i in range(10):
        if eh[i*0x10:i*0x10+8].rstrip(b"\0")==b".code": cfo,cfsz=struct.unpack_from("<II",eh,i*0x10+8)
    ca=ncch+exo+0x200+cfo; f.seek(ca); code=f.read(cfsz); f.close()
    return code,ca,cfsz
def find_reset_party(code):
    # returns (cfn_vaddr, [3 mega/primal bl sites]) using formepersist.py's signature
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
def all_bl_callers(code, cfn):
    """every vaddr whose word is `bl` targeting cfn (works whether bl present or NOP'd: only finds live bl)."""
    out=[]
    for o in range(0,len(code)-4,4):
        x=w(code,o)
        if (x>>24)!=0xEB: continue
        im=x&0xFFFFFF; im-=0x1000000 if im&0x800000 else 0
        if (BASE+o)+8+im*4==cfn: out.append(BASE+o)
    return out
def bl_target(code, va):
    fo=va-BASE; x=w(code,fo)
    if (x>>24)!=0xEB: return None
    im=x&0xFFFFFF; im-=0x1000000 if im&0x800000 else 0
    return (BASE+fo)+8+im*4
