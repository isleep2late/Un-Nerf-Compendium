#!/usr/bin/env python3
"""Gen-5 BW2 Arceus form-driven typing: add per-form personal entries (form index == type id;
gen5 has 17 types, no Fairy) and repoint Arceus FormStatsIndex. Same fix as gen6/7 but in the
NDS personal NARC (rebuilt with ndspy). Produces a patched .nds. Builds on the NO-NERF ROM."""
import sys, struct, ndspy.rom, ndspy.narc
TYPES=["Normal","Fighting","Flying","Poison","Ground","Rock","Bug","Ghost","Steel","Fire",
       "Water","Grass","Electric","Psychic","Ice","Dragon","Dark"]  # gen5: 17 types, no Fairy
ARCEUS=493; T1=0x06; T2=0x07; FSI=0x1C; FCNT=0x20

def find_personal(rom):
    for fid in range(len(rom.files)):
        d=rom.files[fid]
        if len(d)>=4 and d[:4]==b'NARC':
            try:
                n=ndspy.narc.NARC(d)
                if 600<len(n.files)<1000 and len(n.files[ARCEUS])==0x4C:
                    b=n.files[ARCEUS]
                    if b[T1]==0 and b[T2]==0 and b[FCNT]==17:  # Arceus Normal/Normal, 17 forms
                        return fid,n
            except Exception: pass
    return None,None

def build(inpath, outpath):
    rom=ndspy.rom.NintendoDSRom.fromFile(inpath)
    fid,narc=find_personal(rom)
    if fid is None: print("  ! personal narc not found in",inpath); return
    files=[bytearray(f) for f in narc.files]
    n=len(files); base=bytearray(files[ARCEUS])
    fc=base[FCNT]                       # 17 (Normal + 16)
    first=n
    for form in range(1,fc):            # forms 1..16 -> types 1..16
        e=bytearray(base); e[T1]=form; e[T2]=form
        struct.pack_into("<H",e,FSI,0); e[FCNT]=0
        files.append(e)
    struct.pack_into("<H",files[ARCEUS],FSI,first)
    print("  %s: personal NARC fileid %d, Arceus FormStatsIndex 0 -> %d (+%d typed forms)"%(
        inpath.split('/')[-1],fid,first,fc-1))
    narc.files=[bytes(f) for f in files]
    rom.files[fid]=narc.save()
    rom.saveToFile(outpath)
    # verify
    rom2=ndspy.rom.NintendoDSRom.fromFile(outpath)
    n2=ndspy.narc.NARC(rom2.files[fid]); b=n2.files[ARCEUS]
    f2=struct.unpack_from("<H",b,FSI)[0]
    line=", ".join(TYPES[n2.files[f2+k-1][T1]] for k in range(1,fc))
    print("    verify -> Arceus forms 1..%d: %s"%(fc-1,line))
    print("    wrote",outpath)

if __name__=="__main__":
    build("POKEMON NO NERFS/Black2_UNNERFED.nds","Arceus_Unnerf_Output/patched_roms/Black2_UNNERFED_ArceusTyped.nds")
    build("POKEMON NO NERFS/White2_UNNERFED.nds","Arceus_Unnerf_Output/patched_roms/White2_UNNERFED_ArceusTyped.nds")
