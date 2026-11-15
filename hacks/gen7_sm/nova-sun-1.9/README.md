**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**
# Pokemon Nova Sun Expanded (v1.9) [Sun]

The Sun/Moon Un-Nerf set (`../../../gen7_sm/`, read its README for what each feature does and where) applied to
Nova Sun Expanded (v1.9). Nothing here has been booted or played; the patches were only re-applied to
the plain hack and compared byte for byte with the built ROM.

Nova Sun Expanded (v1.9) is a LayeredFS hack of Pokemon Sun (title `0004000000164800`). It ships no `.code`, no `Battle.cro` and
no `a/1/3/7`, so those three get exactly the vanilla Sun 1.0 patches (the hack runs the clean files). It does ship its
own `a/0/3/2` (English text): the patch here is made against the hack's own file and changes only the two
description lines (Soul Dew, bank 35 line 225; Gale Wings, bank 97 line 177), which hold the vanilla wording in the
hack; the hack's other text is untouched. The ROM was built like the other `[Un-Nerf Complete]` 3DS hack files:
`hack-unnerf/tools/ctr/build3ds.py` on the clean Sun dump with the hack's files plus the patched `.code`,
`Battle.cro`, `a/1/3/7` and `a/0/3/2`. Re-applying `mods/` to the plain image with `complete_unnerf.py` gives the same
bytes.

## What changes

All ten features of `gen7_sm`: Battle Tree ban list and Species/Item Clause (`a/1/3/7`), Prankster on Dark types,
Gale Wings at any HP (+ description), Parental Bond 0.5x, Soul Dew +50 % Sp. Atk/Sp. Def (+ description), Hyperspace
Fury and Dark Void for any user (`Battle.cro`), forme persistence, Poke Pelago item formes and Arceus/Silvally form
typing (`.code`, + the `Battle.cro` Protean list).

## Files

```
NovaSun_UnNerf.xdelta   plain hack .3ds -> Un-Nerf hack .3ds (xdelta3)
mods/exefs/code.ips|.bps             .code    (identical to gen7_sm/azahar-mods/0004000000164800/v1.0/all)
mods/romfs_ext/Battle.cro.ips|.bps   Battle.cro (identical to the vanilla 1.0 patch)
mods/romfs_ext/a/1/3/7.ips|.bps      Battle Tree rules (identical to the vanilla 1.0 patch)
mods/romfs_ext/a/0/3/2.ips|.bps      the hack's own a/0/3/2, two description lines
```

## Input

| input | SHA-1 |
|---|---|
| `Pokemon Nova Sun Expanded (v1.9) [Sun].3ds` (the plain rebuilt image: clean Sun 1.0 + the hack's LayeredFS files, `hackbuild-SHA1SUMS.txt`) | `e422dc5594ec343b47e700807150f3ecf8811dd9` |
| its `.code` (= clean Sun 1.0) | `271b0c77432c339fdd6086475646a9a00586214e` |
| its `Battle.cro` (= clean 1.0) | `233500e8a8a06e785dbc5f9dd80d376d3343eeff` |
| its `a/1/3/7` (= clean 1.0) | `6d0192e7a403fab3658592e9f90cf486ce444730` |
| its `a/0/3/2` (the hack's, 2342236 bytes, CRC-32 `735B8FF9`; same file in Nova Sun and Umbra Moon) | `12574c057a8fd3be0096dad19a1984665e8e22af` |

## How to apply

* `xdelta3 -d -s "Pokemon Nova Sun Expanded (v1.9) [Sun].3ds" NovaSun_UnNerf.xdelta "Pokemon Nova Sun Expanded (v1.9) [Sun] [Un-Nerf UNTESTED].3ds"`
* or `python3 ../../../gen7_sm/complete_unnerf.py "Pokemon Nova Sun Expanded (v1.9) [Sun].3ds" --mods mods --out "Pokemon Nova Sun Expanded (v1.9) [Sun] [Un-Nerf UNTESTED].3ds"`
* LayeredFS (the hack's own install route): put `mods/exefs/code.*` and `mods/romfs_ext/Battle.cro.*` into Azahar's
  `load/mods/0004000000164800/` the same way (1.0 game only; with the 1.2 update installed use
  `../../../gen7_sm/azahar-mods/0004000000164800/v1.2/all/exefs/code.*` and `romfs_ext/Battle.cro.*` instead). The nested
  `a/1/3/7` and `a/0/3/2` patches are not applied by Azahar's `romfs_ext`; run
  `python3 ../../../gen7_sm/sm_unnerf.py --mode all ...` on the hack's `romfs/a/0/3/2` and on an `a/1/3/7` taken
  from your clean dump, and put both into the hack's `romfs` folder.

## Output

| output | SHA-1 |
|---|---|
| `Pokemon Nova Sun Expanded (v1.9) [Sun] [Un-Nerf UNTESTED].3ds` | `f68d604085bb5d16041fcd00bc6210b655a98fc6` |
| `.code` | `2f90f102bbe5ad13333a1861460dadf59e2ec5ac` |
| `Battle.cro` | `862f1415036ea13b3d7002aca8b4ab65571c8de6` |
| `a/1/3/7` | `640fc5ddcf931dfc98bacdc2f427e0ff7b7784f2` |
| `a/0/3/2` (CRC-32 `41D1F1AF`) | `c23062ca09d7b13ddfcae9d6a0f72f24e24e4b6e` |

## Skipped

Nothing of the set is skipped. The image is the 1.0 build (as the plain image is); with the 1.2 update installed
the update's `.code`, `Battle.cro` and `a/0/3/2` replace the patched ones (see above for the 1.2 patches). Not
booted, not played.
