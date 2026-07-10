#!/usr/bin/env python3
r"""
Generate small PNG sprites of the Pokéstar props for the PKHaX PREVIEW (not the ROM).

Source: BW2 pokégra a/0/0/4, file S*20+0 (the 96x96 "back"/base NCGR). BW pokégra tiles are laid out
**8 tiles (64 px) wide** (verified against Bulbasaur/Charizard/Victini — the 12-wide read the icon tool
used before is why those looked garbled). Each prop is de-tiled at 8-wide, coloured with its normal
palette (file S*20+18), trimmed to content, and fit (aspect-preserved, bottom-centred) onto a 68x56
RGBA canvas — the same size PKHeX uses for its Gen-5 base sprites.

Props whose base sprite is blank/near-blank in the ROM (opponents with no drawn player-side sprite, e.g.
659 "Monster") are skipped -> PKHaX falls back to its "?" placeholder for those.

Output: Resources/img/pokestar/pokestar_<species>.png  (652..684), consumed by PKHaX SpriteBuilder,
gen5-gated so Gen-6+ (where 652=Chespin etc.) is unaffected.
"""
import os, struct, argparse, ndspy.rom, ndspy.narc
from collections import Counter
from PIL import Image

PROP_MIN, PROP_MAX = 652, 684
STRIDE = 20
CANVAS = (68, 56)

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
                if t == 0:   c = (b0 << 4 | b1 >> 4) + 0x11;                       dp = ((b1 & 0xF) << 8 | d[i]) + 1; i += 1
                elif t == 1: c = ((b0 & 0xF) << 12 | b1 << 4 | d[i] >> 4) + 0x111; dp = ((d[i] & 0xF) << 8 | d[i+1]) + 1; i += 2
                else:        c = t + 1;                                            dp = ((b0 & 0xF) << 8 | b1) + 1
                for _ in range(c):
                    o.append(o[-dp])
            else:
                o.append(d[i]); i += 1
    return bytes(o)

def _palette(pg, sp):
    pd = _lz(pg[sp*STRIDE + 18]); pp = pd.find(b'TTLP')
    if pp < 0:
        return None
    return [((v & 31) << 3, ((v >> 5) & 31) << 3, ((v >> 10) & 31) << 3)
            for v in struct.unpack_from('<16H', pd, pp + 0x18)]

def _decode(pg, sp, tw=8):
    d = _lz(pg[sp*STRIDE + 0]); p = d.find(b'RAHC')
    if p < 0:
        return None
    px = d[p+0x20:]
    cols = _palette(pg, sp)
    if cols is None:
        return None
    pv = []
    for b in px:
        pv.append(b & 0xF); pv.append(b >> 4)
    ntiles = len(pv) // 64; th = (ntiles + tw - 1) // tw
    im = Image.new('RGBA', (tw*8, th*8), (0, 0, 0, 0))
    nz = 0
    for t in range(ntiles):
        tx = (t % tw)*8; ty = (t // tw)*8
        for yy in range(8):
            for xx in range(8):
                v = pv[t*64 + yy*8 + xx]
                if v:
                    im.putpixel((tx+xx, ty+yy), cols[v] + (255,)); nz += 1
    return im, nz

def _fit(im):
    bb = im.getbbox()
    if not bb:
        return None
    im = im.crop(bb)
    w, h = im.size
    s = min(CANVAS[0] / w, CANVAS[1] / h, 1.0) if max(w, h) > 0 else 1.0
    # upscale small sprites a bit so they read in the box, but never past the canvas
    s = min(CANVAS[0] / w, CANVAS[1] / h)
    im = im.resize((max(1, round(w*s)), max(1, round(h*s))), Image.NEAREST)
    canvas = Image.new('RGBA', CANVAS, (0, 0, 0, 0))
    canvas.paste(im, ((CANVAS[0]-im.width)//2, CANVAS[1]-im.height), im)  # bottom-centre
    return canvas

def generate(rompath, outdir, montage=None):
    os.makedirs(outdir, exist_ok=True)
    rom = ndspy.rom.NintendoDSRom.fromFile(rompath)
    pg = ndspy.narc.NARC(rom.files[rom.filenames.idOf('a/0/0/4')]).files
    made, skipped = [], []
    tiles = []
    for sp in range(PROP_MIN, PROP_MAX + 1):
        res = _decode(pg, sp)
        if res is None or res[1] < 60:               # blank / undecodable -> let PKHaX use "?"
            skipped.append(sp); continue
        fit = _fit(res[0])
        if fit is None:
            skipped.append(sp); continue
        fit.save(os.path.join(outdir, f"pokestar_{sp}.png"))
        made.append(sp); tiles.append((sp, fit))
    if montage and tiles:
        cols = 6; rows = (len(tiles)+cols-1)//cols
        M = Image.new('RGBA', (cols*72, rows*64), (70, 70, 70, 255))
        for i, (sp, im) in enumerate(tiles):
            M.paste(im, ((i % cols)*72+2, (i//cols)*64+4), im)
        M.save(montage)
    return made, skipped

if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('rom'); ap.add_argument('--out', required=True); ap.add_argument('--montage')
    a = ap.parse_args()
    made, skipped = generate(a.rom, a.out, a.montage)
    print(f"generated {len(made)} prop sprites: {made}")
    print(f"skipped (blank in ROM -> '?'): {skipped}")
