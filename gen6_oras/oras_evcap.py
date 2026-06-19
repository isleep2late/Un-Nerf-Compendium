#!/usr/bin/env python3
r"""
Omega Ruby / Alpha Sapphire -- remove the 510 EV-total cap (Battle Maison / facility eligibility).

ORAS runs a per-Pokemon eligibility check when you pick a facility team. That function
(code vaddr 0x1E95A0) flags a Pokemon "ineligible" if any EV > 252, the EV TOTAL > 510, any
IV > 31, or a move is illegal. The EV-total comparison loads 510 from a literal at vaddr
**0x1E9734**; a 510+ mon gets filtered out ("you do not have three eligible Pokemon").

This raises that cap to 1530 (> the 6x252 = 1512 maximum), so a maxed-EV mon is fully eligible.
It also bumps the separate Battle-Spot legality validator's cap (literal @ **0x4474B8**) for
consistency -- that one governs the online/Spot path, harmless for single-player but tidy to lift too.

Patches the executable (ExeFS ".code"), NOT the RomFS, then fixes the ExeFS + NCCH hashes (and the
CIA TMD hashes so a .cia still installs). Works on a decrypted .cia OR .3ds (NCSD). Original file is
left untouched; a new *_evcap file is written. Pure standard library, self-verifying.

  python oras_evcap.py "Pokemon Alpha Sapphire.3ds"
  python oras_evcap.py game.cia --inplace
"""
import struct, hashlib, os, shutil, sys, argparse

OLD, NEW = 510, 1530
CODE_VBASE = 0x100000
ELIG_VADDR = 0x1E9734          # THE Maison/facility eligibility EV-total cap (identical in OR and AS)
BATTLESPOT = {                 # Battle-Spot legality validator cap — offset differs per title
    0x000400000011C500: 0x4474B8,   # Alpha Sapphire
    0x000400000011C400: 0x4474C0,   # Omega Ruby
}
def u32(d, o): return struct.unpack_from("<I", d, o)[0]
def align(x, a): return (x + a - 1) & ~(a - 1)

def locate(f):
    """Return dict with content0 (NCCH) absolute offset, plus CIA TMD offsets if it's a .cia."""
    f.seek(0); sig = f.read(0x200)
    if sig[0x100:0x104] == b"NCSD":
        ncch = u32(sig, 0x120) * 0x200
        return dict(ncch=ncch, is_cia=False)
    f.seek(0); head = f.read(0x2020)
    hs, _t, _v, cert, tik, tmd, meta = struct.unpack_from("<IHHIIII", head, 0)
    a = align
    tmd_off = a(a(a(a(0x2020, 64) + cert, 64) + tik, 64), 64)  # = align(align(align(hdr)+cert)+tik) ... see below
    # robust: walk the CIA sections in order
    cert_off = a(hs, 64); tik_off = a(cert_off + cert, 64); tmd_off = a(tik_off + tik, 64)
    cont_off = a(tmd_off + tmd, 64)
    return dict(ncch=cont_off, is_cia=True, tmd_off=tmd_off, tmd_sz=tmd)

def exefs_code_region(f, ncch):
    f.seek(ncch); h = f.read(0x200)
    if h[0x100:0x104] != b"NCCH":
        raise SystemExit("NCCH not found (decrypted .cia/.3ds required).")
    exo = u32(h, 0x1A0) * 0x200
    exhr = u32(h, 0x1A8) * 0x200          # NCCH ExeFS hash-region size (0x1A8; 0x1AC is reserved)
    title_id = struct.unpack_from("<Q", h, 0x118)[0]
    f.seek(ncch + exo); eh = bytearray(f.read(0x200))
    cfo = cfsz = None
    for i in range(10):
        if eh[i*0x10:i*0x10+8].rstrip(b"\0") == b".code":
            cfo, cfsz = struct.unpack_from("<II", eh, i*0x10+8)
    if cfo is None:
        raise SystemExit(".code not found in ExeFS.")
    return dict(exo=exo, exhr=exhr, eh=eh, code_abs=ncch + exo + 0x200 + cfo, code_sz=cfsz,
                title_id=title_id)

def ev_vaddrs(cr):
    """Eligibility cap (always) + the title-specific Battle-Spot cap if we know its offset."""
    vas = [ELIG_VADDR]
    bs = BATTLESPOT.get(cr["title_id"])
    if bs is not None: vas.append(bs)
    return vas

def sha_region(f, off, size):
    h = hashlib.sha256(); f.seek(off); rem = size
    while rem > 0:
        b = f.read(min(8 << 20, rem))
        if not b: break
        h.update(b); rem -= len(b)
    return h.digest()

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("rom"); ap.add_argument("--out"); ap.add_argument("--inplace", action="store_true")
    ap.add_argument("--verify", action="store_true", help="report current literal values, write nothing")
    a = ap.parse_args()
    if not os.path.exists(a.rom): raise SystemExit("file not found: " + a.rom)

    if a.verify:
        with open(a.rom, "rb") as f:
            info = locate(f); cr = exefs_code_region(f, info["ncch"])
            print(f"  title id = {cr['title_id']:016X}")
            for va in ev_vaddrs(cr):
                f.seek(cr["code_abs"] + (va - CODE_VBASE))
                print(f"  vaddr 0x{va:X} = {struct.unpack('<I', f.read(4))[0]}")
        return

    out = a.rom if a.inplace else (a.out or os.path.splitext(a.rom)[0] + "_evcap" + os.path.splitext(a.rom)[1])
    if not a.inplace and out != a.rom:
        print("[copy] duplicating ROM (%.2f GB)..." % (os.path.getsize(a.rom) / 1e9))
        shutil.copyfile(a.rom, out)

    with open(out, "r+b") as f:
        info = locate(f); cr = exefs_code_region(f, info["ncch"])
        changed = 0
        for va in ev_vaddrs(cr):
            pos = cr["code_abs"] + (va - CODE_VBASE)
            f.seek(pos); cur = struct.unpack("<I", f.read(4))[0]
            if cur == NEW:
                print(f"  0x{va:X}: already {NEW}")
            elif cur == OLD:
                f.seek(pos); f.write(struct.pack("<I", NEW)); changed += 1
                print(f"  0x{va:X}: {OLD} -> {NEW}")
            else:
                print(f"  0x{va:X}: = {cur} (unexpected; SKIP -- is this unmodified ORAS?)")
        # --- ExeFS .code hash (last of 8 reverse-order slots) + NCCH ExeFS-superblock hash ---
        code_hash = sha_region(f, cr["code_abs"], cr["code_sz"])
        eh = cr["eh"]; eh[0x200-0x20:0x200] = code_hash
        f.seek(info["ncch"] + cr["exo"]); f.write(bytes(eh))
        sup = sha_region(f, info["ncch"] + cr["exo"], cr["exhr"])
        f.seek(info["ncch"] + 0x1C0); f.write(sup)
        # --- CIA only: refresh TMD content0 hash + info-record hash + header info hash (so it installs) ---
        if info["is_cia"]:
            f.seek(info["tmd_off"]); td = bytearray(f.read(info["tmd_sz"]))
            th = 0x140
            cc = struct.unpack_from(">H", td, th + 0x9E)[0]
            base = th + 0xC4 + 64 * 0x24
            # content0 chunk hash = sha256 over the whole content0 NCCH
            c0_size = struct.unpack_from(">Q", td, base + 0x08)[0]
            print("[cia] rehashing content0 for the TMD (%.2f GB, one-time)..." % (c0_size / 1e9))
            c0_hash = sha_region(f, info["ncch"], c0_size)
            td[base + 0x10:base + 0x30] = c0_hash
            # content-info record 0 hash (over its chunk records) + TMD header info-records hash
            ci = th + 0xC4
            idx_off, cmd_cnt = struct.unpack_from(">HH", td, ci)
            td[ci + 0x04:ci + 0x24] = hashlib.sha256(bytes(td[base + idx_off*0x30: base + (idx_off+max(cmd_cnt,1))*0x30])).digest()
            td[th + 0xA4:th + 0xC4] = hashlib.sha256(bytes(td[th + 0xC4: th + 0xC4 + 64*0x24])).digest()
            f.seek(info["tmd_off"]); f.write(bytes(td))
        f.flush()
        # --- verify ---
        for va in EV_VADDRS:
            f.seek(cr["code_abs"] + (va - CODE_VBASE))
            v = struct.unpack("<I", f.read(4))[0]
            print(f"  VERIFY 0x{va:X} = {v} {'OK' if v == NEW else 'FAILED'}")
    print(f"[done] EV cap lifted (510 -> {NEW}); fixed ExeFS+NCCH"
          + (" + TMD" if info['is_cia'] else "") + f" hashes -> {os.path.basename(out)}")
    print("[note] 510-EV mons are now eligible in the Battle Maison. Test in a fresh battle, not a save state.")
    if not info["is_cia"]:
        print("[note] .3ds: load via File > Load File. Saves are per-Title-ID, so your progress carries over.")
if __name__ == "__main__":
    main()
