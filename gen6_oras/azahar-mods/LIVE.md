# Omega Ruby / Alpha Sapphire — which of these patches can take effect on a running game

Every patch in this folder is the same size in as out (checked from each `.bps` header: source size equals
target size for `.code` at 5439488 bytes and `a/1/7/0` at 33136 bytes), and every IPS record overwrites
existing bytes rather than appending. That makes two different runtime stories, one per target.

## `exefs/code.*` — live-writable into the running process

The 3DS loads the decompressed `.code` at virtual address `0x00100000`, and the file is laid out so that
**process address = 0x00100000 + file offset** for everything the patches touch (`.text` and its cave, the
`.rodata` literals). Each record is a same-size instruction or literal edit, so an app that can write the
guest's memory can apply a record by writing its bytes at `0x00100000 + offset`, with the game running.
Two things have to be true for that write to count:

* the emulator's translated-code cache must be invalidated for the range (Azahar's cheat and GDB paths
  do this; a raw memory poke that skips it leaves the old translation running until that block is
  re-translated);
* the write lands in the copy Azahar built at load. It is not persisted anywhere: a reset re-reads the
  ExeFS and re-applies whatever `load/mods/<TID>/exefs/code.*` says, so the on-disk mod set and the live
  write should agree, or the reset undoes the toggle.

Features whose records live in `.code` (offsets = file offsets, add `0x100000` for the process address):

| feature | bytes | sites |
|---|---|---|
| `evcap` / the `.code` half of `no_restrictions` | 4 | EV-total literals at `0xE9734` and `0x3474C0` (OR) / `0x3474B8` (AS) |
| `xerneas` | 34 | `ChangeFormNo` `0x2B4334`, `ChangeMonsNoForm` `0x1F594` |
| `formepersist` | 107 (OR) / 108 (AS) | the out-of-battle `ChangeFormNo(base)` call sites and the Hoopa reset blocks |
| `arceus` | 66 | type getters `0x2B3B98`, `0x3D3254` (OR) / `0x3D324C` (AS); Multitype gates `0x2B3B6C`, `0x3D3228` / `0x3D3220`; plate-less reset `0x2B3EB8`; cave at `0x479604` (OR) / `0x4795FC` (AS) |

`all/exefs/code.ips` is those four merged (37 records, 223 bytes OR / 224 bytes AS, highest byte
`0x47963C` / `0x479634`, all inside the 5439488-byte file). The edits are read the next time the code runs:
a type getter on the next damage calculation, a forme revert on the next save/load, the EV cap on the next
facility eligibility check. A function already executing keeps its old instruction stream until it returns.

## `romfs_ext/a/1/7/0.*` — takes effect after a LayeredFS rebuild

RomFS patches are applied by Azahar's LayeredFS when the title is **loaded**: the patched file replaces the
original for every read the game makes through the file-system service from then on. There is no live
write to make here; the way to toggle it on a running game is

1. save state,
2. reset (a full `System::Load`, which rebuilds LayeredFS from the current `load/mods/<TID>/romfs_ext/`),
3. load state.

The state carries the game's memory, not the RomFS, so everything the game reads after the load-state sees
the new file. `a/1/7/0` is the Battle Maison rule archive; the game reads it when the Maison is entered, so
the ban list and the clause flags change on the next entry. The one caveat is generic: any RomFS file the
game has **already** read into memory stays as it was until the game reads it again. For OR/AS the only
patched RomFS file is `a/1/7/0`, so that means "leave the Maison and come back".

Feature on this target: `nobanlist` / `no_restrictions` (`a/1/7/0`, 402 bytes, byte-identical to each other).
