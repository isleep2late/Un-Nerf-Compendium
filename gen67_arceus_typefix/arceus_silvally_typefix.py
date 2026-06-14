#!/usr/bin/env python3
"""
Arceus / Silvally form-driven typing  (UnNerf Compendium)
=========================================================
Gen 6/7: Arceus (#493) and Silvally (#773) have FormStatsIndex = 0 in the personal table,
so EVERY form shares the base Normal/Normal entry — the form is cosmetic and the battle type
comes only from Multitype/RKS-System reading the held Plate/Memory. A form set in PKHeX without
the matching Plate is therefore Normal-typed ("looks the part, plays Normal").

This tool gives both species real per-form personal entries. Their form index equals the gen-6/7
internal type id (0 Normal, 1 Fighting, ... 7 Ghost, ... 17 Fairy), so each new form entry is the
base entry with Type1 = Type2 = formIndex (an identity map — no table). It then repoints each
base entry's FormStatsIndex to the new entries. The engine already loads personal data by form
(`pml::personal::LoadPersonalData(MonsNo, form)`), so a form-set Arceus/Silvally now types itself
even with no Plate held; holding the matching Plate still works (Multitype overrides as usual).

INPUT  : a decrypted, EXTRACTED personal GARC (RomFS `a/0/1/7`) — pull it with pk3DS/pkNX or 3dstool.
OUTPUT : a rebuilt GARC, byte-identical for all original entries + 17 new typed entries/species.
REPACK : import the output back into RomFS and rebuild (pk3DS "RomFS" / pkNX), or 3dstool, then
         recompute the NCCH RomFS-superblock hash (+ CIA TMD) — i.e. a normal RomFS rebuild. This
         is a LENGTH-CHANGING edit (+17*84 bytes/species), so it needs a real RomFS rebuild, not a
         byte patch.

USAGE  : python arceus_silvally_typefix.py  path/to/a_0_1_7  [-o OUT] [--species 493 773] [--verify]
Works for ORAS and USUM (identical personal layout). Pure standard library.
"""
import struct, sys, argparse

TYPES=["Normal","Fighting","Flying","Poison","Ground","Rock","Bug","Ghost","Steel",
       "Fire","Water","Grass","Electric","Psychic","Ice","Dragon","Dark","Fairy"]
ENTRY=0x54; T1=0x06; T2=0x07; FSI=0x1C; FCNT=0x20

def parse(d):
    assert d[:4]==b"CRAG","not a GARC (CRAG)"; hsize=struct.unpack_from("<I",d,4)[0]
    o=hsize; assert d[o:o+4]==b"OTAF"; fato_size=struct.unpack_from("<I",d,o+4)[0]; o+=fato_size
    assert d[o:o+4]==b"BTAF"; fatb_size=struct.unpack_from("<I",d,o+4)[0]
    fcount=struct.unpack_from("<I",d,o+8)[0]; p=o+12; files=[]
    for i in range(fcount):
        flags=struct.unpack_from("<I",d,p)[0]; p+=4; subs=[]
        for b in range(32):
            if flags&(1<<b): s,e,l=struct.unpack_from("<III",d,p); p+=12; subs.append((s,e,l))
        files.append(subs)
    o+=fatb_size; assert d[o:o+4]==b"BMIF"; data_base=o+0x0C
    blobs=[(d[data_base+subs[0][0]:data_base+subs[0][0]+subs[0][2]] if subs else b"") for subs in files]
    return blobs

def build(d, species):
    blobs=[bytearray(b) for b in parse(d)]
    n=len(blobs); new=[]; idx=n
    for sp in species:
        base=blobs[sp]
        if len(base)<ENTRY: print("  ! #%d has no base entry (%d B), skipping"%(sp,len(base))); continue
        first=idx
        for form in range(1,18):
            e=bytearray(base); e[T1]=form; e[T2]=form
            struct.pack_into("<H",e,FSI,0); e[FCNT]=0
            new.append(bytes(e)); idx+=1
        struct.pack_into("<H",blobs[sp],FSI,first); blobs[sp][FCNT]=18
        print("  #%d FormStatsIndex -> %d (forms 1..17 typed Fighting..Fairy)"%(sp,first))
    allb=[bytes(b) for b in blobs]+new
    # emit GARC v6 (CRAG) matching gen6/7 layout
    data=bytearray(); recs=[]
    for b in allb:
        s=len(data); data+=b; data+=b"\x00"*((-len(data))%4); recs.append((s,s+len(b),len(b)))
    fatb=bytearray(b"BTAF")+struct.pack("<I",0)+struct.pack("<I",len(allb))
    for s,e,l in recs: fatb+=struct.pack("<I",1)+struct.pack("<III",s,e,l)
    struct.pack_into("<I",fatb,4,len(fatb))
    fato=bytearray(b"OTAF")+struct.pack("<I",0)+struct.pack("<H",len(allb))+struct.pack("<H",0xFFFF)
    for i in range(len(allb)): fato+=struct.pack("<I",i*0x10)
    struct.pack_into("<I",fato,4,len(fato))
    fimb=bytearray(b"BMIF")+struct.pack("<I",0x0C)+struct.pack("<I",len(data))
    HS=0x24; data_off=HS+len(fato)+len(fatb)+len(fimb)
    largest=max(len(b) for b in allb); largest_pad=max(((len(b)+3)//4)*4 for b in allb)
    hdr=bytearray(b"CRAG")+struct.pack("<I",HS)+struct.pack("<H",0xFEFF)+struct.pack("<H",0x0600)
    hdr+=struct.pack("<I",4)+struct.pack("<I",data_off)+struct.pack("<I",data_off+len(data))
    hdr+=struct.pack("<I",largest_pad)+struct.pack("<I",largest)+struct.pack("<I",4)
    return bytes(hdr)+bytes(fato)+bytes(fatb)+bytes(fimb)+bytes(data)

def verify(orig_d, out, species):
    a=parse(orig_d); b=parse(out)
    sp=set(species)
    # every original entry must be byte-identical EXCEPT the base entries we repointed
    ok=all(b[i]==a[i] for i in range(len(a)) if i not in sp)
    print("  re-parse: %d files (was %d); all unchanged originals identical: %s"%(len(b),len(a),ok))
    for sp in species:
        e=b[sp]; fsi=struct.unpack_from("<H",e,FSI)[0]
        line=", ".join("%s"%TYPES[b[fsi+f-1][T1]] for f in range(1,18))
        print("  #%d forms 1..17: %s"%(sp,line))
    return ok

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("personal"); ap.add_argument("-o","--out")
    ap.add_argument("--species",type=lambda s:int(s,0),nargs="*",default=[493,773])
    ap.add_argument("--verify",action="store_true")
    a=ap.parse_args()
    d=open(a.personal,"rb").read()
    if a.verify:
        blobs=parse(d)
        for sp in a.species:
            fsi=struct.unpack_from("<H",blobs[sp],FSI)[0]
            print("#%d base Type %s/%s FormStatsIndex=%d"%(sp,TYPES[blobs[sp][T1]],TYPES[blobs[sp][T2]],fsi))
        return
    out=build(d,a.species)
    op=a.out or (a.personal+"_ArceusSilvallyTyped")
    open(op,"wb").write(out); print("wrote",op,len(out),"bytes (orig",len(d),")")
    verify(d,out,a.species)

if __name__=="__main__": main()
