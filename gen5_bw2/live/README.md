# Black 2 / White 2 — NoBanList as in-place (live-safe) patches

`bw2_nobanlist.py --banlist-only` edits the regulation NARC `a/1/0/6` **in place** and keeps the ROM's
length. These four files are that edit alone, as a patch, so it can be applied to a game that is already
running: every record overwrites bytes inside the NARC, nothing moves, nothing grows, and the 512 MiB
image stays 512 MiB. The rest of the BW2 stack (Arceus form-typing, the Pokéstar Bad-Egg validator fix,
the prop back-sprites) rebuilds the ROM with ndspy and stays in the whole-ROM `../bps/` and `../*.xdelta`
files, which must never be applied to a running game.

## What it removes (from the script's docstring)

Every one of the 22 banning regulation files in `a/1/0/6` (the fixed 188-byte structures used by the
Battle Subway, the Battle Institute and the PWT) gets:

* the species ban-list bitfield `0x1C`–`0x77` zeroed (every legendary and mythical allowed);
* the Species Clause byte `0x08` set to `01` (duplicates allowed);
* the Item Clause byte `0x09` set to `01` (duplicate held items allowed).

Left alone on purpose: the party-size limit (`0x02`/`0x03`; the game crashes without it) and the PWT cup
index (`0xBA`; the bracket loader freezes without it). So the facilities still ask for 3 / 4 / 6 Pokémon at
Lv50, they just stop refusing anything.

225 bytes change, in 22 files. The patch carries 275 bytes in 127 records because runs closer than eight
bytes are merged into one record; the extra 50 bytes are rewritten with their original values.

## Files

| file | container | bytes | records | span |
|---|---|---|---|---|
| `Black2_NoBanList.ips` | **IPS32** (`IPS32` magic, u32 offsets, `EEOF`) | 1046 | 127 | `0xC31AEC4`–`0xC31C230` |
| `White2_NoBanList.ips` | **IPS32** | 1046 | 127 | `0xC31ACC4`–`0xC31C030` |
| `Black2_NoBanList.bps` | BPS, no metadata, keyed to the No-Intro dump | 154 | — | whole image |
| `White2_NoBanList.bps` | BPS, no metadata, keyed to the No-Intro dump | 154 | — | whole image |

**Why IPS32 and not IPS.** `a/1/0/6` sits at file offset `0xC31A600` (195 MiB into the image). A classic
IPS record has a 24-bit offset and cannot address anything past 16 MiB (`flips` refuses to create one:
`ips_16MB`). IPS32 is the same record shape with a 32-bit offset and an `EEOF` terminator; it is what the
app's `IpsPatch` decoder and the Switch scene already read. `flips` does not read IPS32, so these two files
were verified with the Python applier in the work log, not with `flips --apply`. No record starts at
`0x454F46` or `0x45454F46` (the terminators read as offsets), no record uses RLE, the longest record is
15 bytes, and every record lies inside `a/1/0/6`, which lies inside the NTR image (`0x11831A00` B2 /
`0x117DD400` W2), well short of the 512 MiB file.

**One IPS32 serves both dump families.** The records were derived from the No-Intro dumps and again from
the owner's `Black 2.nds` / `White 2.nds`; the two record lists are **identical** for each game (offsets and
bytes, including the merged-run filler), because `a/1/0/6` and its FAT entry are at the same place with the
same contents in both dumps. The BPS files are keyed by CRC32 to the No-Intro dumps only and refuse the
owner's dumps (`73EEC470` / `B72BBA42`), which is the expected BPS behaviour, not a defect: use the IPS32 on
a dump whose CRC32 the BPS does not name. Black 2 and White 2 differ (the NARC is 0x200 bytes earlier in
White 2); do not cross them.

## Hashes

| | size | SHA-1 | CRC32 |
|---|---|---|---|
| B2 base — `Pokemon - Black Version 2 (USA, Europe) (NDSi Enhanced).nds` (No-Intro) | 536870912 | `e51e6dfb8678a3d19dcd2a10691b96a569ca0abb` | `D4427FD1` |
| B2 after either patch | 536870912 | `fd8765b74c05e0a3f3ca830e15f200628243bd0c` | `F203533F` |
| B2 owner dump `Black 2.nds` | 536870912 | `5da09a39424f1a76c52a3eebad9b5e8dcacb71ba` | `73EEC470` |
| B2 owner dump after the IPS32 | 536870912 | `63164d228be891192d0b7b6ffa956f42ce5bfc40` | `55AFE89E` |
| W2 base — `Pokemon - White Version 2 (USA, Europe) (NDSi Enhanced).nds` (No-Intro) | 536870912 | `b5d7490be7b415b8f1e672a53e978a9cc667e56a` | `777EB04F` |
| W2 after either patch | 536870912 | `d102c97822cd3eeb5ee2d340e907eef76d59b475` | `71DECA57` |
| W2 owner dump `White 2.nds` | 536870912 | `332c5758e8bef5be2c167e9fa69a14bbdbf3c435` | `B72BBA42` |
| W2 owner dump after the IPS32 | 536870912 | `76d8d27496430667f325fec2c848656edf753d68` | `B18BC05A` |

The "after" hashes are the script's own output (`--banlist-only`, same-size), and both the `flips --apply`
of the BPS and the IPS32 applier reproduce them byte for byte.

| patch file | SHA-256 |
|---|---|
| `Black2_NoBanList.ips` | `3d41e6279fb149b2cf2c9d1fe8d168ae794726ef5d26568f4d4899d6747da3ff` |
| `Black2_NoBanList.bps` | `1ea0f0fa1e50df1c42b452fb5993b68aec510a392529b1f64dfe60b78a87c56f` |
| `White2_NoBanList.ips` | `e76a5856d5ac40fceff3f5352228b1275463ff6cfea248fa778b3d8b52ac3245` |
| `White2_NoBanList.bps` | `3db14b84bb40d342badea008d65cdb2dd77b245b6f176082c905941415d806b9` |

## Applying to a running game

The records are data, not code. The game fetches a regulation file from `a/1/0/6` over the card bus when
a facility builds its rule set, not at boot, so a write made while the player is in the overworld is
expected to show at the next registration (the first thing to test in the app: patch in the overworld, walk
up to the Subway desk, register a Mewtwo). melonDS serves every card command from the cart object's own ROM buffer, so writing the
records into that buffer is the whole job; there is no cache to flush. A challenge already in progress
keeps the rules it loaded. Reversing is writing the original bytes back at the same offsets (the app can
take them from its untouched copy of the image); the save is untouched either way, because nothing here
writes to the save and nothing in the save records which rule set was in force.

## Not in these files

Arceus form-typing (personal NARC rebuild), the Pokéstar Bad-Egg arm9 fix and the prop back-sprites
all rebuild the ROM (ndspy), which relays the FAT and moves files. They live only in
`../Black2_UnNerf_Pokestar.xdelta`, `../White2_UnNerf_Pokestar.xdelta` and `../bps/`, keyed as
`../bps/SOURCES.md` describes, and are for a ROM file before boot.

```
python3 bw2_nobanlist.py "<dump>.nds" --banlist-only   # -> <dump>_norestrictions.nds, same size
flips --create --bps "<dump>.nds" "<dump>_norestrictions.nds" <Game>_NoBanList.bps
flips --apply <Game>_NoBanList.bps "<No-Intro dump>.nds" out.nds
```
