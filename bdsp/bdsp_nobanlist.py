#!/usr/bin/env python3
r"""
BDSP (Brilliant Diamond / Shining Pearl) -- remove the Battle Tower banned-Pokemon list.

BDSP reuses the Gen-7 banlist bit-string (one bit per Pokemon, banned = 1), stored as a
constant byte array in the IL2CPP **global-metadata.dat** (per ABZB / isleep2late's research).
This finds every copy of that ban record (one per battle mode) and zeroes the banned bits,
keeping the file size identical -- exactly what doing it by hand in Imposter's Ordeal does.

You supply your own dumped global-metadata.dat (romfs path:
 .../Data/Managed/Metadata/global-metadata.dat). The output is a modded copy to drop into your
emulator's LayeredFS mod folder (see apply_bdsp_nobanlist.bat). Does NOT touch species/item
clause (still in progress).

  python bdsp_nobanlist.py global-metadata.dat            # -> global-metadata_nobanlist.dat
"""
import os, sys, shutil, argparse
# The verified Gen-7 ban record (also used in USUM a/1/4/1 and ORAS) -- part A then part B.
# Non-zero bytes are the actual bans; zero them to unban while keeping length.
REC_A = bytes.fromhex("c00000000000000000000000000e000000000000000000000000000000c0070000000000000000000000987e")
REC_B = bytes.fromhex("d80300000000000000f00300000000000000e00187")
# shorter, highly-distinctive anchor (gen5/6 legendary bans) in case BDSP truncated the record:
ANCHOR = bytes.fromhex("c0070000000000000000000000987e")

def zero_bans(buf):
    """Zero every ban byte of every ban record found. Returns (records, bytes_zeroed)."""
    nrec=0; nby=0; i=0
    # Prefer full-record matches; fall back to anchor matches.
    def zero_run(start, length):
        nonlocal nby
        c=0
        for j in range(start, start+length):
            if buf[j]!=0: buf[j]=0; c+=1
        return c
    # full part-A records
    o=0
    while True:
        k=buf.find(REC_A,o)
        if k<0: break
        nrec+=1; nby+=zero_run(k,len(REC_A))
        # part B sometimes follows within a short window
        b=buf.find(REC_B,k+len(REC_A),k+len(REC_A)+0x40)
        if b>=0: nby+=zero_run(b,len(REC_B))
        o=k+1
    if nrec==0:   # truncated/variant layout -> use anchor, zero a conservative window around it
        o=0
        while True:
            k=buf.find(ANCHOR,o)
            if k<0: break
            nrec+=1; nby+=zero_run(max(0,k-0x22), 0x22+len(ANCHOR)+2)  # the C0../0E.. before + 98 7E
            o=k+1
    return nrec,nby

def main():
    ap=argparse.ArgumentParser(); ap.add_argument("metadata"); ap.add_argument("--out"); ap.add_argument("--inplace",action="store_true")
    a=ap.parse_args()
    if not os.path.exists(a.metadata): raise SystemExit("file not found: "+a.metadata)
    out=a.metadata if a.inplace else (a.out or os.path.splitext(a.metadata)[0]+"_nobanlist"+os.path.splitext(a.metadata)[1])
    if not a.inplace and out!=a.metadata: shutil.copyfile(a.metadata,out)
    with open(out,"r+b") as f:
        buf=bytearray(f.read()); rec,nby=zero_bans(buf)
        if rec==0: raise SystemExit("[abort] ban record not found -- is this BDSP global-metadata.dat? Nothing written.")
        f.seek(0); f.write(buf)
    print(f"[done] cleared {rec} ban record(s), zeroed {nby} ban bytes (size unchanged) -> {os.path.basename(out)}")
    print("[note] removes the Battle Tower banned-Pokemon list. Does NOT remove the species/item clause (in progress).")
    print("[note] place the output in your emulator's LayeredFS mod folder (see apply_bdsp_nobanlist.bat).")
if __name__=="__main__": main()
