#!/usr/bin/env python3
"""Convert a decrypted .cia to a .3ds (NCSD/CCI) by extracting content0 (the game NCCH) and
wrapping it in a single-partition NCSD header (fields mirrored from a real Ultra-Sun/Moon-class
.3ds). For Citra/Azahar 'Load File' from a folder. Streams the 3.7GB content (no big RAM use)."""
import sys, struct, shutil
sys.path.insert(0,"Arceus_Unnerf_Output/Un-Nerf-Compendium/Un-Nerf-Compendium-main/gen67_formepersist")
import formepersist as fp

NCSD_FLAGS = bytes.fromhex("0000000201020000")   # from a real .3ds (media-unit 0x200, no crypto)

def convert(cia, out3ds):
    f=open(cia,"rb")
    info=fp.locate(f)
    if not info["is_cia"]:
        print("not a CIA:",cia); return
    ncch_off=info["ncch"]
    # content0 size from TMD
    f.seek(info["tmd_off"]); td=f.read(info["tmd_sz"])
    base=0x140+0xC4+64*0x24
    c0=struct.unpack_from(">Q",td,base+0x08)[0]
    # title id from NCCH header
    f.seek(ncch_off); ncch_hdr=f.read(0x200)
    assert ncch_hdr[0x100:0x104]==b"NCCH","content0 not a decrypted NCCH"
    tid=struct.unpack_from("<Q",ncch_hdr,0x118)[0]
    if c0 % 0x200: c0 = (c0 + 0x1FF) & ~0x1FF
    ncch_mu=c0//0x200
    # build NCSD header (0x4000)
    hdr=bytearray(0x4000)
    hdr[0x100:0x104]=b"NCSD"
    struct.pack_into("<I",hdr,0x104, 0x20+ncch_mu)        # image size (media units)
    struct.pack_into("<Q",hdr,0x108, tid)                 # media id
    struct.pack_into("<II",hdr,0x120, 0x20, ncch_mu)      # partition 0: off 0x20 mu, len
    hdr[0x188:0x190]=NCSD_FLAGS
    struct.pack_into("<Q",hdr,0x190, tid)                 # partition 0 id
    # write header + stream NCCH
    o=open(out3ds,"wb"); o.write(hdr)
    f.seek(ncch_off); rem=c0
    while rem>0:
        b=f.read(min(64<<20,rem))
        if not b: break
        o.write(b); rem-=len(b)
    o.close(); f.close()
    print("wrote %s  (title 0x%016X, NCCH %d bytes)"%(out3ds,tid,c0))
    # verify
    g=open(out3ds,"rb"); h=g.read(0x200)
    off=struct.unpack_from("<I",h,0x120)[0]*0x200; ln=struct.unpack_from("<I",h,0x124)[0]*0x200
    g.seek(off); pm=g.read(0x104)[0x100:0x104]
    print("  verify: NCSD magic=%r  part0 @0x%X len=0x%X  part0 magic=%r"%(h[0x100:0x104],off,ln,pm))
    g.close()

if __name__=="__main__":
    convert("Arceus_Unnerf_Output/patched_roms/UltraMoon_ProteanArceusTest.cia",
            "Arceus_Unnerf_Output/patched_roms/UltraMoon_ProteanArceusTest.3ds")
