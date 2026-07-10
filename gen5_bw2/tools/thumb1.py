"""Minimal Thumb-1 (ARMv5TE) assembler for the BW2 Pokestar validator code cave.
Only the encodings we need; every instruction is disasm-verified by the caller with capstone."""
import struct
def h(x): return struct.pack('<H', x & 0xFFFF)
def ldr_imm(rd,rn,imm):    return h(0b0110100000000000|((imm//4)<<6)|(rn<<3)|rd)      # ldr rd,[rn,#imm]
def ldr_lit(rd,imm):       return h(0b0100100000000000|(rd<<8)|(imm//4))            # ldr rd,[pc,#imm]
def lsrs(rd,rm,imm):       return h(0b0000100000000000|((imm&31)<<6)|(rm<<3)|rd)
def lsls(rd,rm,imm):       return h(0b0000000000000000|((imm&31)<<6)|(rm<<3)|rd)
def movs(rd,imm):          return h(0b0010000000000000|(rd<<8)|(imm&0xFF))
def ands(rd,rm):           return h(0b0100000000000000|(rm<<3)|rd)
def orrs(rd,rm):           return h(0b0100001100000000|(rm<<3)|rd)
def cmp_imm(rn,imm):       return h(0b0010100000000000|(rn<<8)|(imm&0xFF))
def cmp_reg(rn,rm):        return h(0b0100001010000000|(rm<<3)|rn)
def adds_reg(rd,rn,rm):    return h(0b0001100000000000|(rm<<6)|(rn<<3)|rd)
def subs_imm(rd,imm):      return h(0b0011100000000000|(rd<<8)|(imm&0xFF))
def ldrb_reg(rd,rn,rm):    return h(0b0101110000000000|(rm<<6)|(rn<<3)|rd)
def ldrh_imm(rd,rn,imm):   return h(0b1000100000000000|((imm//2)<<6)|(rn<<3)|rd)
def strh_imm(rd,rn,imm):   return h(0b1000000000000000|((imm//2)<<6)|(rn<<3)|rd)
def adr(rd,imm):           return h(0b1010000000000000|(rd<<8)|(imm//4))            # add rd,pc,#imm
def nop():                 return h(0x46C0)
def b(off):                                                                       # off from pc(=addr+4)
    i=(off>>1)&0x7FF;     return h(0b1110000000000000|i)
def bcc(cond,off):         return h(0b1101000000000000|(cond<<8)|((off>>1)&0xFF))  # cond branch
def bl(cur,target):
    off=target-(cur+4); off&=0x7FFFFF if off>=0 else 0x7FFFFF
    off=target-(cur+4)
    hi=(off>>12)&0x7FF; lo=(off>>1)&0x7FF
    return h(0b1111000000000000|hi)+h(0b1111100000000000|lo)
COND={'eq':0,'ne':1,'lt':11,'gt':12,'ge':10,'le':13}
