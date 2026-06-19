#!/usr/bin/env python3
r"""
Omega Ruby / Alpha Sapphire -- remove the Battle Maison ban list.
Zeroes the banned-Pokemon records in the Battle Maison rule GARC (RomFS file "0", a CRAG of
size 0x8170), then rebuilds the affected RomFS IVFC hash slot(s) + the NCCH RomFS hash.
Works on a decrypted .cia OR .3ds (NCSD). Pure standard library.

  python oras_nobanlist.py "Pokemon Alpha Sapphire.3ds"
  python oras_nobanlist.py game.cia --inplace
"""
import struct, hashlib, os, shutil, sys, argparse
def u16(d,o): return struct.unpack_from("<H",d,o)[0]
def u32(d,o): return struct.unpack_from("<I",d,o)[0]
def u64(d,o): return struct.unpack_from("<Q",d,o)[0]
def align(x,a): return (x+a-1)&~(a-1)

GARC_SIZE=0x8170
HEAD=bytes.fromhex("435241471c000000fffe00040400000064040000708100005c0200004f544146")          # first 0x20 bytes of the clean GARC (identifier)
ZERO_RUNS=[(38,39),(44,44),(48,48),(52,52),(56,56),(60,60),(64,64),(68,68),(72,72),(76,76),(80,80),(84,84),(88,88),(92,92),(96,96),(100,100),(105,105),(108,109),(112,113),(116,117),(120,121),(124,125),(128,129),(132,133),(136,137),(140,141),(144,145),(148,149),(152,153),(156,157),(160,161),(164,165),(169,169),(172,173),(176,177),(180,181),(184,185),(188,189),(192,193),(196,197),(200,201),(204,205),(208,209),(212,213),(216,217),(220,221),(224,225),(228,229),(233,233),(236,237),(240,241),(244,245),(248,249),(252,255),(4172,4172),(4185,4185),(4201,4202),(4214,4215),(4234,4235),(4243,4244),(4776,4776),(4789,4789),(4805,4806),(4818,4819),(4838,4839),(4847,4848),(5380,5380),(5393,5393),(5409,5410),(5422,5423),(5442,5443),(5451,5452),(5984,5984),(5997,5997),(6013,6014),(6026,6027),(6046,6047),(6055,6056),(6588,6588),(6601,6601),(6617,6618),(6630,6631),(6650,6651),(6659,6660),(12628,12628),(12641,12641),(12657,12658),(12670,12671),(12690,12691),(12699,12700),(12894,12894),(13232,13232),(13245,13245),(13261,13262),(13274,13275),(13294,13295),(13303,13304),(13498,13498),(13836,13836),(13849,13849),(13865,13866),(13878,13879),(13898,13899),(13907,13908),(14102,14102),(14440,14440),(14453,14453),(14469,14470),(14482,14483),(14502,14503),(14511,14512),(14706,14706),(15044,15044),(15057,15057),(15073,15074),(15086,15087),(15106,15107),(15115,15116),(15310,15310),(15648,15648),(15661,15661),(15677,15678),(15690,15691),(15710,15711),(15719,15720),(15914,15914),(16028,16028),(16252,16252),(16265,16265),(16281,16282),(16294,16295),(16314,16315),(16323,16324),(16518,16518),(16856,16856),(16869,16869),(16885,16886),(16898,16899),(16918,16919),(16927,16928),(17122,17122),(17460,17460),(17473,17473),(17489,17490),(17502,17503),(17522,17523),(17531,17532),(17726,17726),(18064,18064),(18077,18077),(18093,18094),(18106,18107),(18126,18127),(18135,18136),(18330,18330),(18668,18668),(18681,18681),(18697,18698),(18710,18711),(18730,18731),(18739,18740),(18934,18934),(19272,19272),(19285,19285),(19301,19302),(19314,19315),(19334,19335),(19343,19344),(19538,19538),(19876,19876),(19889,19889),(19905,19906),(19918,19919),(19938,19939),(19947,19948),(20142,20142),(20480,20480),(20493,20493),(20509,20510),(20522,20523),(20542,20543),(20551,20552),(20746,20746),(21084,21084),(21097,21097),(21113,21114),(21126,21127),(21146,21147),(21155,21156),(21350,21350),(21688,21688),(21701,21701),(21717,21718),(21730,21731),(21750,21751),(21759,21760),(21954,21954),(22292,22292),(22305,22305),(22321,22322),(22334,22335),(22354,22355),(22363,22364),(22558,22558),(22896,22896),(22909,22909),(22925,22926),(22938,22939),(22958,22959),(22967,22968),(23162,23162),(23500,23500),(23513,23513),(23529,23530),(23542,23543),(23562,23563),(23571,23572),(23766,23766),(27124,27124),(27137,27137),(27153,27154),(27166,27167),(27186,27187),(27195,27196),(27728,27728),(27741,27741),(27757,27758),(27770,27771),(27790,27791),(27799,27800),(28332,28332),(28345,28345),(28361,28362),(28374,28375),(28394,28395),(28403,28404),(28936,28936),(28949,28949),(28965,28966),(28978,28979),(28998,28999),(29007,29008),(29540,29540),(29553,29553),(29569,29570),(29582,29583),(29602,29603),(29611,29612)]                     # (start,end) inclusive, file-relative, set to 0x00
BLOCK=0x1000; HSZ=0x20; PER=BLOCK//HSZ

def locate(f):
    """Return (content_off, romfs_abs, master_size, l3, fileDataOff) for a .cia or .3ds."""
    f.seek(0); sig=f.read(0x200)
    if sig[0x100:0x104]==b"NCSD":
        ncch_off=u32(sig,0x120)*0x200; content_off=None
    else:
        f.seek(0); head=f.read(0x2020); _hs,_t,_v,cert,tk,tmd,meta=struct.unpack_from("<IHHIIII",head,0)
        content_off=align(align(align(align(0x2020,64)+cert,64)+tk,64)+tmd,64); ncch_off=content_off
    f.seek(ncch_off); ncch=f.read(0x200)
    if ncch[0x100:0x104]!=b"NCCH": raise SystemExit("NCCH not found (decrypted .cia/.3ds required).")
    romfs_abs=ncch_off+u32(ncch,0x1B0)*0x200
    f.seek(romfs_abs); ivfc=f.read(0x60)
    if ivfc[:4]!=b"IVFC": raise SystemExit("RomFS IVFC not found.")
    master=u32(ivfc,8); l3=romfs_abs+align(0x60+master,0x1000)
    f.seek(l3); h=struct.unpack("<IIIIIIIIII",f.read(0x28))
    return dict(ncch_off=ncch_off, content_off=content_off, romfs_abs=romfs_abs,
                master=master, l3=l3, fileMetaOff=h[7], fileMetaSz=h[8], fileDataOff=h[9])

def find_garc(f, info):
    f.seek(info["l3"]+info["fileMetaOff"]); meta=f.read(info["fileMetaSz"]); p=0
    while p+0x20<=len(meta):
        doff=u64(meta,p+8); dsz=u64(meta,p+0x10); nlen=u32(meta,p+0x1C)
        nm=meta[p+0x20:p+0x20+nlen].decode("utf-16le","replace")
        if nm=="0" and dsz==GARC_SIZE:
            abs_off=info["l3"]+info["fileDataOff"]+doff
            f.seek(abs_off); 
            if f.read(0x20)==HEAD: return abs_off
        p+=0x20+align(nlen,4)
    return None

def cms(f, info, k):
    l1=bytearray()
    for jb in range(k*PER,(k+1)*PER):
        f.seek(info["l3"]+jb*PER*BLOCK); data=f.read(PER*BLOCK); l2=bytearray()
        for n in range(PER):
            blk=data[n*BLOCK:(n+1)*BLOCK]
            if len(blk)<BLOCK: blk=blk+b"\x00"*(BLOCK-len(blk))
            l2+=hashlib.sha256(blk).digest()
        l1+=hashlib.sha256(bytes(l2)).digest()
    return hashlib.sha256(bytes(l1)).digest()

def main():
    ap=argparse.ArgumentParser(); ap.add_argument("rom"); ap.add_argument("--out"); ap.add_argument("--inplace",action="store_true")
    a=ap.parse_args()
    if not os.path.exists(a.rom): raise SystemExit("file not found: "+a.rom)
    out=a.rom if a.inplace else (a.out or os.path.splitext(a.rom)[0]+"_nobanlist"+os.path.splitext(a.rom)[1])
    if not a.inplace and out!=a.rom:
        print("[copy] duplicating ROM (%.2f GB)..."%(os.path.getsize(a.rom)/1e9)); shutil.copyfile(a.rom,out)
    with open(out,"r+b") as f:
        info=locate(f); g=find_garc(f,info)
        if g is None: raise SystemExit("[abort] Battle Maison GARC not found -- unmodified OR/AS expected.")
        f.seek(g); buf=bytearray(f.read(GARC_SIZE))
        nb=0
        for s,e in ZERO_RUNS:
            for j in range(s,e+1):
                if buf[j]!=0: buf[j]=0; nb+=1
        f.seek(g); f.write(buf)
        slots=sorted({((g-info["l3"])//BLOCK)//PER//PER, ((g+GARC_SIZE-1-info["l3"])//BLOCK)//PER//PER})
        for k in slots:
            h=cms(f,info,k); f.seek(info["romfs_abs"]+0x60+k*HSZ); f.write(h)
        f.flush()
        f.seek(info["romfs_abs"]); ipm=f.read(0x60+info["master"])
        if info["content_off"] is not None:   # .cia: refresh NCCH romfs hash
            f.seek(info["ncch_off"]+0x1E0); f.write(hashlib.sha256(ipm).digest())
        else:                                  # .3ds: NCCH hash is at ncch_off+0x1E0 too
            f.seek(info["ncch_off"]+0x1E0); f.write(hashlib.sha256(ipm).digest())
        f.flush()
    print(f"[done] zeroed {nb} banlist bytes; refreshed IVFC slot(s) {slots} + NCCH hash -> {os.path.basename(out)}")
    print("[note] removes the Battle Maison banned-Pokemon list. Test in a fresh battle, not a save state.")
if __name__=="__main__": main()
