# Omega Ruby / Alpha Sapphire — Azahar load-time mods

The Python tools in `gen6_oras/` and `gen67_arceus_typefix/code_patch/` rewrite a decrypted dump in place.
This folder holds the same edits as **load-time patches** that Azahar (and Citra/Lime3DS forks with the
same `load/mods` support) applies when the untouched game starts, so nothing has to be written to the dump.

Nothing here was transcribed from the scripts' tables. Each script was run on a fresh copy of the owner's
dump, the file it touched (ExeFS `.code`, or one RomFS file) was extracted before and after, and the
difference became the patch. Every patch was then applied back to the original file with Floating IPS and
with a second, independent applier and hashed against the script's own output. For `all/`, the per-feature
diffs were merged (no two features write different bytes at the same offset) and the merged file was
checked byte-for-byte against a copy that had **every** script run on it in sequence.

## Layout

```
000400000011C400/   Omega Ruby        000400000011C500/   Alpha Sapphire
  all/                                  (same shape)
    exefs/code.ips        every .code feature merged
    exefs/code.bps        the same, as BPS
    romfs_ext/a/1/7/0.ips Battle Maison rule archive (ban list + clauses + team size)
    romfs_ext/a/1/7/0.bps
  features/               one patch per feature per file, for selective use
    <feature>.<file>.ips / .bps
```

`<file>` is `code` for the ExeFS executable and the RomFS path with `/` replaced by `-` otherwise
(`a-1-7-0` = `a/1/7/0`).

## Installing the full set

Copy the **contents** of `<TID>/all/` into Azahar's `load/mods/<TID>/`, so that the files land at

```
<Azahar user dir>/load/mods/000400000011C400/exefs/code.ips
<Azahar user dir>/load/mods/000400000011C400/romfs_ext/a/1/7/0.ips
```

(`000400000011C500` for Alpha Sapphire). The user dir is `~/.local/share/azahar` on Linux,
`%APPDATA%\Azahar` on Windows, the app's own user directory on Android. Keep either the `.ips` **or** the
`.bps` for a given file — see the rules below.

## What Azahar does with these files

Taken from the loader source (`core/file_sys/ncch_container.cpp`, `layered_fs.cpp`, `patch.cpp`):

* **Exactly one executable patch is applied.** The first file that exists wins, in this order:
  `exefs/code.ips`, `exefs/code.bps`, `code.ips`, `code.bps` (then Luma's `luma/titles/<TID>/code.ips`).
  The patch is applied to the **decompressed** `.code`, so one patch serves a dump whose ExeFS stores
  `.code` LZSS-compressed (the No-Intro cartridge dumps) and one that stores it flat.
* **One patch per RomFS file.** `romfs_ext/<path>.ips` or `romfs_ext/<path>.bps` patches the RomFS file
  `<path>`; a file without an original in the RomFS is skipped with a warning.
* **BPS checks CRC32s; IPS does not.** A BPS whose source CRC32 does not match the file is refused — for
  the executable that makes the game fail to load rather than run mis-patched; for a RomFS file the
  original is used. An IPS is applied blindly, whatever the file underneath is. Azahar also refuses a BPS
  that carries a metadata block; none of these do.
* **Mods also apply to an installed update.** Azahar strips the update bit (`0004000E...` → `00040000...`)
  when it looks for the mods folder, so with the 1.4 update installed these patches are applied to the
  **update's** `.code`. That is not the executable they were derived from (see the table below): the BPS
  will be refused, the IPS will corrupt it. Use these only on the base game, or remove the update first.

## Base these were derived from (v1.0 executable)

The owner's dumps (`Pokemon Omega Ruby - pokemonerdotcom.3ds`, `Pokemon Alpha Sapphira - pokemonerdotcom.3ds`)
store `.code` flat. The No-Intro cartridge dumps (`Pokemon Omega Ruby (USA) (En,Ja,Fr,De,Es,It,Ko) (Rev 2).3ds`,
same for Alpha Sapphire) store it LZSS-compressed (3039008 / 3039088 bytes); decompressed with the same
routine Azahar uses, they are **byte-identical** to the flat ones, and the RomFS file below is identical
in all four dumps. One set therefore serves both dump families.

| title | file | size | CRC32 | SHA-1 |
|---|---|---|---|---|
| OR `000400000011C400` | ExeFS `.code` (decompressed) | 5439488 | `4D9BBCE3` | `f0a007360be0e6ea096bb0518cef52c93cf4e165` |
| AS `000400000011C500` | ExeFS `.code` (decompressed) | 5439488 | `72B62B7F` | `8819d013d63a184d01954eed2f30eea68b9582c5` |
| OR and AS | RomFS `a/1/7/0` | 33136 | `A1623931` | `ca155dc5122a796bed745f5e838a281a6c711a28` |

**Not the same executable** (do not use these patches on it): the 1.4 update's `.code`, decompressed, is
5439488 bytes with CRC32 `B0AB0BFE` (OR, `0004000E0011C400`) / `DDC6E452` (AS, `0004000E0011C500`).
The update does not replace `a/1/7/0`.

Results after `all/` is applied: OR `.code` CRC32 `5B3445C5`, AS `.code` CRC32 `B721A5D5`, `a/1/7/0`
CRC32 `0A6B996A` (both).

## Features

Offsets below are file offsets inside `.code` (virtual address minus `0x100000`).

| feature | file | bytes | what it does (from the script that produced it) |
|---|---|---|---|
| `nobanlist` | `a/1/7/0` | 402 | `oras_nobanlist.py`: zeroes the banned-Pokémon records in the Battle Maison rule GARC. Its zero-runs are the same as `no_restrictions`', so this also clears the rule-flags word at `0x26` (Species Clause, Item Clause, team size). Byte-identical to `no_restrictions.a-1-7-0`. |
| `no_restrictions` | `a/1/7/0` + `code` | 402 + 4 | `oras_no_restrictions.py`: the `a/1/7/0` edit above, plus the 510 EV-total cap raised to 1530 in the facility eligibility check (`0xE9734`) and the Battle Spot validator (AS `0x3474B8`, OR `0x3474C0`). Its `.code` half is byte-identical to `evcap.code`. |
| `evcap` | `code` | 4 | `oras_evcap.py`: the two EV-cap literals only. |
| `xerneas` | `code` | 34 | `oras_xerneas_ability.py`: makes the two personal-table `SetTokusei` writes conditional on species != 716 (`ChangeFormNo` at `0x2B4334`, `ChangeMonsNoForm` at `0x1F594`), so an edited Ability stays on Xerneas. |
| `formepersist` | `code` | 107 (OR) / 108 (AS) | `formepersist.py --full`: NOPs the out-of-battle `ChangeFormNo(base)` revert calls (Mega/Primal loop, the per-title table, and the Hoopa destructive-reset blocks) so altered formes survive save/load. |
| `arceus` | `code` | 66 | `s2_oras_build.py`: Arceus form-driven typing. Code cave at `0x479604` (OR) / `0x4795FC` (AS) reached from both type getters (`0x2B3B98`, OR `0x3D3254` / AS `0x3D324C`), the Multitype gates NOPed (`0x2B3B6C`, OR `0x3D3228` / AS `0x3D3220`), and the plate-less form reset NOPed at `0x2B3EB8`. |

`all/exefs/code.*` = `no_restrictions`/`evcap` + `xerneas` + `formepersist` + `arceus` (211 bytes OR,
212 bytes AS; no overlapping offsets). `all/romfs_ext/a/1/7/0.*` = `nobanlist`/`no_restrictions`.

The `.code` features touch disjoint offsets, so any subset of `features/*.code.ips` can be merged into one
`exefs/code.ips` by concatenating their records. `a/1/7/0` has one edit, shared by two feature names.

These correspond to the owner's finished build
`OmegaRuby_UNNERF_HOOPApersist_ARCEUSformtype_XerneasAbility.3ds` (and the Alpha Sapphire one) minus nothing:
the same scripts, the same dump. Protean on Arceus remains unsolved in ORAS (see the repo's CHANGES notes);
no patch here claims otherwise.
