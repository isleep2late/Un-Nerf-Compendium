# Ultra Sun / Ultra Moon — which of these patches can take effect on a running game

Every patch in this folder is the same size in as out, checked from each `.bps` header: `.code` 5914624,
`Battle.cro` 1294336, `a/1/4/1` 30372 and `a/0/3/2` 2793100 bytes, source and target alike (that includes
the `a/0/3/2` text repacks, which move data around inside the archive but do not change its length). Every
IPS record overwrites existing bytes. The runtime story differs by target.

## `exefs/code.*` — live-writable into the running process

US/UM store `.code` flat, and the 3DS maps it at virtual address `0x00100000` with
**process address = 0x00100000 + file offset** for everything the patches touch. Each record is a same-size
instruction edit, so an app that can write the guest's memory can apply one by writing its bytes at
`0x00100000 + offset` while the game runs, provided that

* the emulator's translated-code cache is invalidated for the range (Azahar's cheat and GDB paths do this;
  a raw poke that skips it leaves the old translation running until the block is re-translated), and
* the on-disk mod set agrees with the live write: a reset re-reads the ExeFS and re-applies
  `load/mods/<TID>/exefs/code.*`, so a live toggle that is not mirrored there is undone by the next reset.

Features whose records live in `.code` (file offsets; add `0x100000` for the process address):

| feature | bytes | sites |
|---|---|---|
| `formepersist` | 84 | 17 out-of-battle `ChangeFormNo(base)` call sites + the Hoopa destructive-reset calls |
| `resort` | 4 | the Poké Resort item-form `ChangeFormNo` call at `0x1E05A0` |
| `arceus` (`.code` part) | 82 | two handlers in the `.text` cave at `0x4B99F8` (UM) / `0x4B99F0` (US), both type getters hooked, the Multitype/RKS gates removed, the plate-less reset at `0x2236D8` |

`all/exefs/code.ips` is those three merged (30 records, 182 bytes, highest byte `0x4B9A38` UM /
`0x4B9A30` US, all inside the 5914624-byte file). They are read the next time the code runs: a forme revert
on the next save/load, the Arceus/Silvally typing on the next type lookup.

## `romfs_ext/*` — takes effect after a LayeredFS rebuild

RomFS patches are applied by Azahar's LayeredFS when the title is **loaded**; from then on every read the
game makes of that file through the file-system service gets the patched bytes. Nothing here is written
live. To toggle on a running game:

1. save state,
2. reset (a full `System::Load`, which rebuilds LayeredFS from `load/mods/<TID>/romfs_ext/`),
3. load state.

The state carries memory, not the RomFS, so reads made after the load-state see the new files. What that
means per file:

* **`Battle.cro`** (`prankster` 1 byte, `galewings` 4, `parentalbond` 1, `souldew` 157, `arceus` 4;
  merged 6 records, 196 bytes, highest byte `0x102674`). A CRO is loaded into memory when a battle starts
  and unloaded when it ends. **A `Battle.cro` that is already loaded stays the old one until the next battle
  loads it**: a Soul Dew toggle made during a Battle Tree run applies from the next battle, not the current
  turn. (A save state taken mid-battle carries the old CRO in memory for the same reason.)
* **`a/1/4/1`** (`nbl`, 171 bytes) is the Battle Tree rule table, read when the Tree is entered: leave and
  re-enter after the reset.
* **`a/0/3/2`** (`galewings` / `souldew` text, or the `all/` repack) is the English text archive; the
  rewritten Soul Dew and Gale Wings descriptions show once the game re-reads that text bank. This file is
  cosmetic: the behaviour lives in `Battle.cro`. Only one `a/0/3/2` patch may be present at a time (see
  `README.md`).

The `.code` records could in principle be written live and the RomFS ones cannot, so a "toggle" on US/UM
is really: write the `.code` records into the process now, and expect the `Battle.cro` / `a/1/4/1`
behaviour after the save-state, reset, load-state cycle and the next battle / facility entry.
