#!/usr/bin/env python3
import argparse, hashlib, os, struct, zlib

MU = 0x200
TITLES = {
    0x0004000000164800: "Pokemon Sun",
    0x0004000000175E00: "Pokemon Moon",
}
CHUNK = 64 << 20


def u32(b, o): return struct.unpack_from("<I", b, o)[0]
def u64(b, o): return struct.unpack_from("<Q", b, o)[0]
def align(x, a): return (x + a - 1) & ~(a - 1)
def crc(b): return zlib.crc32(b) & 0xFFFFFFFF


class PatchError(Exception):
    pass


def bps_apply(patch, source):
    if patch[:4] != b"BPS1":
        raise PatchError("not a BPS patch")
    if crc(patch[:-4]) != u32(patch, len(patch) - 4):
        raise PatchError("BPS patch file is damaged (patch CRC32 mismatch)")
    pos = 4

    def num():
        nonlocal pos
        data, shift = 0, 1
        while True:
            x = patch[pos]; pos += 1
            data += (x & 0x7F) * shift
            if x & 0x80:
                return data
            shift <<= 7
            data += shift

    ssize, tsize, msize = num(), num(), num()
    pos += msize
    src_crc, tgt_crc = u32(patch, len(patch) - 12), u32(patch, len(patch) - 8)
    if len(source) != ssize or crc(source) != src_crc:
        raise PatchError("this file is not the one the patch was made for (size %d CRC32 %08X, expected size %d CRC32 %08X)"
                         % (len(source), crc(source), ssize, src_crc))
    out = bytearray(tsize)
    op = sro = tro = 0
    end = len(patch) - 12
    while pos < end:
        d = num(); cmd = d & 3; ln = (d >> 2) + 1
        if cmd == 0:
            out[op:op + ln] = source[op:op + ln]; op += ln
        elif cmd == 1:
            out[op:op + ln] = patch[pos:pos + ln]; pos += ln; op += ln
        elif cmd == 2:
            d = num(); sro += -(d >> 1) if d & 1 else (d >> 1)
            out[op:op + ln] = source[sro:sro + ln]; sro += ln; op += ln
        else:
            d = num(); tro += -(d >> 1) if d & 1 else (d >> 1)
            for _ in range(ln):
                out[op] = out[tro]; op += 1; tro += 1
    if op != tsize or crc(out) != tgt_crc:
        raise PatchError("BPS result failed its CRC32 check")
    return bytes(out)


def ips_apply(patch, source):
    if patch[:5] != b"PATCH":
        raise PatchError("not an IPS patch")
    out = bytearray(source); i = 5
    while patch[i:i + 3] != b"EOF":
        off = int.from_bytes(patch[i:i + 3], "big"); sz = int.from_bytes(patch[i + 3:i + 5], "big"); i += 5
        if sz == 0:
            rl = int.from_bytes(patch[i:i + 2], "big"); data = bytes([patch[i + 2]]) * rl; i += 3
        else:
            data = patch[i:i + sz]; i += sz
        if off + len(data) > len(out):
            raise PatchError("IPS record writes past the end of the file")
        out[off:off + len(data)] = data
    return bytes(out)


def apply_patch(path, source):
    data = open(path, "rb").read()
    out = bps_apply(data, source) if path.endswith(".bps") else ips_apply(data, source)
    if len(out) != len(source):
        raise PatchError("%s changes the file size; only same-size patches are supported" % path)
    return out


def blz_decompress(data):
    top = u32(data, len(data) - 8); extra = u32(data, len(data) - 4)
    out = bytearray(len(data) + extra); out[:len(data)] = data
    i = len(data) - (top >> 24); stop = len(data) - (top & 0xFFFFFF); o = len(out)
    while i > stop:
        i -= 1; flags = data[i]
        for _ in range(8):
            if i <= stop:
                break
            if flags & 0x80:
                i -= 2; v = data[i] | (data[i + 1] << 8)
                ln = (v >> 12) + 3; disp = (v & 0xFFF) + 3
                for _ in range(ln):
                    o -= 1; out[o] = out[o + disp]
            else:
                i -= 1; o -= 1; out[o] = data[i]
            flags = (flags << 1) & 0xFF
    return bytes(out)


def pick(base, stem):
    for ext in (".bps", ".ips"):
        if os.path.isfile(base + stem + ext):
            return base + stem + ext
    return None


def load_patch_set(mods_dir):
    code = pick(os.path.join(mods_dir, "exefs", ""), "code")
    romfs = {}
    root = os.path.join(mods_dir, "romfs_ext")
    for dp, _dn, files in os.walk(root):
        for fn in sorted(files):
            if not fn.endswith((".bps", ".ips")):
                continue
            rel = os.path.relpath(os.path.join(dp, fn[:-4]), root).replace(os.sep, "/")
            if rel not in romfs or fn.endswith(".bps"):
                romfs[rel] = os.path.join(dp, fn)
    return code, romfs


class Container:
    def __init__(self, f):
        self.f = f
        f.seek(0); h = f.read(0x4000)
        if h[0x100:0x104] == b"NCSD":
            self.kind = "3ds"; self.ncsd = bytearray(h[:0x4000])
            self.ncch = u32(h, 0x120) * MU
        else:
            hs, _t, _v, cert, tik, tmd, meta = struct.unpack_from("<IHHIIII", h, 0)
            if hs != 0x2020:
                raise SystemExit("not a .3ds (NCSD) or .cia file")
            self.kind = "cia"; self.cia_hdr = bytearray(h[:0x2020])
            self.cert_off = align(hs, 64); self.tik_off = align(self.cert_off + cert, 64)
            self.tmd_off = align(self.tik_off + tik, 64); self.tmd_size = tmd
            self.content_off = align(self.tmd_off + tmd, 64)
            self.meta_size = meta
            self.content_size = u64(h, 0x18)
            self.ncch = self.content_off
        f.seek(self.ncch); self.h = bytearray(f.read(0x200))
        if self.h[0x100:0x104] != b"NCCH":
            raise SystemExit("NCCH not found: the file must be decrypted")
        if self.h[0x18F] & 4 == 0:
            raise SystemExit("this file is encrypted; decrypt it first (emulators and this tool need decrypted content)")
        self.tid = u64(self.h, 0x108)
        f.seek(self.ncch + 0x200); self.exh = bytearray(f.read(0x800))
        self.compressed = bool(self.exh[0xD] & 1)
        self.exefs_rel = u32(self.h, 0x1A0) * MU
        self.exefs_size = u32(self.h, 0x1A4) * MU
        self.romfs_rel = u32(self.h, 0x1B0) * MU
        self.romfs_size = u32(self.h, 0x1B4) * MU
        self.content_len = u32(self.h, 0x104) * MU
        f.seek(self.ncch + self.exefs_rel); self.exefs_hdr = bytearray(f.read(0x200))

    def exefs_entries(self):
        out = []
        for i in range(10):
            e = self.exefs_hdr[i * 16:(i + 1) * 16]
            name = e[:8].rstrip(b"\0").decode("ascii")
            if name:
                out.append((i, name, u32(e, 8), u32(e, 12)))
        return out

    def read_exefs(self, name, ncch_base=None):
        base = self.ncch if ncch_base is None else ncch_base
        for _i, n, off, size in self.exefs_entries():
            if n == name:
                self.f.seek(base + self.exefs_rel + 0x200 + off); return self.f.read(size)
        raise SystemExit("ExeFS has no %s" % name)


class RomFS:
    def __init__(self, f, base):
        self.f = f; self.base = base
        f.seek(base); iv = f.read(0x60)
        if iv[:4] != b"IVFC":
            raise SystemExit("RomFS IVFC header not found")
        self.master_size = u32(iv, 8)
        lv = [(u64(iv, 0x0C + 0x18 * k + 8), 1 << u32(iv, 0x0C + 0x18 * k + 16)) for k in range(3)]
        (self.l1_size, self.b1), (self.l2_size, self.b2), (self.l3_size, self.b3) = lv
        self.l3 = base + align(0x60 + self.master_size, self.b3)
        self.l1 = self.l3 + align(self.l3_size, self.b3)
        self.l2 = self.l1 + align(self.l1_size, self.b1)
        f.seek(self.l3); hh = struct.unpack("<10I", f.read(0x28))
        self.dmo, self.dml, self.fmo, self.fml, self.fdo = hh[3], hh[4], hh[7], hh[8], hh[9]

    def files(self):
        f = self.f
        f.seek(self.l3 + self.dmo); dm = f.read(self.dml)
        f.seek(self.l3 + self.fmo); fm = f.read(self.fml)
        out = {}

        def walk(doff, prefix):
            fo = u32(dm, doff + 0xC)
            while fo != 0xFFFFFFFF:
                n = u32(fm, fo + 0x1C)
                out[prefix + fm[fo + 0x20:fo + 0x20 + n].decode("utf-16le")] = (self.fdo + u64(fm, fo + 8), u64(fm, fo + 0x10))
                fo = u32(fm, fo + 4)
            co = u32(dm, doff + 8)
            while co != 0xFFFFFFFF:
                n = u32(dm, co + 0x14)
                walk(co, prefix + dm[co + 0x18:co + 0x18 + n].decode("utf-16le") + "/")
                co = u32(dm, co + 4)

        walk(0, "")
        return out

    def _block(self, start, size, bs, idx):
        self.f.seek(start + idx * bs); b = self.f.read(bs)
        valid = max(0, min(bs, size - idx * bs))
        return b[:valid] + b"\0" * (bs - valid)

    def _check_or_set(self, table_start, idx, digest, write):
        self.f.seek(table_start + idx * 32)
        if write:
            self.f.write(digest); return True
        return self.f.read(32) == digest

    def refresh(self, l3_ranges, write):
        b3 = sorted({k for s, e in l3_ranges for k in range(s // self.b3, (e - 1) // self.b3 + 1)})
        ok = True
        for k in b3:
            ok &= self._check_or_set(self.l2, k, hashlib.sha256(self._block(self.l3, self.l3_size, self.b3, k)).digest(), write)
        b2 = sorted({k * 32 // self.b2 for k in b3})
        for k in b2:
            ok &= self._check_or_set(self.l1, k, hashlib.sha256(self._block(self.l2, self.l2_size, self.b2, k)).digest(), write)
        b1 = sorted({k * 32 // self.b1 for k in b2})
        for k in b1:
            ok &= self._check_or_set(self.base + 0x60, k, hashlib.sha256(self._block(self.l1, self.l1_size, self.b1, k)).digest(), write)
        return ok


def copy_range(src, dst, src_off, length, dst_off=None):
    src.seek(src_off)
    if dst_off is not None:
        dst.seek(dst_off)
    while length:
        n = min(CHUNK, length); b = src.read(n)
        if len(b) != n:
            raise SystemExit("input file is truncated")
        dst.write(b); length -= n


def fill(dst, length, byte):
    blk = bytes([byte]) * min(CHUNK, length)
    while length:
        n = min(len(blk), length); dst.write(blk[:n]); length -= n


def build_exefs(c, code):
    files = []
    for i, name, off, size in c.exefs_entries():
        files.append((i, name, code if name == ".code" else c.read_exefs(name)))
    hdr = bytearray(0x200); body = bytearray(); pos = 0
    for i, name, data in files:
        struct.pack_into("<8sII", hdr, i * 16, name.encode("ascii"), pos, len(data))
        hdr[0xC0 + (9 - i) * 32:0xC0 + (10 - i) * 32] = hashlib.sha256(data).digest()
        body += data + b"\0" * (align(len(data), MU) - len(data)); pos = len(body)
    return bytes(hdr + body)


def write_ncch(c, src, dst, new_exefs, romfs_rel):
    h = bytearray(c.h); exh = bytearray(c.exh)
    exefs_rel = c.exefs_rel
    exh[0xD] &= 0xFE
    struct.pack_into("<I", h, 0x1A4, len(new_exefs) // MU)
    struct.pack_into("<I", h, 0x1B0, romfs_rel // MU)
    struct.pack_into("<I", h, 0x104, (romfs_rel + c.romfs_size) // MU)
    h[0x160:0x180] = hashlib.sha256(exh[:0x400]).digest()
    h[0x1C0:0x1E0] = hashlib.sha256(new_exefs[:u32(h, 0x1A8) * MU]).digest()
    dst.seek(c.ncch); dst.write(h); dst.write(exh)
    copy_range(src, dst, c.ncch + 0xA00, exefs_rel - 0xA00)
    dst.write(new_exefs)
    fill(dst, romfs_rel - exefs_rel - len(new_exefs), 0)
    copy_range(src, dst, c.ncch + c.romfs_rel, c.romfs_size)


def plan_3ds(c, exefs_len):
    hdr = bytearray(c.ncsd)
    total = os.fstat(c.f.fileno()).st_size
    card = u32(hdr, 0x104) * MU
    parts = [(u32(hdr, 0x120 + 8 * i), u32(hdr, 0x124 + 8 * i)) for i in range(8)]
    romfs_rel = align(c.exefs_rel + exefs_len, 0x1000)
    p0_new = align(romfs_rel + c.romfs_size, MU) // MU
    delta = p0_new - parts[0][1]
    old_p0_end = parts[0][0] + parts[0][1]
    old_end = max(o + s for o, s in parts if s)
    if old_end * MU > total:
        raise SystemExit("the input is shorter than its own partition table says; the dump is damaged")
    struct.pack_into("<II", hdr, 0x120, parts[0][0], p0_new)
    for i in range(1, 8):
        off, size = parts[i]
        if size:
            if off < old_p0_end:
                raise SystemExit("unexpected partition layout; nothing was written")
            struct.pack_into("<II", hdr, 0x120 + 8 * i, off + delta, size)
    end = old_end + delta
    writable = u64(hdr, 0x200)
    if writable != 0xFFFFFFFFFFFFFFFF and writable < (1 << 40) and end > writable:
        raise SystemExit("the rebuilt image would overlap the card's save area; nothing was written")
    if end * MU > card:
        raise SystemExit("the rebuilt image does not fit in the card size the header declares; nothing was written")
    if u32(hdr, 0x300):
        struct.pack_into("<I", hdr, 0x300, end * MU)
    out_len = total if total >= card else end * MU
    return dict(hdr=hdr, romfs_rel=romfs_rel, old_p0_end=old_p0_end, old_end=old_end, delta=delta, end=end, out_len=out_len, trimmed=total < card)


def relayout_3ds(c, src, dst, new_exefs, plan):
    copy_range(src, dst, 0, c.ncch, 0)
    write_ncch(c, src, dst, new_exefs, plan["romfs_rel"])
    dst.seek((plan["old_p0_end"] + plan["delta"]) * MU)
    copy_range(src, dst, plan["old_p0_end"] * MU, (plan["old_end"] - plan["old_p0_end"]) * MU)
    fill(dst, plan["out_len"] - plan["end"] * MU, 0xFF)
    dst.seek(0); dst.write(plan["hdr"])
    return c.ncch, plan["romfs_rel"]


def tmd_fix(c, dst):
    dst.seek(c.tmd_off); tmd = bytearray(dst.read(c.tmd_size))
    sig = struct.unpack_from(">I", tmd, 0)[0]
    sizes = {0x10000: 0x200, 0x10001: 0x100, 0x10002: 0x3C, 0x10003: 0x200, 0x10004: 0x100, 0x10005: 0x3C}
    if sig not in sizes:
        raise SystemExit("unrecognised TMD signature type")
    hdr = align(4 + sizes[sig], 64)
    count = struct.unpack_from(">H", tmd, hdr + 0x9E)[0]
    info = hdr + 0xC4; chunks = info + 64 * 36
    dst.seek(c.content_off)
    h = hashlib.sha256(); left = struct.unpack_from(">Q", tmd, chunks + 8)[0]
    while left:
        b = dst.read(min(CHUNK, left)); h.update(b); left -= len(b)
    tmd[chunks + 16:chunks + 48] = h.digest()
    tmd[info + 4:info + 36] = hashlib.sha256(tmd[chunks:chunks + 48 * count]).digest()
    tmd[hdr + 0xA4:hdr + 0xC4] = hashlib.sha256(tmd[info:info + 64 * 36]).digest()
    dst.seek(c.tmd_off); dst.write(tmd)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser(description="Bake every Un-Nerf Compendium patch for this generation into a copy of a decrypted .3ds or .cia")
    ap.add_argument("game", help="decrypted .3ds or .cia (it is only read)")
    ap.add_argument("--out", help="output file (default: next to the input, named '<input name> [Un-Nerf UNTESTED]')")
    ap.add_argument("--mods", help="patch set directory (default: azahar-mods/<title id>/v1.0/all next to this script)")
    ap.add_argument("--verify", action="store_true", help="check that every patch applies, write nothing")
    a = ap.parse_args()
    src = open(a.game, "rb")
    c = Container(src)
    title = TITLES.get(c.tid)
    if title is None:
        raise SystemExit("title %016X is not one this tool knows" % c.tid)
    mods = a.mods or os.path.join(here, "azahar-mods", "%016X" % c.tid, "v1.0", "all")
    if not os.path.isdir(mods):
        raise SystemExit("no patch set for %s at %s; run the copy of this tool that sits in that game's folder" % (title, mods))
    code_patch, romfs_patches = load_patch_set(mods)
    if not code_patch and not romfs_patches:
        raise SystemExit("no patches found in %s" % mods)
    print("[info] %s (%016X), %s, .code %s" % (title, c.tid, c.kind, "compressed" if c.compressed else "flat"))
    raw_code = c.read_exefs(".code")
    code = blz_decompress(raw_code) if c.compressed else raw_code
    new_code = code
    try:
        if code_patch:
            new_code = apply_patch(code_patch, code)
            print("[code] %s: .code %08X -> %08X" % (os.path.relpath(code_patch, mods), crc(code), crc(new_code)))
        rf = RomFS(src, c.ncch + c.romfs_rel)
        table = rf.files()
        edits = []
        for path in sorted(romfs_patches):
            if path not in table:
                raise SystemExit("RomFS has no %s" % path)
            off, size = table[path]
            src.seek(rf.l3 + off); old = src.read(size)
            new = apply_patch(romfs_patches[path], old)
            print("[romfs] %s: %s %08X -> %08X" % (os.path.relpath(romfs_patches[path], mods), path, crc(old), crc(new)))
            edits.append((off, old, new))
        if not rf.refresh([(o, o + len(n)) for o, _x, n in edits], False):
            raise SystemExit("the RomFS hash tree of the input does not verify; refusing to patch a damaged dump")
    except PatchError as e:
        raise SystemExit("[abort] %s. Nothing was written." % e)
    plan = None
    if c.compressed:
        if c.kind != "3ds":
            raise SystemExit("a .cia whose .code is compressed is not supported (its content would have to grow); use the .3ds dump. Nothing was written.")
        new_exefs = build_exefs(c, new_code)
        plan = plan_3ds(c, len(new_exefs))
        print("[plan] .code stored uncompressed: RomFS and later partitions move by 0x%X bytes%s"
              % (plan["delta"] * MU, ", trimmed output" if plan["trimmed"] else ""))
    if a.verify:
        print("[verify] every patch applies; nothing written"); return
    out = a.out or os.path.join(os.path.dirname(os.path.abspath(a.game)),
                                "%s [Un-Nerf UNTESTED]%s" % os.path.splitext(os.path.basename(a.game)))
    if os.path.realpath(out) == os.path.realpath(a.game) or (os.path.exists(out) and os.path.samefile(out, a.game)):
        raise SystemExit("the output must be a different file from the input")
    tmp = out + ".part"
    try:
        with open(tmp, "w+b") as dst:
            print("[copy] %s -> %s%s" % (os.path.basename(a.game), out, " (rebuilt with the .code stored uncompressed)" if plan else ""))
            if plan:
                ncch_out, romfs_rel = relayout_3ds(c, src, dst, new_exefs, plan)
            else:
                copy_range(src, dst, 0, os.fstat(src.fileno()).st_size, 0)
                ncch_out, romfs_rel = c.ncch, c.romfs_rel
                if new_code != code:
                    for i, name, off, size in c.exefs_entries():
                        if name == ".code":
                            dst.seek(ncch_out + c.exefs_rel + 0x200 + off); dst.write(new_code)
                            hd = bytearray(c.exefs_hdr)
                            hd[0xC0 + (9 - i) * 32:0xC0 + (10 - i) * 32] = hashlib.sha256(new_code).digest()
                            dst.seek(ncch_out + c.exefs_rel); dst.write(hd)
                            nh = bytearray(c.h)
                            dst.seek(ncch_out + c.exefs_rel); region = dst.read(u32(nh, 0x1A8) * MU)
                            nh[0x1C0:0x1E0] = hashlib.sha256(region).digest()
                            dst.seek(ncch_out); dst.write(nh)
            orf = RomFS(dst, ncch_out + romfs_rel)
            for off, _old, new in edits:
                dst.seek(orf.l3 + off); dst.write(new)
            orf.refresh([(o, o + len(n)) for o, _x, n in edits], True)
            dst.flush()
            if not orf.refresh([(o, o + len(n)) for o, _x, n in edits], False):
                raise SystemExit("internal error: RomFS hash tree did not verify after writing")
            dst.seek(ncch_out); nh = bytearray(dst.read(0x200))
            dst.seek(ncch_out + romfs_rel); region = dst.read(u32(nh, 0x1B8) * MU)
            nh[0x1E0:0x200] = hashlib.sha256(region).digest()
            dst.seek(ncch_out); dst.write(nh)
            if c.kind == "cia":
                tmd_fix(c, dst)
        os.replace(tmp, out)
    except BaseException:
        if os.path.exists(tmp):
            os.remove(tmp)
        raise
    print("[done] %s" % out)


if __name__ == "__main__":
    main()
