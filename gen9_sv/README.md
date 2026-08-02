# Gen 9 — Scarlet / Violet update de-nerf

Restores the base stats that Scarlet/Violet **updates** took away. These are the only
Pokémon in the entire series whose base stats were ever nerfed by a patch, and this
folder puts them back the way they originally shipped.

> Fact check (this is often misremembered as a Sword/Shield thing): **no Sword/Shield
> update ever changed a base stat.** The nerfs below all live in Scarlet/Violet.
> Zacian, Zamazenta, and Cresselia were never nerfed by an update either — their stats
> dropped in the **Gen 8 → Gen 9 transition** (SV launch data), which is why the
> restore for those three is optional.

## What gets restored

### Treasures of Ruin — nerfed by the day-one 1.0.1 update

The v1.0.0 cartridge stats are the originals; only players who never updated ever saw them.

| Pokémon | v1.0.0 (restored) | 1.0.1+ (nerfed) | Change undone |
|---|---|---|---|
| Wo-Chien | 85/90/100/100/135/70 | 85/85/100/95/135/70 | +5 Atk, +5 SpA |
| Chien-Pao | 80/130/80/90/65/135 | 80/120/80/90/65/135 | +10 Atk |
| Ting-Lu | 165/110/130/55/80/45 | 155/110/125/60/80/45 | +10 HP, +5 Def (−5 SpA) |
| Chi-Yu | 55/80/80/145/120/100 | 55/80/80/135/120/100 | +10 SpA |

### Optional (`--gen8-legends`): Gen 8 → Gen 9 transition nerfs

| Pokémon | Gen 8 (restored) | Gen 9 (nerfed) |
|---|---|---|
| Zacian | Atk 130 | Atk 120 |
| Zacian-Crowned | Atk 170 | Atk 150 |
| Zamazenta | Atk 130 | Atk 120 |
| Zamazenta-Crowned | Atk 130 / Def 145 / SpD 145 | Atk 120 / Def 140 / SpD 140 |
| Cresselia | Def 120 / SpD 130 | Def 110 / SpD 120 |

## Usage

The patcher edits the extracted personal data FlatBuffer
(`romfs:/avalon/data/personal_array.bin`, stored inside `data.trpfs`). Extract it from
your own legally dumped copy with pkNX (or any trpfs-aware tool), patch, and repack as
a LayeredFS mod:

```
python3 sv_denerf_personal.py personal_array.bin
python3 sv_denerf_personal.py personal_array.bin --gen8-legends
```

Safety: a timestamped `.bak` is written first, the file is validated against the SV
schema before any write (including a Pikachu control entry), every target must match
its known pre-patch stats exactly, size never changes, and re-running is a no-op.
`--selftest` exercises the whole parse/patch/verify cycle on synthetic data with no
game files involved.

## Neutralizing Gas (not patchable here — read this)

Protosynthesis and Quark Drive originally ignored Neutralizing Gas; SV **version 3.0.0**
(The Indigo Disk patch, Dec 2023) made Neutralizing Gas suppress them. Orichalcum Pulse
and Hadron Engine had their protective ability flags dropped in the 2.0.x window.
That behavior lives in the game **code** (exefs), not in the data files this folder
patches, and no SV code reverse engineering exists in this project — so there is no
ROM patch for it here. Your options:

- Play on an SV version **below 3.0.0** and the original behavior is simply still there.
- On our Showdown server, the No Nerfs format already restores it: Hadron Engine,
  Orichalcum Pulse, Quark Drive, and Protosynthesis are immune to Neutralizing Gas.

## Files

- `sv_denerf_personal.py` — the patcher (self-validating, in-place, idempotent).
