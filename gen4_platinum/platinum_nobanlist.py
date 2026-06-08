#!/usr/bin/env python3
r"""
Pokemon Platinum -- remove the Battle Frontier banned-species list.
The banlist is a u16 array of banned species IDs in arm9 (Mewtwo, Mew, the box legends, etc.);
this zeroes them so every species can enter the Frontier. Pure stdlib, direct byte edit
(NDS has no hash tree). Use a decrypted Platinum .nds you dumped yourself.

  python platinum_nobanlist.py "Pokemon Platinum.nds"      # -> *_nobanlist.nds
"""
import struct, os, sys, shutil, argparse
def u32(d,o): return struct.unpack_from("<I",d,o)[0]
# arm9-relative (offset, original_hex, new_hex)
ARM9_EDITS = [
    (0xF05BE,"96","00"),(0xF05C0,"97","00"),(0xF05C2,"f9","00"),(0xF05C4,"fa","00"),(0xF05C6,"fb","00"),
    (0xF05C8,"7e017f01800181018201e301e401e701e901ea01eb01ec01ed01",
             "0000000000000000000000000000000000000000000000000000"),
]
def patch(buf):
    a9o=u32(buf,0x20)
    changed=0
    for off,oh,nh in ARM9_EDITS:
        o=a9o+off; old=bytes.fromhex(oh); new=bytes.fromhex(nh)
        cur=bytes(buf[o:o+len(old)])
        if cur==new: continue
        if cur!=old: raise SystemExit(f"[abort] unexpected arm9 bytes at 0x{o:X} ({cur.hex()} != {old.hex()}); is this an unmodified Platinum .nds?")
        buf[o:o+len(new)]=new; changed+=len(new)
    return changed
def main():
    ap=argparse.ArgumentParser(); ap.add_argument("nds"); ap.add_argument("--out"); ap.add_argument("--inplace",action="store_true")
    a=ap.parse_args()
    if not os.path.exists(a.nds): raise SystemExit("file not found: "+a.nds)
    out=a.nds if a.inplace else (a.out or os.path.splitext(a.nds)[0]+"_nobanlist.nds")
    if not a.inplace and out!=a.nds: shutil.copyfile(a.nds,out)
    with open(out,"r+b") as f:
        buf=bytearray(f.read()); n=patch(buf); f.seek(0); f.write(buf)
    print(f"[done] zeroed {n} banlist bytes in arm9 -> {os.path.basename(out)}")
    print("[note] removes the Battle Frontier banned-species list. (Form-restriction removal is a separate, experimental script edit.)")
if __name__=="__main__": main()
