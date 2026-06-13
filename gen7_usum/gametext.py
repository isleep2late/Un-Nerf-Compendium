#!/usr/bin/env python3
"""Minimal Gen-7 (USUM) GARC message-text decoder/encoder for the Un-Nerf Compendium.
Used to rewrite ability/item descriptions (a/0/3/2). Pure standard library."""
import struct
def _u16(d,o): return struct.unpack_from("<H",d,o)[0]
def _u32(d,o): return struct.unpack_from("<I",d,o)[0]
KEY_BASE=0x7C89; KEY_ADV=0x2983

def garc_subfiles(d):
    assert d[:4]==b"CRAG","not a GARC"
    headsize=_u32(d,4); data_off=_u32(d,0x10); p=headsize; p+=_u32(d,p+4)
    assert d[p:p+4]==b"BTAF"; cnt=_u32(d,p+8); q=p+0x0C; out=[]
    for _ in range(cnt):
        fl=_u32(d,q); q+=4; subs=[]
        for b in range(32):
            if fl&(1<<b):
                s=_u32(d,q); e=_u32(d,q+4); q+=12; subs.append((s,e))
        out.append(d[data_off+subs[0][0]:data_off+subs[0][1]] if subs else None)
    return out

def decode_raw(f):
    lines=_u16(f,2); secoff=_u32(f,0xC); base=secoff+4; key=KEY_BASE; out=[]
    for i in range(lines):
        off=_u32(f,base+i*8); ln=_u16(f,base+i*8+4); pos=secoff+off; k=key; w=[]
        for _ in range(ln):
            c=_u16(f,pos)^k; pos+=2; k=((k<<3)|(k>>13))&0xFFFF; w.append(c)
        out.append(w); key=(key+KEY_ADV)&0xFFFF
    return out

def _str_to_words(s):
    w=[]; i=0
    while i<len(s):
        if s[i:i+2]=="\\n": w.append(0xE000); i+=2; continue
        w.append(ord(s[i])); i+=1
    w.append(0); return w

def encode_raw(line_words, secoff=0x10):
    n=len(line_words); table=n*8; text=bytearray(); offs=[]; key=KEY_BASE
    for words in line_words:
        while len(text)%4: text+=b"\x00"
        off=4+table+len(text); k=key
        for c in words:
            text+=struct.pack("<H",(c^k)&0xFFFF); k=((k<<3)|(k>>13))&0xFFFF
        offs.append((off,len(words))); key=(key+KEY_ADV)&0xFFFF
    seclen=4+table+len(text); sec=bytearray(struct.pack("<I",seclen))
    for off,ln in offs: sec+=struct.pack("<IHH",off,ln,0)
    sec+=text
    return struct.pack("<HHIII",1,n,seclen,0,secoff)+bytes(sec)

def edit_textfile(sub, edits):
    words=decode_raw(sub)
    for idx,s in edits.items(): words[idx]=_str_to_words(s)
    return encode_raw(words)

def garc_repack(d, replacements, pad_to=None):
    files=garc_subfiles(d); new=[replacements.get(i,files[i]) for i in range(len(files))]
    headsize=_u32(d,4); p=headsize; p+=_u32(d,p+4); assert d[p:p+4]==b"BTAF"
    cnt=_u32(d,p+8); q=p+0x0C; flags=[]
    for _ in range(cnt): fl=_u32(d,q); q+=4; flags.append(fl); q+=bin(fl).count("1")*12
    al=lambda x:(x+3)&~3
    data=bytearray(); fatb=bytearray(); fato=bytearray()
    for i in range(cnt):
        fato+=struct.pack("<I",len(fatb)); fatb+=struct.pack("<I",flags[i]); f=new[i]
        if f is None: continue
        while len(data)%4: data+=b"\x00"
        st=len(data); fatb+=struct.pack("<III",st,st+len(f),len(f)); data+=f
    fato_c=b"OTAF"+struct.pack("<IHH",0x0C+len(fato),cnt,0xFFFF)+bytes(fato)
    fatb_c=b"BTAF"+struct.pack("<II",0x0C+len(fatb),cnt)+bytes(fatb)
    data_off=headsize+len(fato_c)+len(fatb_c)+0x0C; largest=max((len(x) for x in new if x),default=0)
    bmif=b"BMIF"+struct.pack("<II",0x0C+len(data),0)+bytes(data)
    body=fato_c+fatb_c+bmif
    hdr=b"CRAG"+struct.pack("<IHHI",headsize,0xFEFF,0x0600,4)+struct.pack("<IIIII",data_off,headsize+len(body),largest,al(largest),4)
    out=bytearray(hdr+body)
    if pad_to is not None:
        assert len(out)<=pad_to,"repacked larger than original"; out+=b"\x00"*(pad_to-len(out))
    return bytes(out)
