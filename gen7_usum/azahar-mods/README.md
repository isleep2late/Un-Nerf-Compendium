# Ultra Sun / Ultra Moon — Azahar load-time mods

The Python tools in `gen7_usum/` and `gen67_arceus_typefix/code_patch/` rewrite a decrypted `.cia` in
place. This folder holds the same edits as **load-time patches** that Azahar (and Citra/Lime3DS forks with
the same `load/mods` support) applies when the untouched game starts, so nothing has to be written to the
dump.

Nothing here was transcribed from the scripts' tables. Each script (or `unnerf.py` mode) was run alone on
a fresh copy of the owner's `.cia`, the files it touched (ExeFS `.code`, RomFS `Battle.cro`, `a/1/4/1`,
`a/0/3/2`) were extracted before and after, and the difference became the patch. Every patch was then
applied back to the original file with Floating IPS and with a second, independent applier and hashed
against the script's own output. For `all/`, the per-feature diffs were merged (no two features write
different bytes at the same offset) and the merged file was checked byte-for-byte against a copy that had
**every** script run on it in sequence (`unnerf.py --mode all`, `formepersist.py --full`,
`resort_itemform_persist.py`, `s2_build_final.py` / `s2_build_port.py`).

## Layout

```
00040000001B5000/   Ultra Sun         00040000001B5100/   Ultra Moon
  all/                                  (same shape)
    exefs/code.ips            every .code feature merged
    exefs/code.bps            the same, as BPS
    romfs_ext/Battle.cro.ips  every Battle.cro feature merged (+ .bps)
    romfs_ext/a/1/4/1.ips     Battle Tree rule table: ban list + clauses (+ .bps)
    romfs_ext/a/0/3/2.ips     English text: the two rewritten descriptions (+ .bps)
  features/                   one patch per feature per file, for selective use
    <feature>.<file>.ips / .bps
```

`<file>` is `code` for the ExeFS executable and the RomFS path with `/` replaced by `-` otherwise
(`a-1-4-1` = `a/1/4/1`, `a-0-3-2` = `a/0/3/2`).

## Installing the full set

Copy the **contents** of `<TID>/all/` into Azahar's `load/mods/<TID>/`, so that the files land at

```
<Azahar user dir>/load/mods/00040000001B5100/exefs/code.ips
<Azahar user dir>/load/mods/00040000001B5100/romfs_ext/Battle.cro.ips
<Azahar user dir>/load/mods/00040000001B5100/romfs_ext/a/1/4/1.ips
<Azahar user dir>/load/mods/00040000001B5100/romfs_ext/a/0/3/2.ips
```

(`00040000001B5000` for Ultra Sun). The user dir is `~/.local/share/azahar` on Linux, `%APPDATA%\Azahar`
on Windows, the app's own user directory on Android. Keep either the `.ips` **or** the `.bps` for a given
file — see the rules below. For `a/0/3/2` prefer the `.bps`: the archive is re-laid-out by the text
tool, so the IPS is ~1.9 MB while the BPS is 6 KB and does the same thing.

## What Azahar does with these files

Taken from the loader source (`core/file_sys/ncch_container.cpp`, `layered_fs.cpp`, `patch.cpp`):

* **Exactly one executable patch is applied.** The first file that exists wins, in this order:
  `exefs/code.ips`, `exefs/code.bps`, `code.ips`, `code.bps` (then Luma's `luma/titles/<TID>/code.ips`).
  The patch is applied to the decompressed `.code` (US/UM store it flat anyway).
* **One patch per RomFS file.** `romfs_ext/<path>.ips` or `romfs_ext/<path>.bps` patches the RomFS file
  `<path>`; a file without an original in the RomFS is skipped with a warning.
* **BPS checks CRC32s; IPS does not.** A BPS whose source CRC32 does not match the file is refused — for
  the executable that makes the game fail to load rather than run mis-patched; for a RomFS file the
  original is used. An IPS is applied blindly, whatever the file underneath is. Azahar also refuses a BPS
  that carries a metadata block; none of these do.
* **Mods also apply to an installed update.** Azahar strips the update bit (`0004000E...` → `00040000...`)
  when it looks for the mods folder, so with the 1.2 update installed these patches are applied to the
  **update's** `.code` and to the update's `Battle.cro` and `a/0/3/2` (the update replaces both; it does
  not replace `a/1/4/1`). Those are not the files the patches were derived from (see the table below): the
  BPS files will be refused, the IPS files will corrupt them. Use these only on the base game, or remove
  the update first.

## Base these were derived from (v1.0)

The owner's `Pokemon Ultra Sun.cia` / `Pokemon Ultra Moon.cia` and the No-Intro cartridge dumps
(`Pokemon Ultra Sun (USA) (En,Ja,Fr,De,Es,It,Zh,Ko).3ds`, same for Ultra Moon) carry **byte-identical**
`.code`, `Battle.cro`, `a/1/4/1` and `a/0/3/2`, so one set serves both dump families. The three RomFS files
are also identical between Ultra Sun and Ultra Moon, and so are their patches; only `.code` differs per
title. (The "v1.0 No Outline" pokemoner `.3ds` files the finished builds were made from have a 4-byte
`.code` edit at `0x32E2B4` — the outline removal — and therefore different `.code` CRC32s, `9DF0C7F3` US /
`2AF29FAF` UM; the patches still apply to them because none of the records touch that word, but the BPS
will refuse them.)

| title | file | size | CRC32 | SHA-1 |
|---|---|---|---|---|
| US `00040000001B5000` | ExeFS `.code` | 5914624 | `6DBB9B4D` | `d05602171c1854f4c6d33c28589689f52f56c4cd` |
| UM `00040000001B5100` | ExeFS `.code` | 5914624 | `1C3F1290` | `46be5f8a8502a8b2e494f8be394ef275c0fc406c` |
| US and UM | RomFS `Battle.cro` | 1294336 | `1025191A` | `53daf6ae893542e4133d433f02f4425b84360422` |
| US and UM | RomFS `a/1/4/1` | 30372 | `75E48390` | `45e7cc6096cb4abb12e86eb6c7ecd0e3d7652527` |
| US and UM | RomFS `a/0/3/2` | 2793100 | `0FB62104` | `3ac3895fff45239ffed72a08d6f90ea47b4c16fb` |

**Not the same files** (do not use these patches on them): the 1.2 update (`0004000E001B5000` /
`0004000E001B5100`, "Pokemon Ultra Sun/Moon v1.2 Update for Citra.cia") ships `.code` of 5922816 bytes,
CRC32 `CE281B2B` (US) / `061A17D2` (UM); `Battle.cro` of 1298432 bytes, CRC32 `B0F91C1A`; `a/0/3/2` of
2793156 bytes, CRC32 `C2FEF549`. An app can tell a 1.2 executable from a 1.0 one by size alone.

Results after `all/` is applied: US `.code` CRC32 `CD9DBFEE`, UM `.code` CRC32 `C690FA6D`, `Battle.cro`
`6A62BDFB`, `a/1/4/1` `07491715`, `a/0/3/2` `5749BA1F`.

## Features

Offsets are file offsets inside the named file (`.code`: virtual address minus `0x100000`).

| feature | file | bytes | what it does (from the script that produced it) |
|---|---|---|---|
| `nbl` | `a/1/4/1` | 171 | `unnerf.py --mode nbl`: zeroes the Battle Tree banned-species records (11 × 2 runs, `0x772`…`0x64D0`) and clears the Species Clause / Item Clause flags of all 14 facility rule records (`0x6F2`…`0x6413`). |
| `prankster` | `Battle.cro` | 1 | `--mode prankster`: `0x24B14` `beq` → `b`, removing the "no effect on Dark types" rule. |
| `galewings` | `Battle.cro` + `a/0/3/2` | 4 + text | `--mode galewings`: `0xDA514` `beq` → `nop`, removing the full-HP condition; the Gale Wings description (bank 102, line 177) rewritten to the Gen 6 wording. |
| `parentalbond` | `Battle.cro` | 1 | `--mode parentalbond`: `0x24EAC` second-hit multiplier 0.25× → 0.5×. |
| `souldew` | `Battle.cro` + `a/0/3/2` | 157 + text | `--mode souldew`: a 184-byte handler in the `.text`/`.rodata` padding at `0xFC980` restoring +50 % Sp.Atk/Sp.Def for Latios/Latias, and the Soul Dew handler entry at `0xBBA10` repointed to it; the Soul Dew description (bank 39, line 225) rewritten. |
| `formepersist` | `code` | 84 | `formepersist.py --full`: NOPs the out-of-battle `ChangeFormNo(base)` revert calls (17 table sites + the Hoopa destructive-reset calls) so altered formes survive save/load. |
| `resort` | `code` | 4 | `resort_itemform_persist.py`: NOPs the Poké Resort's item-form `ChangeFormNo` call at `0x1E05A0` so item-derived formes are not re-normalised when a Pokémon is placed in the Resort. |
| `arceus` | `code` + `Battle.cro` | 82 + 4 | `s2_arceus_final.py` (UM) / `s2_arceus_port.py` (US): Arceus and Silvally form-driven typing — two handlers in the `.text` cave at `0x4B99F8` (UM) / `0x4B99F0` (US), hooked from both type getters, the Multitype/RKS gates removed, the plate-less form reset at `0x2236D8` changed to `mov r0,r4`; plus the Battle.cro Protean species lock list at `0x102670` cleared (`ed010503` → `ffffffff`) so Protean re-types Arceus/Silvally. |

`all/exefs/code.*` = `formepersist` + `resort` + `arceus` (170 bytes; no overlapping offsets).
`all/romfs_ext/Battle.cro.*` = `prankster` + `galewings` + `parentalbond` + `souldew` + `arceus`
(167 bytes; no overlapping offsets). `all/romfs_ext/a/1/4/1.*` = `nbl`.

**`a/0/3/2` is the one file whose feature patches are not additive.** `gametext.garc_repack` rebuilds the
whole archive, so `galewings.a-0-3-2` and `souldew.a-0-3-2` are two different whole-file repacks (they
disagree on most offsets, although each changes exactly one text line — checked by decoding all 127 banks
before and after). Use `galewings.a-0-3-2.*` **or** `souldew.a-0-3-2.*` **or** `all/romfs_ext/a/0/3/2.*`,
which is the `--mode all` repack carrying exactly those two lines and nothing else. Never stack two of them.
The text edit is cosmetic; the behaviour change lives in `Battle.cro`.

The `.code` features and the `Battle.cro` features touch disjoint offsets, so any subset of the
`features/*.code.ips` (or `*.Battle.cro.ips`) records can be concatenated into one file.

These correspond to the owner's finished builds
`UltraSun_UNNERF_ABILITYUNNERFS_HOOPApersist_ARCEUS_SILVALLY_formtype_PROTEAN.3ds` and the Ultra Moon one:
the same scripts, the same base game.
