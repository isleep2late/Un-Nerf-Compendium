"""Gen-5 (BW/B2W2) NARC text codec. Container: header u16 numBlocks,u16 numLines,u32 fileSize;
section base 0x10 (u32 secLen), entry table at 0x14 = numLines*(u32 offset-rel-0x10, u32 charCount),
then UTF-16LE char data. Char cipher: seed(line)=(0x7C89 + line*0x2983)&0xFFFF; per char
plain = cipher ^ key; key = ((key<<3)|(key>>13))&0xFFFF (rotate-left-3). Terminator 0xFFFF."""
import struct
KEY_BASE=0x7C89; KEY_ADV=0x2983
def _rl(k): return ((k<<3)|(k>>13))&0xFFFF
def decode_file(data):
    nb,nl=struct.unpack_from('<HH',data,0)
    out=[]
    for l in range(nl):
        off,cc=struct.unpack_from('<II',data,0x14+l*8)
        raw=struct.unpack_from('<%dH'%cc,data,0x10+off)
        key=(KEY_BASE + l*KEY_ADV)&0xFFFF
        chars=[]
        for c in raw:
            chars.append(c^key); key=_rl(key)
        out.append(chars)
    return out
def to_str(codes):
    s=[]
    for c in codes:
        if c==0xFFFF: break
        if c in (0x0000,0xF000): continue
        if c==0xE000: s.append('\n'); continue
        if c==0x25BC: s.append('\n'); continue
        if 0x20<=c<0x7F: s.append(chr(c))
        else: s.append('{%04X}'%c)
    return ''.join(s)
if __name__=='__main__':
    import ndspy.rom, ndspy.narc, sys
    rom=ndspy.rom.NintendoDSRom.fromFile(sys.argv[1] if len(sys.argv)>1 else 'Black2_clean.nds')
    def narc(p): return ndspy.narc.NARC(rom.files[rom.filenames.idOf(p)])
    names=[to_str(x) for x in decode_file(bytes(narc('a/0/0/2').files[90]))]
    print("total names:",len(names))
    for i in range(648,687):
        print(f"{i}: {names[i]!r}")
