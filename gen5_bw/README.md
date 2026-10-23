# Black / White
**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**

The Black 2 / White 2 Un-Nerf set (`../gen5_bw2/`) carried over to Pokemon Black and Pokemon White. Nothing here
has been booted or played: each patch was only re-applied to its clean dump and compared byte for byte with the
built ROM. Pokestar Studios and the PWT do not exist in Black / White, so this set is the ban list, the clauses
and the Arceus typing.

## What changes

1. **Ban list removed** (regulation NARC `a/1/0/5`, 29 records of 188 bytes). The 18 records that carry the
   legendary ban bits (records 10-22 and 24-28, the Battle Subway's among them) get their species bitfield
   `0x1C`-`0x77` zeroed. Banned before: Mewtwo, Mew, Lugia, Ho-Oh, Celebi, Kyogre,
   Groudon, Rayquaza, Jirachi, Deoxys, Dialga, Palkia, Giratina, Phione, Manaphy, Darkrai, Shaymin, Arceus,
   Victini, Reshiram, Zekrom, Kyurem, Keldeo, Meloetta, Genesect, plus Chatot in records 15-19.
2. **Species Clause and Item Clause off** in the same 18 records (bytes `0x08` and `0x09` set to `01`, the value
   records 0-9 already have). 185 bytes change in total.
   Left alone on purpose, as in Black 2 / White 2: the party size (`0x02`/`0x03`), the Lv 50 byte (`0x04`, a
   scaling rule), every other byte of each record, and the 11 records without ban bits (0-9 and 23).
3. **Arceus form typing.** The personal table `a/0/1/6` has 669 rows (row 668 is a 1300-byte table, not a
   Pokemon), and Arceus (row 493) has FormStatsIndex 0 and 17 forms, so every form reads the Normal row. 16 rows
   are appended at 669-684 (Fighting ... Dark; Gen 5 has no Fairy), each a copy of Arceus's row with both types
   set to the form's type, and Arceus's FormStatsIndex is set to 669. The level-up learnset `a/0/1/8` and the
   evolution table `a/0/1/9` (668 rows each) grow to 685 rows: row 668 is an empty learnset / zero evolution
   row and rows 669-684 copy Arceus's own rows, because the game reads both through the same form index and
   levelling up a plate-holding Arceus without those rows stops the game (measured on Black 2, see
   `../gen45_nds_arceus_typefix/README.md`).
   The game's form-to-row function (decompressed ARM9 `0x0201AFD4`, the same instructions as Black 2's
   `0x02020480` apart from the 60/76-byte row size and the call targets) returns `FormStatsIndex + form - 1`
   whenever FormStatsIndex is not 0 and `0 < form < form count`, with no species exception, so Arceus forms
   take these rows the way Rotom, Giratina, Shaymin and the other form species already do.
   The three rebuilt tables are written into the free 0xFF space after the NTR data (`0x0C3B8E00` on Black,
   `0x0C3B9400` on White, below the DSi region at `0x0C400000`); their three FAT entries, the header's
   used-ROM-size word (`0x80`) and the header CRC (`0x15E`) are updated. Every other byte of the image,
   including the DSi (TWL) region, stays where it was, and the file stays 256 MiB.

## Files

| file | what | apply with |
|---|---|---|
| `Black_UnNerf.xdelta` | VCDIFF (xdelta3 3.0.11, `-9 -S djw -B 268435456`), clean Black -> patched Black | `xdelta3 -d -s "<clean Black>.nds" Black_UnNerf.xdelta out.nds` |
| `White_UnNerf.xdelta` | the same for White | `xdelta3 -d -s "<clean White>.nds" White_UnNerf.xdelta out.nds` |
| `bps/Black_UnNerf.bps` | BPS, no metadata, same source and target as the xdelta | `flips --apply bps/Black_UnNerf.bps "<clean Black>.nds" out.nds` |
| `bps/White_UnNerf.bps` | the same for White | `flips --apply bps/White_UnNerf.bps "<clean White>.nds" out.nds` |
| `bw_nobanlist.py` | changes 1 and 2 on your own `.nds`, in place, Python 3 standard library only | `python3 bw_nobanlist.py game.nds --out step1.nds` |
| `bw_arceus_typefix.py` | change 3 on your own `.nds`, Python 3 standard library only | `python3 bw_arceus_typefix.py step1.nds --out final.nds` |

The patches copy the three moved tables out of your own dump (source copies), so they carry only the changed
bytes and the new FAT and row values (under 1 KB of new data each). The two scripts give exactly the patch
target from a clean dump, in either order. Both refuse a ROM that is not clean Black (`IRBO`) or White (`IRAO`)
or that does not have the expected layout (29 regulation records with 18 banning ones; an unpatched Arceus row;
tables that rebuild byte for byte; free space after the NTR data), so running them twice stops at the second
run. `bw_nobanlist.py` takes `--narc`, `--records` and `--banning`
(defaults `a/1/0/5`, 29, 18), `bw_arceus_typefix.py` takes `--personal`, `--learnset` and `--evolution`
(defaults `a/0/1/6`, `a/0/1/8`, `a/0/1/9`); the personal row count and row size are read from the file.

## Input and output

| | file | size | SHA-1 | CRC32 |
|---|---|---|---|---|
| Black input | `Pokemon - Black Version (USA, Europe) (NDSi Enhanced).nds` | 268435456 | `26ad0b9967aa279c4a266ee69f52b9b2332399a5` | `4F6E5580` |
| Black output | `Pokemon - Black Version (USA, Europe) (NDSi Enhanced) [Un-Nerf UNTESTED].nds` | 268435456 | `cac3a0da78f78b0fe659ea54d39b014b36605f0d` | `AF99B86D` |
| White input | `Pokemon - White Version (USA, Europe) (NDSi Enhanced).nds` | 268435456 | `bc696a0dfb448c7b3a8a206f0f8214411a039208` | `B552501C` |
| White output | `Pokemon - White Version (USA, Europe) (NDSi Enhanced) [Un-Nerf UNTESTED].nds` | 268435456 | `f85c9a359f6b46db48e7d4ea42723cac0be3081f` | `BAF0F6A7` |

The BPS files refuse any other source CRC32. Black and White patches are not interchangeable.

A changed DSi-enhanced ROM no longer matches its DSi signatures, which matters only to a console starting it in
DSi mode with signature checks on; emulators and DS-mode loaders do not check them.

## Not in this set

* **Gen 5 formes** (Giratina-Origin kept without the Griseous Orb, Shaymin-Sky kept, Arceus forme kept without a
  Plate): a later step that covers all four Gen 5 games. The code that reverts the formes has not been located
  in Black / White yet, so those formes still revert as in the retail game.
* **Pokestar Studios props and the PWT:** they do not exist in Black / White.
* **Soul Dew, any ability, 6-Pokemon Subway:** not part of the Black 2 / White 2 set this one follows; the party
  sizes stay as in Black 2 / White 2.
