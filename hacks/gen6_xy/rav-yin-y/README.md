# Rav-Yin Y (Pokemon Y hack): the X/Y Un-Nerf set
**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**

Status: **untested**. Nobody has booted the patched hack, entered the Battle Maison or fought a battle with it yet.
Keep a backup of your save before you try it.

The same changes as `../../../gen6_xy/` (read its README for what each one does): the Battle Maison ban list and the
Soul Dew ban cleared (`a/1/8/9`, as in the OR/AS set), the 510 EV cap in the Maison eligibility check lifted to 1530,
an edited Ability stays on Xerneas, Arceus takes its form's type without a Plate, and the out-of-battle form reverts
(Mega, Shaymin-Sky, Furfrou, Keldeo-Resolute) are disabled.

Rav-Yin Y ships a full 1.0 `.code` that differs from the clean Pokemon Y 1.0 `.code` in 9 `.rodata` bytes
(vaddr `0x53C89E`-`0x53C98C`), none of them at or next to a patched site; every patched site holds the clean bytes.
It does not change `a/1/8/9` (the file in the hack image is the clean one). So the patches are the base game's,
applied to the hack.

## Files

```
0004000000055E00/exefs/code.bps          the hack's .code -> patched (checks the hack's .code CRC32)
0004000000055E00/exefs/code.ips          the same records (identical to gen6_xy/azahar-mods/0004000000055E00/v1.0/all/exefs/code.ips)
0004000000055E00/romfs_ext/a/1/8/9.bps   Battle Maison rule archive (identical to the gen6_xy one)
0004000000055E00/romfs_ext/a/1/8/9.ips
```

## Input (exact)

| file | size | SHA-1 |
|---|---|---|
| `Pokemon Rav-Yin Y (no version in pack) [Y].3ds` (the plain hack image: the clean Pokemon Y 1.0 dump with the hack's ExeFS and RomFS files built in, `.code` stored flat) | 1843052544 | `a715faf2cc5ef17e61afb79312d04b3b8d366b56` |
| its ExeFS `.code` | 5156864 | `4e75654dc0c5faa782972a8d1417c51390d1543c` (CRC32 `B8A75DC5`) |
| its RomFS `a/1/8/9` | 23776 | `8126e28efa78ad0e76d6fd6d9627cf63be2f6c36` (CRC32 `51C67A3F`) |

## How to apply

```
python3 ../../../gen6_xy/complete_unnerf.py "Pokemon Rav-Yin Y (no version in pack) [Y].3ds" --mods 0004000000055E00 --out "Pokemon Rav-Yin Y (no version in pack) [Y] [Un-Nerf UNTESTED].3ds"
```

Run it from this folder (or give `--mods` the full path of the `0004000000055E00` folder here). The tool checks both BPS
patches against the hack image first and writes nothing if anything differs. The hack image stores `.code` flat, so
the result has the same size and layout as the input; only `.code`, `a/1/8/9` and their hashes change
(808 bytes).

| output | SHA-1 |
|---|---|
| `Pokemon Rav-Yin Y (no version in pack) [Y] [Un-Nerf UNTESTED].3ds` | `1442fdeb1b38a3bed48592e9cd0217378cf12b1b` |
| its `.code` | `e8095cc0997b16a39e174a116c3c16320f440915` (CRC32 `2ACE64A4`) |
| its `a/1/8/9` | `bf08110d3e5bdf367a31d9dda7e63ccd660c5d55` (CRC32 `ACF2AA78`) |

For Azahar `load/mods` on a LayeredFS install of the hack, `0004000000055E00/exefs/code.ips` gives the `.code` features; the
nested `a/1/8/9` patch is skipped by Azahar (see the gen6_xy README), so use the baked image for the Maison rules.

## Skipped, and why

As in `gen6_xy/`: the Battle Spot EV validator has no counterpart in X/Y; Hyperspace Fury, Hoopa and Primal reverts do
not exist in X/Y; Protean on Arceus is not solved. Nothing was tested in game or in an emulator.
