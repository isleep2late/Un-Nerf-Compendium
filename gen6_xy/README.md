# Gen 6 - Pokemon X / Y: Battle Maison rules, 510 EV cap, Xerneas ability, Arceus typing, forme persistence
**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**

Status: **untested**. Everything here was built from static analysis only: the Omega Ruby / Alpha Sapphire set in
`gen6_oras/` and `gen67_arceus_typefix/` was mapped onto the X/Y executable by masked instruction signatures
(branch targets and pointers masked; every mapped function was compared instruction by instruction with its OR/AS
counterpart) and onto the X/Y Battle Maison rule file record by record. Nobody has booted a patched game, entered the
Maison or fought a battle with it yet. Keep a backup of your save before you try it.

This is the OR/AS Un-Nerf set for X and Y. X/Y has no Hyperspace Fury, no Hoopa and no Mega Rayquaza, so the OR/AS
pieces for those have nothing to change here.

## What it changes

| feature | file | what it does |
|---|---|---|
| `nobanlist` | RomFS `a/1/8/9` | The Battle Maison rule archive. X/Y's 38 records of 604 bytes are byte-identical to records 0-37 of OR/AS `a/1/7/0`, so the OR/AS edit is applied to the same bytes: the banned-species bit field (record bytes 10-100: Mewtwo, Mew, Lugia, Ho-Oh, Celebi, Kyogre, Groudon, Rayquaza, Jirachi, Deoxys, Dialga, Palkia, Giratina, Phione, Manaphy, Darkrai, Shaymin, Arceus, Victini, Reshiram, Zekrom, Kyurem, Keldeo, Meloetta, Genesect, Xerneas, Yveltal, Zygarde, Diancie, Hoopa, Volcanion) and the Soul Dew item-ban bit (record byte 294) are cleared in every record, record 24 byte 408 is cleared, and, exactly as the OR/AS patch does at `0x26-0xFF` of its archive, the archive's table header from `0x26` to the end of the `BTAF` tag (`0x26-0xC3` here) is cleared. The OR/AS patch made this way was checked in game on OR/AS (Maison Single accepted Latias, Latias and Mew); on X/Y it is untested. 323 bytes. |
| `evcap` | ExeFS `.code` | The Maison/facility eligibility check (vaddr `0x1D58BC`, instruction-identical to OR/AS `0x1E95A0`) refuses a Pokemon whose EV total is above 510; its literal at `0x1D5A50` goes from 510 to 1530, so maxed-EV Pokemon are eligible. 1.5: `0x1D5D48`. |
| `xerneas` | ExeFS `.code` | The two personal-table `SetTokusei` writes become `blne`, skipped for species 716, so an edited Ability stays on Xerneas: `ChangeFormNo` (`0x39C374`, instruction-identical to OR/AS `0x3B41CC`) at `0x39C4DC`, and `ChangeMonsNoForm` at `0x39C5F8`. Same words as the OR/AS patch. 1.5: `0x39D6D4`, `0x39D7F0`. |
| `arceus` | ExeFS `.code` | Arceus takes its form's type when it holds no Plate (a Plate still wins), and a plate-less form is no longer reset. The Multitype gate in both type getters is NOPed (`0x39BD14`, `0x4A1A58`), both getters branch (`0x39BD40`, `0x4A1A84`) to a 56-byte cave at the end of `.text` (`0x53BEDC`, zero padding inside the mapped text pages) that calls GetHeldItem (`0x4A0C84`), ItemToForm (`0x39BF44`) and GetFormNo (`0x152FA8`), and ItemToForm's `mov r0,#1` at `0x39C060` is NOPed. One correction against the OR/AS cave: OR/AS calls `0x3B3D98`, the literal word just before ItemToForm's first instruction (`0x3B3D9C`, the address the game's own caller uses); in X/Y that word would decode as a conditional write to `r1` (the item), so the X/Y cave calls the real first instruction. 1.5: gates `0x39CF0C` / `0x4A34DC`, hooks `0x39CF38` / `0x4A3508`, cave `0x53ED4C`, persist `0x39D258`. |
| `formepersist` | ExeFS `.code` | The out-of-battle form reverts are disabled, as `formepersist.py --full` does on OR/AS, so a form set with PKHeX/PKHaX survives save and reload: the party Mega revert (`0x43FD80`, the OR/AS `0x46F0C8` block; X/Y has no Primal Kyogre/Groudon calls), the Shaymin-Sky reverts (`0x43EA4C`, `0x43F5E8`, `0x43F728`, `0x43F928`, `0x43FAEC`), the Furfrou trim reverts (`0x43F61C`, `0x43F68C`, `0x43F6FC` with its two trim-timer setters `0x43F708`/`0x43F714`, `0x43F824` with `0x43F830`/`0x43F83C`) and the Keldeo-Resolute revert for a Keldeo without Secret Sword (`0x43F9CC`), each a `bl ChangeFormNo` (or setter) replaced by `mov r0,r0`. The X/Y deposit function ends its Furfrou revert with a tail branch `b ChangeFormNo` after `pop {r4,lr}` (`0x43EA84`); it becomes `bx lr`, which returns exactly where the call would have. Every site is matched to an OR/AS site the OR/AS set patches (same function shape, same offset in the function). 17 words. 1.5: the same functions at their 1.5 addresses (all found uniquely). |

The form-setting code (Mega Evolution, Furfrou trimming, the Gracidea) is untouched, so forms still change normally.

## Files

```
xy_unnerf.py            patches files you extracted yourself: the decompressed ExeFS .code (1.0 or 1.5) and a/1/8/9
complete_unnerf.py      the gen6_oras one-pass tool, set to X/Y: bakes azahar-mods/<TID>/v1.0/all into a copy of a decrypted .3ds
complete_unnerf.bat     Windows wrapper for complete_unnerf.py
azahar-mods/
  0004000000055D00/     Pokemon X        0004000000055E00/   Pokemon Y   (same shape)
    v1.0/all/exefs/code.ips, code.bps            every .code feature merged (clean 1.0 .code -> patched)
    v1.0/all/romfs_ext/a/1/8/9.ips, 9.bps        Battle Maison rule archive
    v1.0/features/<feature>.code.ips/.bps, nobanlist.a-1-8-9.ips/.bps
    v1.5/all/exefs/code.ips, code.bps            the same .code features for the 1.5 update's .code
    v1.5/features/<feature>.code.ips/.bps
```

The IPS and BPS files were made by running `xy_unnerf.py` on the extracted clean file and diffing the result; the
`all/` patch equals the four `features/` code patches together (no shared offsets). The IPS for `.code` is the same
file for X and Y (the 28 bytes that differ between the X and Y executables are not near any patched site); the BPS
files are per title because they check the whole file's CRC32.

## Inputs (exact)

| title | file | size | CRC32 | SHA-1 |
|---|---|---|---|---|
| X | `Pokemon X (USA) (En,Ja,Fr,De,Es,It,Ko).3ds` (No-Intro, decrypted, `.code` stored compressed) | 2147483648 | | `20b7278f10a32ab440b4dff1d382a1401590aceb` |
| Y | `Pokemon Y (USA) (En,Ja,Fr,De,Es,It,Ko).3ds` (same) | 2147483648 | | `d41e08f879c741c1866eb33447c6a8d4e9b1a1f5` |
| X 1.0 | ExeFS `.code`, decompressed | 5156864 | `3778C475` | `cee48dc75e0711de439954f34158fa42281234e2` |
| Y 1.0 | ExeFS `.code`, decompressed | 5156864 | `46BDAD57` | `4a0c0b79fdfa68d383cf55eca1969b6a7e0316bf` |
| X and Y | RomFS `a/1/8/9` | 23776 | `51C67A3F` | `8126e28efa78ad0e76d6fd6d9627cf63be2f6c36` |
| X 1.5 | update `0004000E00055D00`, ExeFS `.code`, decompressed (from `Pokemon X Update v1.5 (All) for Citra.cia`, SHA-1 `48c4fb433ce51fcb661ecbbb01a5fd8ea09c2270`) | 5169152 | `BA7B14F7` | `e0343026b064bb30f43e6c401c308d56d5b9cf31` |
| Y 1.5 | update `0004000E00055E00`, ExeFS `.code`, decompressed (from `Pokémon Y Update v1.5 (All) for Citra.cia`, SHA-1 `0721bbf65b6efc1615fa6ae065de3b55433a30ab`) | 5169152 | `3E056C1E` | `df2fa3527a73cd0b5d61816385c4c3fb6fa52e39` |

The 1.5 update does not ship `a/1/8/9`; the game keeps using the base file.

## How to apply

**A whole decrypted cartridge dump (recommended):**

```
python3 complete_unnerf.py "Pokemon X (USA) (En,Ja,Fr,De,Es,It,Ko).3ds"
```

It writes `Pokemon X (USA) (En,Ja,Fr,De,Es,It,Ko) [Un-Nerf UNTESTED].3ds` next to the input (or `--out <file>`),
checks every patch against the input first (BPS CRC32s, RomFS hash tree) and writes nothing if anything is off. The
No-Intro dumps store `.code` compressed, so the tool stores the patched `.code` uncompressed and moves the RomFS and
the later partitions up by `0x231000` bytes inside the same 2 GiB image, then rebuilds the ExeFS, ExeFS/exheader/RomFS
superblock hashes and the RomFS hash tree. Use the base game without the 1.5 update installed, or install the 1.5
code patch below as well.

| output | SHA-1 |
|---|---|
| `Pokemon X (USA) (En,Ja,Fr,De,Es,It,Ko) [Un-Nerf UNTESTED].3ds` | `1ec6639b96acf97a9b097aa3923a026152275b78` |
| `Pokemon Y (USA) (En,Ja,Fr,De,Es,It,Ko) [Un-Nerf UNTESTED].3ds` | `2dcfef17be87276a518ea672f08277c83bb93338` |

**Azahar `load/mods` (code features only):** copy the contents of `azahar-mods/<TID>/v1.0/all/` (or `v1.5/all/` if
the 1.5 update is installed; Azahar applies mods to the update's `.code` when one is installed) into
`<Azahar user dir>/load/mods/<TID>/`. Keep either the `.ips` or the `.bps` of a file. Azahar applies `romfs_ext`
patches only at the RomFS root and skips nested paths such as `a/1/8/9` without a log line (measured on the OR/AS
folder), so through `load/mods` you get the `.code` features but **not** the Maison rules; use `complete_unnerf.py`
or `xy_unnerf.py` for those. A 1.0 code patch on the 1.5 `.code` (or the reverse) is refused (BPS) or corrupts it
(IPS).

**Files you extracted yourself** (for a `.cia`, a LayeredFS setup, or a selection of features):

```
python3 xy_unnerf.py code code.bin --out code.unnerf.bin [--features evcap,xerneas,arceus,formepersist]
python3 xy_unnerf.py maison 9 --out 9.unnerf
```

`code.bin` must be the decompressed `.code`. The tool picks the 1.0 or 1.5 table by checking every site's original
bytes and refuses a file where any site holds something else. Patched results:

| result | CRC32 | SHA-1 |
|---|---|---|
| X 1.0 `.code`, all features | `A511FD14` | `e13d6c47d6a62df26bd172aa1c9eb63b35e93bce` |
| Y 1.0 `.code`, all features | `D4D49436` | `fadd7cb928b8146955182316b0f23b937b43acdc` |
| X 1.5 `.code`, all features | `9B14F745` | `e54949ab7794614bd296fdec40f9ff55fb16c829` |
| Y 1.5 `.code`, all features | `1F6A8FAC` | `8181e081eac3dc3121ad7bdd795ae2e4ea662b18` |
| `a/1/8/9` | `ACF2AA78` | `bf08110d3e5bdf367a31d9dda7e63ccd660c5d55` |

Any IPS/BPS tool (Floating IPS, Rom Patcher JS) applies the `azahar-mods` files to the extracted files with the same
results.

## Skipped, and why

- **Battle Spot EV cap.** The OR/AS EV-cap patch also raises the Battle Spot validator's 510 (OR `0x4474C0`). That
  validator (six byte-sized EV checks then the total, OR `0x4472A8`) has no counterpart in X/Y's `.code` or in any of
  its 133 `.cro` modules by instruction pattern; the other 510 literals in X/Y's `.code` are EV-gain clamps
  (vitamins, battle EVs), which the OR/AS set leaves alone too. Only the Maison eligibility cap is lifted. The OR/AS
  notes call the Battle Spot one harmless for single-player play.
- **Hyperspace Fury, Hoopa forme persistence, Primal reverts, Mega Rayquaza.** None of these exist in X/Y.
- **Protean on Arceus** is not solved (it is not solved in OR/AS either).
- **Full-file `romfs/` overrides** for Azahar are not shipped (they would be game files); use `complete_unnerf.py`.
- **A `.cia` whose `.code` is compressed** is refused by `complete_unnerf.py` (as in OR/AS); extract and use
  `xy_unnerf.py`, or use the `.3ds`.
- Nothing was tested in game or in an emulator.
