#!/usr/bin/env python3
r"""
BW2 Pokéstar box/party ICON generator (a/0/0/7).

Most Pokéstar props display a shared "?" placeholder icon in the party/PC menus (verified: species
652–682 share two "?" icons; 683 Black Belt and 684 Smeargle already have real icons). This makes
recognizable 32×32 mini icons from each prop's clean sprite and writes them over the "?" icons, so
they read as the actual creature instead of a question mark — in-game.

Icon NARC a/0/0/7 layout: files 0–1 = shared NCLR palette banks (file0 = 3×16-col, file1 = 4×16-col
=> 7 shared palettes, index 0..6); files 2–7 = shared NANR/NCER anim/cell; files 8+ = per-species icon
NCGRs, icon(species S) = file 8 + S*2 (two 32×32 frames, 4bpp). Each species' icon-palette index is
in arm9 @ 0x8B4CF (one byte per species). We keep each icon's existing palette index and only rewrite
its PIXELS (quantized to that palette's 16 colours) — structurally identical, guaranteed to render.

Sprite source: the clean 96×96 sprite (a/0/0/4 file S*20+0), trimmed to its content bbox and downscaled
to 32×32. (The 256×128 animated front sheet uses a non-trivial tile order that can't be detiled without
the cell data, so the clean 96×96 sprite is used as the recognizable source.)

Reversible (keep the input ROM). Only a/0/0/7 is modified. Needs in-emulator visual confirm (STEP 6).
"""
import sys, argparse, struct, ndspy.rom, ndspy.narc, ndspy.codeCompression as cc
from PIL import Image

# Only the 17 user-facing Pokéstar opponents have real battle sprites; the other internal "Prop XX"
# entries (664, 666–679, 681) are behind-the-scenes placeholders -> never regenerate those.
USER_PROPS = [652, 653, 654, 655, 656, 657, 658, 659, 660, 661, 662, 663, 665, 680, 682, 683, 684]
SKIP = {683, 684}          # already have real icons — user said "check first, don't overwrite a real one"
ARM9_RAM = 0x2004000
ICON_PAL_TABLE = 0x8B4CF   # arm9: one palette-index byte per species

def _lz(d):
    d = bytes(d)
    if not d or d[0] != 0x11:
        return d
    size = d[1] | (d[2] << 8) | (d[3] << 16)
    o = bytearray(); i = 4
    while len(o) < size:
        fl = d[i]; i += 1
        for b in range(8):
            if len(o) >= size:
                break
            if fl & (0x80 >> b):
                b0 = d[i]; b1 = d[i+1]; i += 2; t = b0 >> 4
                if t == 0:   c = (b0 << 4 | b1 >> 4) + 0x11;                    dp = ((b1 & 0xF) << 8 | d[i]) + 1; i += 1
                elif t == 1: c = ((b0 & 0xF) << 12 | b1 << 4 | d[i] >> 4) + 0x111; dp = ((d[i] & 0xF) << 8 | d[i+1]) + 1; i += 2
                else:        c = t + 1;                                        dp = ((b0 & 0xF) << 8 | b1) + 1
                for _ in range(c):
                    o.append(o[-dp])
            else:
                o.append(d[i]); i += 1
    return bytes(o)

def _palettes(icon_files):
    """Return list of 7 palettes, each a list of 16 (r,g,b)."""
    pals = []
    for fi in (0, 1):
        d = _lz(icon_files[fi]); p = d.find(b'TTLP')
        n = struct.unpack_from('<I', d, p + 0x10)[0] // 32   # #16-colour palettes
        for k in range(n):
            base = p + 0x18 + k * 32
            pals.append([((v & 31) << 3, ((v >> 5) & 31) << 3, ((v >> 10) & 31) << 3)
                         for v in struct.unpack_from('<16H', d, base)])
    return pals

def _sprite_mini(pg_files, species):
    """Clean 96×96 sprite (file S*20) -> trimmed -> 32×32 RGBA."""
    S = species
    d = _lz(pg_files[S * 20 + 0]); p = d.find(b'RAHC')
    if p < 0:
        return None
    px = d[p + 0x20:]
    pd = _lz(pg_files[S * 20 + 18]); pp = pd.find(b'TTLP')
    if pp < 0:
        return None
    cols = [((v & 31) << 3, ((v >> 5) & 31) << 3, ((v >> 10) & 31) << 3)
            for v in struct.unpack_from('<16H', pd, pp + 0x18)]
    pv = []
    for b in px:
        pv.append(b & 0xF); pv.append(b >> 4)
    tilesx = 12                                  # 96px / 8 = 12 tiles wide, row-major 8x8 tiles (BW order)
    im = Image.new('RGBA', (96, 96), (0, 0, 0, 0))
    nonzero = 0
    for t in range(min(144, len(pv) // 64)):
        tx = (t % tilesx) * 8; ty = (t // tilesx) * 8
        for yy in range(8):
            for xx in range(8):
                v = pv[t * 64 + yy * 8 + xx]
                if v != 0 and ty + yy < 96:
                    im.putpixel((tx + xx, ty + yy), cols[v] + (255,))
                    nonzero += 1
    if nonzero < 40:
        return None                              # essentially blank (e.g. 659 Monster)
    bbox = im.getbbox()
    if not bbox:
        return None
    im = im.crop(bbox)
    # scale the longest side to 32, NEAREST (crisp pixel art), centre on a 32x32 transparent canvas
    w, h = im.size
    scale = 32 / max(w, h)
    im = im.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.NEAREST)
    canvas = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    canvas.paste(im, ((32 - im.width) // 2, (32 - im.height) // 2))
    return canvas

def _best_palette(mini, pals):
    """Pick the shared palette that minimises total nearest-colour error for the sprite's pixels."""
    px = mini.load()
    best_i, best_err = 0, 1 << 62
    for pi, pal in enumerate(pals):
        err = 0
        for y in range(32):
            for x in range(32):
                r, g, b, a = px[x, y]
                if a < 128:
                    continue
                bd = 1 << 30
                for idx in range(1, 16):
                    pr, pg, pb = pal[idx]
                    dd = (r - pr) ** 2 + (g - pg) ** 2 + (b - pb) ** 2
                    if dd < bd:
                        bd = dd
                err += bd
        if err < best_err:
            best_err, best_i = err, pi
    return best_i

def _quantize_to(mini, pal):
    """Map each opaque pixel to nearest palette index (1..15); transparent -> 0."""
    out = bytearray(32 * 32)
    px = mini.load()
    for y in range(32):
        for x in range(32):
            r, g, b, a = px[x, y]
            if a < 128:
                out[y * 32 + x] = 0
                continue
            best = 1; bd = 1 << 30
            for idx in range(1, 16):
                pr, pg, pb = pal[idx]
                dd = (r - pr) ** 2 + (g - pg) ** 2 + (b - pb) ** 2
                if dd < bd:
                    bd = dd; best = idx
            out[y * 32 + x] = best
    return out

def _encode_icon(pix32, template):
    """Write 32×32 index pixels into both 32×32 frames of an icon NCGR (based on template file bytes)."""
    d = bytearray(template)
    p = d.find(b'RAHC')
    data_off = p + 0x20
    # pack 4bpp, tiled 8×8, 4 tiles wide (32px). Frame is 512 bytes; write to frame0 and frame1.
    frame = bytearray(512)
    tilesx = 4
    for t in range(16):
        tx = (t % tilesx) * 8; ty = (t // tilesx) * 8
        for yy in range(8):
            for xx in range(4):
                lo = pix32[(ty + yy) * 32 + tx + xx * 2]
                hi = pix32[(ty + yy) * 32 + tx + xx * 2 + 1]
                frame[t * 32 + yy * 4 + xx] = (lo & 0xF) | ((hi & 0xF) << 4)
    d[data_off:data_off + 512] = frame
    if data_off + 1024 <= len(d):
        d[data_off + 512:data_off + 1024] = frame   # frame 1 = same (static)
    return bytes(d)

def _read_arm9(rom):
    raw = bytes(rom.arm9)
    off = raw.find(struct.pack('<I', 0xDEC00621))
    if off >= 8 and struct.unpack_from('<I', raw, off - 8)[0] == 0:
        return raw                                         # already decompressed (compression disabled)
    return cc.decompress(raw)

def generate(inpath, outpath, dump_png=None, verbose=True):
    rom = ndspy.rom.NintendoDSRom.fromFile(inpath)
    a9 = _read_arm9(rom)                                    # READ-ONLY: never write arm9 (boot-safety)
    icfid = rom.filenames.idOf('a/0/0/7')
    narc = ndspy.narc.NARC(rom.files[icfid])
    icon_files = [bytearray(f) for f in narc.files]
    pg = ndspy.narc.NARC(rom.files[rom.filenames.idOf('a/0/0/4')]).files
    pals = _palettes(icon_files)
    montage = Image.new('RGBA', (34 * len(USER_PROPS), 34), (200, 200, 200, 255)) if dump_png else None
    done = []
    for si, sp in enumerate(USER_PROPS):
        if sp in SKIP:
            continue
        mini = _sprite_mini(pg, sp)
        if mini is None:
            continue  # e.g. 659 Monster has no clean 96×96 sprite -> leave existing icon
        # Keep each icon's EXISTING shared palette (its arm9 index, read-only) and quantise the mini
        # to it — so we only rewrite pixel bytes in the icon NARC and never touch arm9 (which caused
        # the earlier white-screen boot failure).
        palIdx = a9[ICON_PAL_TABLE + sp] if ICON_PAL_TABLE + sp < len(a9) else 0
        if palIdx >= len(pals):
            palIdx = 0
        pix = _quantize_to(mini, pals[palIdx])
        fi = 8 + sp * 2
        icon_files[fi] = bytearray(_encode_icon(pix, icon_files[fi]))
        done.append(sp)
        if montage is not None:
            vis = Image.new('RGBA', (32, 32), (60, 60, 60, 255))
            for y in range(32):
                for x in range(32):
                    v = pix[y * 32 + x]
                    vis.putpixel((x, y), (0, 0, 0, 0) if v == 0 else pals[palIdx][v] + (255,))
            montage.paste(vis, (si * 34, 1))
    narc.files = [bytes(f) for f in icon_files]
    rom.files[icfid] = narc.save()
    rom.saveToFile(outpath)                              # arm9 untouched by the icon step
    if montage is not None:
        montage = montage.resize((montage.width * 3, montage.height * 3), Image.NEAREST)
        montage.save(dump_png)
    if verbose:
        print("[icons] regenerated %d prop icons from sprites: %s" % (len(done), done))
        print("[icons] wrote", outpath)
    return done

if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('rom'); ap.add_argument('--out'); ap.add_argument('--png')
    a = ap.parse_args()
    generate(a.rom, a.out or a.rom.replace('.nds', '_icons.nds'), dump_png=a.png)
