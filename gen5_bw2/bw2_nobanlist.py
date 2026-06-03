#!/usr/bin/env python3
r"""
Black 2 / White 2 -- remove ALL Battle Subway + PWT restrictions (banlist, Soul Dew, item
clause, species clause, >3 Pokemon). Edits the banlist NARC a/1/0/6 in place; pure stdlib.

The banlist lives in narc a/1/0/6 (B2/W2). Each 188-byte rule sub-file carries the restriction
flags; this zeroes the exact fields, matching the proven community method + a known-working ROM.

  python black2_nobanlist.py "Pokemon Black 2.nds"            # -> *_nobanlist.nds
  python black2_nobanlist.py game.nds --out custom.nds --inplace
"""
import struct, sys, os, shutil, argparse
def u16(d,o): return struct.unpack_from("<H",d,o)[0]
def u32(d,o): return struct.unpack_from("<I",d,o)[0]

# (offset_in_subfile, expected_original_byte) -> set to 0x00 when it matches.
SIG = [(0x1C,0xC0),(0x29,0x0E),(0x39,0xC0),(0x3A,0x07),(0x46,0x98),(0x47,0x7E),(0x5A,0xD8),(0x5B,0x03)]
PWT = [(0x78,(0x02,)),(0xB5,(0x01,)),(0xB8,(0x01,)),(0xBA,(0x01,0x02,0x03))]  # extra fields in PWT files
PWT_INDICES = {33,34,35,36}  # the four PWT rule sub-files in a/1/0/6 (B2/W2)

def find_a106(buf):
    """Return absolute offset of narc a/1/0/6 file data + its length, by walking the NitroFS."""
    fnt_off=u32(buf,0x40); fat_off=u32(buf,0x48)
    fnt=buf[fnt_off:fnt_off+u32(buf,0x44)]
    def fat(fid): return struct.unpack_from("<II",buf,fat_off+fid*8)
    def de(did):
        idx=did&0xFFF; so=u32(fnt,idx*8); fid=u16(fnt,idx*8+4); p=so; out={}
        while True:
            t=fnt[p]; p+=1
            if t==0: break
            ln=t&0x7F; nm=fnt[p:p+ln].decode("ascii","replace"); p+=ln
            if t&0x80: out[nm]=("dir",u16(fnt,p)); p+=2
            else: out[nm]=("file",fid); fid+=1
        return out
    cur=0xF000
    for part in "a/1/0/6".split("/"):
        e=de(cur)
        if part not in e: raise SystemExit("a/1/0/6 not found -- is this a Black2/White2 .nds?")
        typ,val=e[part]
        if typ=="file": s,en=fat(val); return s,en-s
        cur=val
    raise SystemExit("a/1/0/6 is not a file")

def narc_fat(buf, base):
    if buf[base:base+4]!=b"NARC": raise SystemExit("a/1/0/6 is not a NARC")
    p=base+0x10; assert buf[p:p+4]==b"BTAF"; cnt=u16(buf,p+8); ent=p+0xC
    fat=[(u32(buf,ent+i*8),u32(buf,ent+i*8+4)) for i in range(cnt)]
    p+=u32(buf,p+4); p+=u32(buf,p+4); img=p+8   # skip BTAF, BTNF; GMIF data (absolute)
    return img, fat

def patch(buf):
    a_off,a_len=find_a106(buf)
    img,fat=narc_fat(buf,a_off)
    n_files=0; n_bytes=0
    for idx,(s,e) in enumerate(fat):
        if e-s!=188: continue
        base=img+s
        if not all(buf[base+o]==v for o,v in SIG): continue   # only true banlist sub-files
        for o,v in SIG:
            if buf[base+o]==v: buf[base+o]=0; n_bytes+=1
        if idx in PWT_INDICES:                # PWT files carry extra clause/limit fields
            for o,vals in PWT:
                if buf[base+o] in vals: buf[base+o]=0; n_bytes+=1
        n_files+=1
    return n_files,n_bytes

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("nds"); ap.add_argument("--out"); ap.add_argument("--inplace",action="store_true")
    a=ap.parse_args()
    if not os.path.exists(a.nds): raise SystemExit("file not found: "+a.nds)
    out = a.nds if a.inplace else (a.out or os.path.splitext(a.nds)[0]+"_nobanlist.nds")
    if not a.inplace and out!=a.nds: shutil.copyfile(a.nds,out)
    with open(out,"r+b") as f:
        buf=bytearray(f.read())
        nf,nb=patch(buf)
        if nf==0: raise SystemExit("[abort] no banlist sub-files matched -- unmodified B2/W2 .nds expected.")
        f.seek(0); f.write(buf)
    print(f"[done] zeroed {nb} restriction bytes across {nf} rule sub-files in a/1/0/6 -> {os.path.basename(out)}")
    print("[note] removes Subway+PWT banlist, Soul Dew, item/species clause, and the >3-Pokemon limit.")
if __name__=="__main__": main()
