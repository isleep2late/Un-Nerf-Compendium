**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**
# Sun / Moon

The Ultra Sun / Ultra Moon Un-Nerf set (`../gen7_usum/`) carried over to Pokemon Sun and Pokemon Moon, for the
1.0 cartridge files and for the files of the 1.2 update. Nothing here has been booted or played. Every site was
found by masked ARM signature against the US/UM files the Compendium already ships patches for, its original bytes
were checked, and each patch was only re-applied to its clean input and compared byte for byte with the built ROM
(`results-logs/unnerf-untested/sm/apply.log`).

All edits keep every file the same size. Sun and Moon share `Battle.cro`, `a/1/3/7` and `a/0/3/2`; only the
ExeFS `.code` differs per title, and every `.code` site sits at the same address in both.

## What changes

Offsets are file offsets inside the named file (`.code`: virtual address minus `0x100000`). "1.0" is the cartridge
(No-Intro dump), "1.2" the files of the 1.2 update title (`0004000E00164800` / `0004000E00175E00`).

| feature | file | 1.0 | 1.2 | what it does |
|---|---|---|---|---|
| `nbl` | `a/1/3/7` | 112 runs, 210 bytes | (the update has no `a/1/3/7`; the 1.0 patch applies) | Battle Tree rules: the 24 rule records use the US/UM 1192-byte format. In the 14 records that carry the clauses (1, 4, 7, 10 and 12-21) the Species Clause (+0x0E) and Item Clause (+0x0F) flags go `01` -> `00` and the banned-species bitfield (+0x7C, 38 species: Mewtwo, Mew, Lugia, Ho-Oh, Celebi, Kyogre to Deoxys, Dialga, Palkia, Giratina, Phione to Victini, Reshiram, Zekrom, Kyurem to Genesect, Xerneas to Volcanion, Cosmog to Lunala, Necrozma, Magearna, Marshadow) is cleared. Unlike US/UM, Sun/Moon also ban legendaries in records 12-14, so those are cleared too. Level and team-size bytes are not touched (as in US/UM). |
| `prankster` | `Battle.cro` | `0x24288` | `0x24288` | `beq` -> `b`: Prankster moves affect Dark types again. |
| `galewings` | `Battle.cro` + `a/0/3/2` | `0xD6208` | `0xD672C` | `beq` -> `nop`: Gale Wings gives priority at any HP. Description: bank 97 line 177 becomes the Gen 6 text. |
| `parentalbond` | `Battle.cro` | `0x24440` | `0x24440` | second hit `0x400` -> `0x800` (0.25x -> 0.5x). |
| `souldew` | `Battle.cro` + `a/0/3/2` | handler `0xF7E00`, entry `0xB79C0` | handler `0xF8400`, entry `0xB7EA0` | The US/UM 184-byte handler (+50 % Sp. Atk and Sp. Def for Latios/Latias, no facility gate) re-assembled into the zero padding between `.text` and `.rodata` (1.0 `0xF7D44`-`0xF8000`, 1.2 `0xF8358`-`0xF9000`); its three calls re-targeted to the Sun/Moon functions (1.0 `0x85A08`, `0x90034`, `0x80F58`; 1.2 `0x85DD8`, `0x90450`, `0x81314`), found as the calls the Sun/Moon Soul Dew handler itself makes at the same positions. The old handler's first instruction becomes a branch to it. Description: bank 35 line 225 becomes the Gen 6 text. |
| `hyperspacefury` | `Battle.cro` | `0xC4250` | `0xC4730` | `bne` -> `b`: any Pokemon can use Hyperspace Fury. |
| `darkvoid` | `Battle.cro` | `0xC05AC` | `0xC0A8C` | `bne` -> `b`: any Pokemon can use Dark Void (Gen 6 rule). |
| `formepersist` | `.code` | 18 words | 18 words | `mov r0,r0` over 14 out-of-battle `bl ChangeFormNo` revert calls (1.0 `ChangeFormNo` = VA `0x31BB4C`, 1.2 `0x31C250`) and the 4 Hoopa extra-reset setter calls, so altered formes survive save/load. These are the Sun/Moon counterparts of the US/UM `formepersist.py --full` set (17 table sites + Hoopa): 14 of the 17 table sites exist in Sun/Moon (the 11 instructions before each are identical to US/UM apart from call targets); US/UM `0x3453B4`, `0x3453E8`, `0x34541C` have no counterpart. Everything `formepersist.py`'s own auto-locator finds in Sun/Moon is included. 1.0: `0x330614 0x330DD8 0x330E0C 0x330E40 0x3310CC 0x3310F8 0x331144 0x33129C 0x3313FC 0x3314CC 0x33156C 0x3315F4 0x3317FC 0x345838` + `0x331150 0x33115C 0x3312A8 0x3312B4`; 1.2: `0x332034 0x332888 0x3328BC 0x3328F0 0x332B7C 0x332BA8 0x332BF4 0x332D4C 0x332EAC 0x332F7C 0x33301C 0x3330A4 0x3332AC 0x347474` + `0x332C00 0x332C0C 0x332D58 0x332D64`. |
| `resort` | `.code` | `0x1D9ABC` | `0x1D9D30` | Poke Pelago keeps item-derived formes: the one `bl ChangeFormNo` that `resort_itemform_persist.py`'s own signature finds (the US/UM Poke Resort site) -> `nop`. |
| `arceus` | `.code` + `Battle.cro` | see right | see right | Arceus and Silvally take their form's type when no Plate/Memory is held (US/UM `s2_arceus_port.py` scheme): form reset `mov r0,#1` -> `mov r0,r4` (1.0 `0x21B4FC`, 1.2 `0x21BC00`); in both type getters (1.0 VA `0x4931F4`/`0x493288`, 1.2 `0x494E7C`/`0x494F10`) the Multitype/RKS gates are removed and the Arceus/Silvally branches jump to two 8-word handlers in the `.text` end padding (1.0 `0x4957A0`, 1.2 `0x4976A0`) that call the game's GetHeldItem, plate and memory tables (byte-identical to US/UM) and GetFormNo. `Battle.cro` Protean species list `ed010503` -> `ffffffff` (1.0 `0xFD568`, 1.2 `0xFE568`). |

The two description lines are rewritten in place: the new text is shorter, so it is written into the line's own
slot and only that line's length changes; no other line, bank or archive offset moves (US/UM's `gametext.py`
repacks the whole archive instead). Decoding all 122 banks before and after shows exactly these two lines changed.

## Files

```
sm_unnerf.py         per-feature patcher for your own extracted files (1.0 or 1.2); standard library only
complete_unnerf.py   bakes azahar-mods/<TID>/v1.0/all into a copy of a decrypted .3ds or .cia (the Compendium's
                     gen7_usum tool with the Sun/Moon title IDs)
Sun_UnNerf.xdelta    clean Sun .3ds  -> the Un-Nerf .3ds (xdelta3)
Moon_UnNerf.xdelta   clean Moon .3ds -> the Un-Nerf .3ds (xdelta3)
azahar-mods/0004000000164800/   Sun        azahar-mods/0004000000175E00/   Moon
  v1.0/all/exefs/code.ips|.bps, romfs_ext/Battle.cro.ips|.bps, romfs_ext/a/1/3/7.ips|.bps, romfs_ext/a/0/3/2.ips|.bps
  v1.0/features/<feature>.<file>.ips|.bps   (file = code, Battle.cro, a-1-3-7, a-0-3-2)
  v1.2/all/...  v1.2/features/...           the same for the 1.2 update's .code, Battle.cro and a/0/3/2
```

Every patch was made by running `sm_unnerf.py` (one mode, or `all`) on the clean file and diffing; the
`features/` patches touch disjoint offsets, including the two `a/0/3/2` ones, so any subset can be stacked.

## Inputs (clean)

| input | SHA-1 |
|---|---|
| `Pokemon Sun (USA) (En,Ja,Fr,De,Es,It,Zh,Ko).3ds` (decrypted No-Intro dump, 4 GiB) | `ee95c40e484c4ad51fee190abe11a5b6b55365e0` |
| `Pokemon Moon (USA) (En,Ja,Fr,De,Es,It,Zh,Ko).3ds` | `deb6831605df5ccad1a6c7fe910d779ea508e5c1` |
| Sun 1.0 `.code` (5713920 bytes, CRC-32 `DE47EF73`) | `271b0c77432c339fdd6086475646a9a00586214e` |
| Moon 1.0 `.code` (5713920, `02948BA5`) | `90b2a20adcf67a0508f7380381e4f32b2d9255d3` |
| 1.0 `Battle.cro` (1273856, `D582C507`) | `233500e8a8a06e785dbc5f9dd80d376d3343eeff` |
| 1.0 `a/1/3/7` (29160, `FCB8926D`) | `6d0192e7a403fab3658592e9f90cf486ce444730` |
| 1.0 `a/0/3/2` (2342124, `7C6240E6`) | `40567be024b3e41cae82c6470d5d9643bd1fed5c` |
| `Pokemon Sun Update v1.2 for Citra - pokemonerdotcom.cia` | `0a4e0d8fdae0a728d795baede6c1ce5ce2dffd12` |
| `Pokemon Moon Update v1.2 for Citra - pokemonerdotcom.cia` | `92576c9d573e729f21b85a844bd9cb3a22e20782` |
| Sun 1.2 `.code` (5722112, `2A28CFAD`) | `b53cddb1ef1c87513d70652c7488e254a21d2a30` |
| Moon 1.2 `.code` (5722112, `8E4AE24C`) | `ff3a56730c2b5e389ae11849a48ee7691d77e8a5` |
| 1.2 `Battle.cro` (1277952, `9EE998A6`) | `b3288d341f92bfd08ca8daececc83a4c9cfa5cd1` |
| 1.2 `a/0/3/2` (2342128, `A04DCEDE`) | `330aa26009c7f09997426f1107ac73839cac4b81` |

## How to apply

* **Whole game, one step:** `python3 complete_unnerf.py "Pokemon Sun (USA) (En,Ja,Fr,De,Es,It,Zh,Ko).3ds"`
  writes `... [Un-Nerf UNTESTED].3ds` next to it (`--out` to choose, `--verify` to only check). The `.code` is
  stored flat in these dumps, so the image keeps its layout. The script also accepts a decrypted `.cia`; that
  route was not tried for Sun/Moon.
* **xdelta3:** `xdelta3 -d -s "<clean Sun .3ds>" Sun_UnNerf.xdelta "<out>.3ds"` (Moon likewise). The source must be
  the 4 GiB dump above.
* **Your own extracted files:** `python3 sm_unnerf.py --mode all --code code.bin --battle Battle.cro --tree a/1/3/7 --text a/0/3/2 --out-dir out`
  (or one mode: `nbl`, `prankster`, `galewings`, `parentalbond`, `souldew`, `hyperspacefury`, `darkvoid`,
  `formepersist`, `resort`, `arceus`; `--verify` checks only). It accepts the 1.0 files and the 1.2 update's files
  (told apart by size) and refuses any file whose bytes at a site are not the expected ones.
* **Azahar `load/mods`:** copy the contents of `azahar-mods/<TID>/v1.0/all/` into `load/mods/<TID>/` for a 1.0
  game, or `v1.2/all/` when the 1.2 update is installed (Azahar applies mods to the update's `.code` and
  `Battle.cro` then). As measured on the US/UM folder, Azahar applies `romfs_ext` patches only at the RomFS root:
  `code` and `Battle.cro` are applied, the nested `a/1/3/7` (Tree rules) and `a/0/3/2` (descriptions) are not; for
  those use `complete_unnerf.py` or `sm_unnerf.py`. Keep either the `.ips` or the `.bps` of a file, not both.

**The 1.2 update and the baked images:** the update ships its own `.code`, `Battle.cro` and `a/0/3/2`, which
replace the patched 1.0 ones. Run the `[Un-Nerf UNTESTED].3ds` without the update, or keep the update and add
`azahar-mods/<TID>/v1.2/all` as Azahar mods (the Tree rules then still come from the baked 1.0 `a/1/3/7`).

## Outputs

| output | SHA-1 |
|---|---|
| `Pokemon Sun (USA) (En,Ja,Fr,De,Es,It,Zh,Ko) [Un-Nerf UNTESTED].3ds` | `79f1cdbce34a7c461dd47dfd7e9eb0c2bf88e14c` |
| `Pokemon Moon (USA) (En,Ja,Fr,De,Es,It,Zh,Ko) [Un-Nerf UNTESTED].3ds` | `01c66a14d2096272e0dbf51e962e6640579a5de6` |
| Sun 1.0 `.code` after `all` (CRC-32 `68330AE1`) | `2f90f102bbe5ad13333a1861460dadf59e2ec5ac` |
| Moon 1.0 `.code` after `all` (`B4E06E37`) | `cdc1ef2d579f64e40df2ca94d7e1b4ec6e81d480` |
| 1.0 `Battle.cro` after `all` (`4CE8DC54`) | `862f1415036ea13b3d7002aca8b4ab65571c8de6` |
| `a/1/3/7` after `all` (`1ACB1294`) | `640fc5ddcf931dfc98bacdc2f427e0ff7b7784f2` |
| 1.0 `a/0/3/2` after `all` (`3D2EEF02`) | `5cbf0354b011f816d6ce6619ddbcc68ccb1fb55d` |
| Sun 1.2 `.code` after `all` (`BFFA956C`) | `245757dfd9690698728c25a6f53f7e4b46b0fd5c` |
| Moon 1.2 `.code` after `all` (`1B98B88D`) | `5a91c42aff1b1a69610df13b8161de6263188f03` |
| 1.2 `Battle.cro` after `all` (`D50DC232`) | `a2a204ea23060eeae271a865de7e394691479978` |
| 1.2 `a/0/3/2` after `all` (`6023034E`) | `01d923c6ff51503aa42d5cd65dc093f7e5e2da10` |

## Static checks (the only checks run)

* Every handler a patch touches (Prankster, Parental Bond, Gale Wings, Soul Dew, Hyperspace Fury, Dark Void, both
  type getters, the forme-revert call sites) has the same instruction stream as its US/UM counterpart apart from
  call targets, and each call target maps to one Sun/Moon function only.
* No `Battle.cro` relocation or import patch points into the Soul Dew cave or anywhere between `.text` and
  `.rodata`, so the loader does not overwrite the handler.
* The plate and memory tables the Arceus handlers read are byte-identical to US/UM's.
* The four images pass a full hash-tree check (ExeFS, every RomFS level, every file compared with the patched
  file it should hold).

## Skipped, and what is not covered

* **No feature of the US/UM set is skipped.** Everything above is static: no boot, no battle, no Tree
  registration has been run on it.
* **No 1.2 image.** The baked `.3ds` files are 1.0; for 1.2 only the patch files exist (the update is a separate
  title, not a ROM).
* `Battle.cro`'s header hash table is not updated (the US/UM patches leave it too).
* Each Tree record ends in a 16-bit value at +0x4A4 that differs per record; it is not a CRC-16 of the record
  (six common variants compared) and is left as is, as the US/UM patch leaves its own.
* Three US/UM forme-revert sites have no Sun/Moon counterpart (see `formepersist`), so there is nothing to patch
  there.
