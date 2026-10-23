# Supernova Sun Expanded 1.0.5 (Ultra Sun hack) + Un-Nerf
**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**

The complete Ultra Sun / Ultra Moon Un-Nerf set (`gen7_usum/azahar-mods/00040000001B5000/all/`) carried over to
Pokemon Supernova Sun Expanded v1.0.5. Nothing here has been booted or played: the patch set was only re-applied to
the plain hack image and the result compared byte for byte with the built ROM.

The hack ships no executable and no `Battle.cro` or `a/1/4/1`, so in a built image of the hack those three files are
the clean Ultra Sun 1.0 files, byte for byte. Their patches here are the compendium's own `all/` files, copied
unchanged. The hack does ship its own English text archive `a/0/3/2`, so the compendium's text patch (keyed to the
clean archive) cannot be used; `romfs_ext/a/0/3/2.bps` is new: it puts the compendium's two rewritten descriptions
into the hack's own text and keeps every other line the hack wrote.

## What changes

| # | feature | file | what it does |
|---|---|---|---|
| 1 | Battle Tree ban list and clauses | `a/1/4/1` (171 bytes) | zeroes the banned-species records and clears the Species Clause / Item Clause flags of all 14 facility rule records |
| 2 | Prankster | `Battle.cro` `0x24B14` | `beq` -> `b`: Prankster moves work on Dark types again |
| 3 | Gale Wings | `Battle.cro` `0xDA514` + text | full-HP condition removed; description (bank 102, line 177) back to the Gen 6 wording |
| 4 | Parental Bond | `Battle.cro` `0x24EAC` | second hit 0.25x -> 0.5x |
| 5 | Soul Dew | `Battle.cro` `0xBBA10`, handler at `0xFC980` + text | Gen 3-6 effect (+50 % Sp. Atk and Sp. Def for Latios/Latias); description (bank 39, line 225) rewritten |
| 6 | Forme persistence | `.code` (84 bytes) | the out-of-battle `ChangeFormNo` reverts and the Hoopa reset calls are removed, so altered formes survive save/load |
| 7 | Poke Resort item formes | `.code` `0x1E05A0` | the Resort no longer resets item-derived formes |
| 8 | Arceus / Silvally form typing | `.code` cave `0x4B99F0` and hooks + `Battle.cro` `0x102670` | type follows the stored form (no Plate or Memory needed); Protean re-types Arceus/Silvally |
| 9 | Hyperspace Fury | `Battle.cro` `0xC8267` | any Pokemon can use it, not only Hoopa-Unbound |
| 10 | Dark Void | `Battle.cro` `0xC45C3` | any Pokemon can use it, not only Darkrai |

Offsets are file offsets (`.code`: virtual address minus `0x100000`). The feature-by-feature breakdown and the
derivation of each patch are in `gen7_usum/azahar-mods/README.md`.

The text merge changes exactly two lines and no other bank:

| bank / line | the hack's text | after |
|---|---|---|
| 39 / 225 (Soul Dew) | "... It raises the power of Psychic- and Dragon-type moves." (the hack's own Gen 7 text) | "... It raises its Sp. Atk and Sp. Def stats." |
| 102 / 177 (Gale Wings) | "Gives priority to Flying-type moves when the Pokémon’s HP is full." | "Gives priority to the Pokémon’s Flying-type moves." |

All other lines, including the hack's own item descriptions in bank 39 (lines 26, 28, 29, 31, 32, 565 and 958 among
them), keep the hack's wording. The rebuilt archive is 60 bytes shorter than the hack's; it is padded with zeros to
the hack's size so the file keeps its place in the RomFS, as the compendium's own text patch pads the clean archive.

## Files

| patch | patches | input size / CRC32 / SHA-1 | result CRC32 / SHA-1 |
|---|---|---|---|
| `azahar-mods/00040000001B5000/all/exefs/code.bps` (and `.ips`) | ExeFS `.code` | 5914624 / `6DBB9B4D` / `d05602171c1854f4c6d33c28589689f52f56c4cd` | `CD9DBFEE` / `8f8d5d98498cf3645a23838c758a9924e6e70ef6` |
| `azahar-mods/00040000001B5000/all/romfs_ext/Battle.cro.bps` (and `.ips`) | RomFS `Battle.cro` | 1294336 / `1025191A` / `53daf6ae893542e4133d433f02f4425b84360422` | `3928F52B` / `a90ae1a46d040cb25470a1c050cac21c0dfc7582` |
| `azahar-mods/00040000001B5000/all/romfs_ext/a/1/4/1.bps` (and `.ips`) | RomFS `a/1/4/1` | 30372 / `75E48390` / `45e7cc6096cb4abb12e86eb6c7ecd0e3d7652527` | `07491715` / `55740e1723abd0f035b35c109e20e2d6453cd8f9` |
| `azahar-mods/00040000001B5000/all/romfs_ext/a/0/3/2.bps` (new) | RomFS `a/0/3/2` (the hack's) | 2793492 / `BBCAABEA` / `5a706d069f83d357f00c8dbabe9882378af40312` | `3BC45915` / `1e51f15f85b0f8631159f1e69486c89b573b669a` |

The `.code`, `Battle.cro` and `a/1/4/1` patches are identical to the compendium's `gen7_usum` `all/` files (same
SHA-256). `complete_unnerf.py` is the compendium's `gen7_usum/complete_unnerf.py`, unchanged.

## Input

`Pokemon Supernova Sun Expanded (v1.0.5) [Ultra Sun].3ds`, a decrypted `.3ds` of the hack, 3713167360 bytes,
**SHA-1 `cd1739524ec42ac746c79b4fe638b69c19832213`**, title id `00040000001B5000`. It was built from the clean
`Pokemon Ultra Sun (USA) (En,Ja,Fr,De,Es,It,Zh,Ko).3ds` (SHA-1 `51957bc32b3e96bda2197496bf0b462dc3da0130`) with the
hack's v1.0.5 files: 31 RomFS files replaced (its `luma` RomFS files, its 1.0 `Shop.cro`, and its `title_us.moflex`
copied over the other language videos as the hack's own batch files do), the clean `.code` stored flat, the hack's
banner and icon.

Any other decrypted `.3ds` (or `.cia` with a flat `.code`) of Supernova Sun v1.0.5 on Ultra Sun 1.0 works as long as
its four files match the input column above: every BPS checks the size and CRC32 of the file it patches and nothing
is written if one does not match. The output SHA-1 below is only for the input above.

## How to apply

Python 3, standard library only. The input is only read; the result is a new file.

```
python3 complete_unnerf.py "Pokemon Supernova Sun Expanded (v1.0.5) [Ultra Sun].3ds" --verify
python3 complete_unnerf.py "Pokemon Supernova Sun Expanded (v1.0.5) [Ultra Sun].3ds" --out "Pokemon Supernova Sun Expanded (v1.0.5) [Ultra Sun] [Un-Nerf UNTESTED].3ds"
```

(`py -3` instead of `python3` on Windows.) The script reads the patch set from `azahar-mods/00040000001B5000/all/`
next to it, prefers the `.bps` of each file, patches the four files in place (every patch keeps the file size) and
recomputes the ExeFS hash and the RomFS hash tree. Without `--out` it writes `<input name> [Un-Nerf Complete].3ds`
next to the input.

## Output

`Pokemon Supernova Sun Expanded (v1.0.5) [Ultra Sun] [Un-Nerf UNTESTED].3ds`, 3713167360 bytes,
**SHA-1 `5bd41b9cdeeafdf57e26fbd0c2cda3439c7ab094`**.

Static checks on it (no emulator was run): every ExeFS, exheader and RomFS hash verifies (887040 level-3 blocks, 0
bad); besides the `.code`, only `Battle.cro`, `a/1/4/1` and `a/0/3/2` of the 747 RomFS files differ from the
plain hack; every changed byte lies in those four files or in the hash fields that cover them; Arceus (493) and
Silvally (773) personal rows in the hack equal clean Ultra Sun (Normal type, 18 forms), which feature 8 relies on.

## Use

- **Run it as v1.0, without the 1.2 update.** The update replaces `.code`, `Battle.cro` and `a/0/3/2` with files these
  patches were not made for. This set is v1.0 only; a 1.2 set does not exist yet.
- **Azahar `load/mods`:** copying `azahar-mods/00040000001B5000/all/` into `load/mods/00040000001B5000/` over the
  plain hack gives only the `.code` and `Battle.cro` features: Azahar skips `romfs_ext` files in sub-folders, so the
  Battle Tree rules (`a/1/4/1`) and the two descriptions (`a/0/3/2`) are not applied that way. Use the baked image
  for the whole set, and do not add the mods on top of the baked image.
- Nothing in the set touches the save format; a save of the plain hack is expected to load in the Un-Nerf image
  (not tried).

## Skipped

None of the set's ten features is skipped.
