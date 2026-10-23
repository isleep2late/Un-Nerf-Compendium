# Xtreme-Yang X (Pokemon X hack): the X/Y Un-Nerf set
**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**

Status: **untested**. Nobody has booted the patched hack, entered the Battle Maison or fought a battle with it yet.
Keep a backup of your save before you try it.

The same changes as `../../../gen6_xy/` (read its README for what each one does): the Battle Maison ban list and the
Soul Dew ban cleared (`a/1/8/9`, as in the OR/AS set), the 510 EV cap in the Maison eligibility check lifted to 1530,
an edited Ability stays on Xerneas, Arceus takes its form's type without a Plate, and the out-of-battle form reverts
(Mega, Shaymin-Sky, Furfrou, Keldeo-Resolute) are disabled.

Xtreme-Yang X ships a full 1.0 `.code` that differs from the clean Pokemon X 1.0 `.code` in 8 `.rodata` bytes
(vaddr `0x53C89E`-`0x53C98C`), none of them at or next to a patched site; every patched site holds the clean bytes.
It does not change `a/1/8/9` (the file in the hack image is the clean one). So the patches are the base game's,
applied to the hack.

## Files

```
0004000000055D00/exefs/code.bps          the hack's .code -> patched (checks the hack's .code CRC32)
0004000000055D00/exefs/code.ips          the same records (identical to gen6_xy/azahar-mods/0004000000055D00/v1.0/all/exefs/code.ips)
0004000000055D00/romfs_ext/a/1/8/9.bps   Battle Maison rule archive (identical to the gen6_xy one)
0004000000055D00/romfs_ext/a/1/8/9.ips
```

## Input (exact)

| file | size | SHA-1 |
|---|---|---|
| `Pokemon Xtreme-Yang X (no version in pack) [X].3ds` (the plain hack image: the clean Pokemon X 1.0 dump with the hack's ExeFS and RomFS files built in, `.code` stored flat) | 1843056640 | `71ec578c76184e17e4a8a5ed818801f5a869bb69` |
| its ExeFS `.code` | 5156864 | `340feb41728e8ebd7a495eff816084236e449444` (CRC32 `5C3E94D1`) |
| its RomFS `a/1/8/9` | 23776 | `8126e28efa78ad0e76d6fd6d9627cf63be2f6c36` (CRC32 `51C67A3F`) |

## How to apply

```
python3 ../../../gen6_xy/complete_unnerf.py "Pokemon Xtreme-Yang X (no version in pack) [X].3ds" --mods 0004000000055D00 --out "Pokemon Xtreme-Yang X (no version in pack) [X] [Un-Nerf UNTESTED].3ds"
```

Run it from this folder (or give `--mods` the full path of the `0004000000055D00` folder here). The tool checks both BPS
patches against the hack image first and writes nothing if anything differs. The hack image stores `.code` flat, so
the result has the same size and layout as the input; only `.code`, `a/1/8/9` and their hashes change
(839 bytes).

| output | SHA-1 |
|---|---|
| `Pokemon Xtreme-Yang X (no version in pack) [X] [Un-Nerf UNTESTED].3ds` | `99b711808ab92bfe1a614aa574dbf84302c3e687` |
| its `.code` | `01f44d5bbd02c72eebc7ea38a499fa712047f50a` (CRC32 `CE57ADB0`) |
| its `a/1/8/9` | `bf08110d3e5bdf367a31d9dda7e63ccd660c5d55` (CRC32 `ACF2AA78`) |

For Azahar `load/mods` on a LayeredFS install of the hack, `0004000000055D00/exefs/code.ips` gives the `.code` features; the
nested `a/1/8/9` patch is skipped by Azahar (see the gen6_xy README), so use the baked image for the Maison rules.

## Skipped, and why

As in `gen6_xy/`: the Battle Spot EV validator has no counterpart in X/Y; Hyperspace Fury, Hoopa and Primal reverts do
not exist in X/Y; Protean on Arceus is not solved. Nothing was tested in game or in an emulator.
