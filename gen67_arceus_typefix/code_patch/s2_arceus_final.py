#!/usr/bin/env python3
"""Final USUM Arceus/Silvally fix (code.bin):
 (A) form persistence: 0x3236D8  mov r0,#1 -> mov r0,r4  (non-plate item => no form reset)
 (B) plate-or-form type: inject 2 handlers in the .text code cave (0x5B99F8) and hook the
     GetType1/GetType2 Arceus+Multitype / Silvally+RKS branches to them:
        t = plateOrMemoryTable(heldItem);  return t if t!=0 (plate/memory held) else GetFormNo()
This gives: hold matching Plate/Memory -> plate/memory type (Multitype/RKS fires); no plate ->
the PKHeX form's type. Length-preserving. Verified with Unicorn."""
import struct
BASE=0x100000
CAVE=0x5B99F8                      # end-of-.text padding (executable, 1544 free bytes)
ARCEUS_CAVE=CAVE
SILV_CAVE=CAVE+0x20
GETHELDITEM=0x4ADFA4              # r0=data -> held item
GETFORMNO  =0x4ADF74              # r0=data -> form
PLATE_TBL  =0x3233A4             # item -> Arceus type (0 if not a plate)
MEM_TBL    =0x323434             # item -> Silvally type (0 if not a memory)
# getter branch entry points (each: r4=CoreParam; original first insn = ldr r0,[r4,#0xc])
HOOKS={0x4AF2BC:ARCEUS_CAVE, 0x4AF350:ARCEUS_CAVE,   # GetType1/2 Arceus
       0x4AF304:SILV_CAVE,   0x4AF398:SILV_CAVE}     # GetType1/2 Silvally
FORM_PERSIST={0x3236D8:0xE1A00004}                   # mov r0,r4
# Route Arceus/Silvally to the handler for ANY ability (not just Multitype/RKS) so a Protean
# Arceus/Silvally also gets form typing. Originals: bne 0x1A000007, beq 0x0A000009.
GATE_REMOVE={0x4AF2B8:0xE1A00000, 0x4AF34C:0xE1A00000,   # GetType1/2 Arceus: bne(not-Multitype)->nop (fall to handler)
             0x4AF2D8:0xEA000009, 0x4AF36C:0xEA000009}    # GetType1/2 Silvally: beq(RKS)->b (always to handler)

def bl(frm,to): return 0xEB000000|(((to-(frm+8))>>2)&0xFFFFFF)
def b (frm,to): return 0xEA000000|(((to-(frm+8))>>2)&0xFFFFFF)

def cave_words(addr, table):
    # ldr r0,[r4,#0xc]; bl GetHeldItem; bl table; cmp r0,#0; popne{..pc}; ldr r0,[r4,#0xc]; bl GetFormNo; pop{..pc}
    return [0xE594000C,
            bl(addr+0x04,GETHELDITEM),
            bl(addr+0x08,table),
            0xE3500000,
            0x18BD8070,              # popne {r4,r5,r6,pc}
            0xE594000C,
            bl(addr+0x18,GETFORMNO),
            0xE8BD8070]              # pop {r4,r5,r6,pc}

def all_edits():
    e=dict(FORM_PERSIST); e.update(GATE_REMOVE)
    for i,w in enumerate(cave_words(ARCEUS_CAVE,PLATE_TBL)): e[ARCEUS_CAVE+i*4]=w
    for i,w in enumerate(cave_words(SILV_CAVE,MEM_TBL)):    e[SILV_CAVE+i*4]=w
    for site,dst in HOOKS.items(): e[site]=b(site,dst)
    return e

def apply(code):
    code=bytearray(code)
    # sanity: hook sites must currently be `ldr r0,[r4,#0xc]` (E594000C); persist site mov r0,#1
    for site in HOOKS:
        assert struct.unpack_from("<I",code,site-BASE)[0]==0xE594000C, "hook 0x%X not ldr r0,[r4,#0xc]"%site
    assert struct.unpack_from("<I",code,0x3236D8-BASE)[0]==0xE3A00001, "persist site not mov r0,#1"
    for site in (0x4AF2B8,0x4AF34C): assert struct.unpack_from("<I",code,site-BASE)[0]==0x1A000007, "gate 0x%X not bne"%site
    for site in (0x4AF2D8,0x4AF36C): assert struct.unpack_from("<I",code,site-BASE)[0]==0x0A000009, "gate 0x%X not beq"%site
    # cave must be empty (zeros)
    for off in range(CAVE, SILV_CAVE+0x20, 4):
        assert struct.unpack_from("<I",code,off-BASE)[0]==0, "cave 0x%X not zero"%off
    for va,w in all_edits().items(): struct.pack_into("<I",code,va-BASE,w)
    return bytes(code)

if __name__=="__main__":
    code=bytearray(open("UltraMoon_FullExtract/exefs/code.bin","rb").read())
    patched=apply(code)
    # ---- Unicorn-verify the cave logic (mock GetHeldItem/GetFormNo, use real plate table) ----
    from unicorn import Uc,UC_ARCH_ARM,UC_MODE_ARM,UC_HOOK_CODE,UcError
    from unicorn.arm_const import UC_ARM_REG_R0,UC_ARM_REG_R4,UC_ARM_REG_SP,UC_ARM_REG_LR,UC_ARM_REG_PC
    TYPES=["Normal","Fighting","Flying","Poison","Ground","Rock","Bug","Ghost","Steel","Fire","Water","Grass","Electric","Psychic","Ice","Dragon","Dark","Fairy"]
    csz=(len(patched)+0xFFF)&~0xFFF
    def run_cave(cave,item,form):
        mu=Uc(UC_ARCH_ARM,UC_MODE_ARM); mu.mem_map(BASE,csz); mu.mem_write(BASE,bytes(patched))
        mu.mem_map(0x900000,0x40000); mu.reg_write(UC_ARM_REG_SP,0x930000); mu.reg_write(UC_ARM_REG_R4,0x920000)
        mu.reg_write(UC_ARM_REG_LR,0xDEAD0000)
        def hook(u,a,sz,ud):
            if a==GETHELDITEM: u.reg_write(UC_ARM_REG_R0,item); u.reg_write(UC_ARM_REG_PC,u.reg_read(UC_ARM_REG_LR))
            elif a==GETFORMNO: u.reg_write(UC_ARM_REG_R0,form); u.reg_write(UC_ARM_REG_PC,u.reg_read(UC_ARM_REG_LR))
        mu.hook_add(UC_HOOK_CODE,hook,begin=GETHELDITEM,end=GETHELDITEM)
        mu.hook_add(UC_HOOK_CODE,hook,begin=GETFORMNO,end=GETFORMNO)
        try: mu.emu_start(cave,0xDEAD0000,count=2000)
        except UcError: pass
        return mu.reg_read(UC_ARM_REG_R0)
    print("=== Unicorn verify ARCEUS plate-or-form handler (PKHeX form=Ghost(7)) ===")
    for it,n in [(0,"no plate"),(0x131,"Earth Plate"),(0x136,"Spooky Plate"),(0x4A,"King's Rock")]:
        t=run_cave(ARCEUS_CAVE,it,7); print("  form=Ghost + %-13s -> type=%s"%(n,TYPES[t] if t<18 else "?%d"%t))
    print("=== Silvally handler (form=Ghost(7)) ===")
    for it,n in [(0,"no memory"),(0x089,"some item")]:
        t=run_cave(SILV_CAVE,it,7); print("  form=Ghost + %-10s -> type=%s"%(n,TYPES[t] if t<18 else "?%d"%t))
