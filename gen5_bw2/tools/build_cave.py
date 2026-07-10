"""Assemble the Pokestar validator cave. Returns (bytes, entry_relative_layout).
Cave reads the mon's species (decrypt+unshuffle-aware) and, if it's a prop (652-684),
forces the validator's 'valid' path (r4=1) instead of setting checksumFailed."""
import sys; sys.path.insert(0,'tools')
import thumb1 as T
# blockA physical position for each shuffle value sv (0..23): BlockPosition[sv*4+0]
BLOCKA=bytes([0,0,0,0,0,0,1,1,2,3,2,3,1,1,2,3,2,3,1,1,2,3,2,3])
LO,HI=652,684

def build(cave_base, ret_addr):
    # layout offsets (bytes) within cave — must match hand-computed branch targets
    c=[]
    def emit(x): c.append(x)
    # 0x00
    emit(T.ldr_imm(3,5,0))     # ldr r3,[r5,#0]   ; PID
    emit(T.lsrs(3,3,13))       # lsrs r3,r3,#13
    emit(T.movs(0,0x1F))       # movs r0,#0x1f
    emit(T.ands(3,0))          # ands r3,r0       ; sv0 (0..31)
    emit(T.cmp_imm(3,24))      # cmp r3,#24
    emit(T.bcc(T.COND['lt'],(0x0E)-(0x0A+4)))  # blt L1(0x0E)
    emit(T.subs_imm(3,24))     # subs r3,#24      ; %24
    # 0x0E L1:
    emit(T.adr(0,(0x40)-(((0x0E+4)&~3))))      # adr r0,TBL(0x40)
    emit(T.ldrb_reg(0,0,3))    # ldrb r0,[r0,r3] ; posA
    emit(T.lsls(0,0,5))        # lsls r0,r0,#5   ; *0x20
    emit(T.adds_reg(0,0,5))    # adds r0,r0,r5   ; r5+posA*0x20
    emit(T.ldrh_imm(0,0,8))    # ldrh r0,[r0,#8] ; species (blocks=r5+8)
    # 0x18
    emit(T.ldr_lit(1,(0x38)-(((0x18+4)&~3))))  # ldr r1,=652(0x38)
    emit(T.cmp_reg(0,1))       # cmp r0,r1
    emit(T.bcc(T.COND['lt'],(0x28)-(0x1C+4)))  # blt DOFLAG(0x28)
    emit(T.ldr_lit(1,(0x3C)-(((0x1E+4)&~3))))  # ldr r1,=684(0x3C)
    emit(T.cmp_reg(0,1))       # cmp r0,r1
    emit(T.bcc(T.COND['gt'],(0x28)-(0x22+4)))  # bgt DOFLAG(0x28)
    # 0x24 prop -> valid
    emit(T.movs(4,1))          # movs r4,#1
    emit(T.b((0x32)-(0x26+4))) # b RET(0x32)
    # 0x28 DOFLAG: original flag set
    emit(T.ldrh_imm(1,5,4))    # ldrh r1,[r5,#4]
    emit(T.movs(0,4))          # movs r0,#4
    emit(T.movs(4,0))          # movs r4,#0
    emit(T.orrs(0,1))          # orrs r0,r1
    emit(T.strh_imm(0,5,4))    # strh r0,[r5,#4]
    # 0x32 RET: long branch back to validator tail (0x201DDFC)
    emit(T.bl(cave_base+0x32, ret_addr))       # bl ret_addr  (4 bytes -> ends 0x36)
    blob=b''.join(c)
    assert len(blob)==0x36, hex(len(blob))
    blob+=T.nop()              # 0x36 pad to word align -> 0x38
    assert len(blob)==0x38
    blob+=(LO).to_bytes(4,'little')   # 0x38 =652
    blob+=(HI).to_bytes(4,'little')   # 0x3C =684
    blob+=BLOCKA                       # 0x40 TBL (24 bytes) -> 0x58
    assert len(blob)==0x58, hex(len(blob))
    return blob
