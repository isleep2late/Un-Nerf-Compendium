# The Un-Nerf Compendium

*A one-stop collection of patches that restore Pokemon games to how a lot of us wish they
still played: un-nerfed abilities and items, and lifted Battle Frontier / Subway / Maison /
Tree / Tower ban lists and clauses, across **Generations 3 through 8 plus BDSP** (with bonus
Gen 1 + Gen 3 save-editing in PKHaX).*

> You supply your own **legally-dumped** games. **Nothing copyrighted is distributed here, so do
> NOT ask for ROMs, CIAs, or saves** - only small patch tools and patch files.

---

## What's included

| Gen | Game(s) | What it removes / restores | Delivered as | Folder |
|-----|---------|----------------------------|--------------|--------|
| 3 | Emerald | Battle Frontier **ban list, level cap, Species Clause, Item Clause**; **Soul Dew un-nerf** (works in-Frontier); **any ability on any Pokemon** (with PKHaX) | `.ips` patches + PKHaX | `gen3_emerald/`, `PKHaX/` |
| 4 | Platinum | Battle Frontier **ban list, Species Clause, Item Clause**; **alternate formes kept** (Giratina-Origin, Rotom forms, Sky Shaymin) in and out of battle; **Soul Dew un-nerf** | `.xdelta` | `gen4_platinum/` |
| 5 | Black 2 / White 2 | Battle Subway + Battle Institute + PWT **ban list, Species Clause, Item Clause** - party-size limit kept, no PWT freeze | `.xdelta` patches + Python script | `gen5_bw2/` |
| 6 | Omega Ruby / Alpha Sapphire | Battle Maison **ban list, Species Clause, Item Clause, team-size limit, and the 510 EV-total cap** | Python patcher (`.cia`/`.3ds`) | `gen6_oras/` |
| 7 | Ultra Sun / Ultra Moon | Battle Tree **ban list + Species Clause + Item Clause**; **Prankster / Gale Wings / Parental Bond / Soul Dew** un-nerfs; matching in-game text | Python patcher (`.cia`) | root (`unnerf.py`) |
| 6/7 | ORAS + USUM | **Alternate forme persistence** helper | Python patcher | `gen67_formepersist/` |
| 8 | Sword / Shield | **Crowned Zacian/Zamazenta + Eternamax persistence** and **Battle Tower Species + Item Clause removal** | LayeredFS pchtxt + Python | `gen8_swsh/` |
| Switch | Brilliant Diamond / Shining Pearl | Battle Tower **ban list + Species Clause + Item Clause** | exefs mod (`.ips`/pchtxt) + Python | `bdsp/` |

PKHaX (a patched PKHeX save editor) lives in `PKHaX/`; see its section below.

---

## General requirements

- **Python 3** (3.8+). Every script here is pure standard library - nothing to `pip install`.
  Runs on Windows, macOS, and Linux. Windows users can also double-click the `.bat` files.
- Your own **dumped (and for 3DS, decrypted) game file**:
  - **GBA / NDS** (`.gba`, `.nds`) - a standard cart dump.
  - **3DS** (`.cia`, `.3ds`) - must be **decrypted** (e.g. a GodMode9 dump); an encrypted CIA won't parse.
  - **BDSP** - your dumped RomFS `global-metadata.dat` plus a Switch emulator/CFW with LayeredFS.
- For the **`.xdelta`** patches (Gen 4 and Gen 5), apply with **Delta Patcher** (GUI) or `xdelta3`:
  ```
  xdelta3 -d -s "clean game.nds" patch.xdelta "patched.nds"
  ```
- Each tool checks the original bytes before writing and **outputs a new file** (your original is
  left untouched), so it can't silently corrupt the wrong file.
- **Test in a fresh battle after a clean boot.** Don't judge a patch from an old save state.

---

## Gen 3 - Emerald  (`gen3_emerald/`, `PKHaX/`)

Three **IPS** patches for a clean Emerald `.gba` (apply with Lunar IPS / Flips / MultiPatch).
Saves are cross-compatible with stock Emerald.

- **`gen3_emerald/1_Emerald_FrontierUnlock_SoulDew.ips`** - removes **all** Battle Frontier entry
  restrictions (ban list, **level cap, Species Clause, Item Clause**) and **un-nerfs Soul Dew**
  inside the Frontier. Covers Singles / Doubles / Multi / Link and every facility.
- **`gen3_emerald/2_Emerald_AnyAbility.ips`** - **any ability on any Pokemon**. Gen 3 has no
  per-mon ability ID (just a 1-bit slot), so this stores one in the unused PK3 byte `0x1E` and
  patches every engine site that reads a battler's ability to honor it.
- **`gen3_emerald/3_Emerald_Full_Hackmons_v3.ips`** - **everything** (patch 1 + patch 2 + a
  flash-save fix). This is the recommended all-in-one build.

To actually set an arbitrary ability, use **PKHaX** (the Gen-3 Ability dropdown lists all 78 - pick
one and save; the patched ROM reads byte `0x1E`). Details and offsets in `gen3_emerald/README.md`.

## Gen 4 - Platinum  (`gen4_platinum/`)

One `.xdelta` does the whole job. Apply
**`gen4_platinum/Platinum_unbanned_species_item_clause_formefix.xdelta`** to a clean
**Pokemon Platinum (USA)** `.nds` (the standard 128 MiB No-Intro dump, serial CPUE). It removes,
in a single patch:

- **Ban list** - every banned legendary / mythical can enter the Battle Frontier (zeroes the
  banned-species list in `arm9`, per ABZB's find).
- **Species Clause and Item Clause** - run duplicate species and duplicate held items at every
  facility. This is fixed at two gates: the team-selection check (`arm9`) **and** the Tower's
  registered-team re-check (`arm9 0x0204A410`), which is the one that actually rejected teams.
  Arcade reuses the Tower path; Castle has its own selection check; Hall is one Pokemon; Factory
  is rentals - so all facilities are covered.
- **Alternate formes kept** - Giratina-Origin, the Rotom appliance forms, and Sky Shaymin no
  longer revert to base form on facility entry. Done at the shared script-command **handler**
  (overlay 5, `0x021F6DC2`) rather than by deleting script segments, so it's a single byte and
  covers all four facilities at once (credit SmolJoltik, PP topic 67882, for finding the cause).
  A second edit (overlay 16, `0x02259FBD`) stops the **battle engine** from reverting
  Giratina-Origin to Altered after Castle/Arcade strip its held item.
- **Soul Dew un-nerf** - the Frontier secretly disables Soul Dew's +50% Sp.Atk / Sp.Def. Two
  one-byte edits in the battle overlay (`0x0225A5F0`, `0x0225A61E`) delete that Frontier guard so
  the boost applies again. (Griseous Orb's boost has no Frontier guard and works once the forme
  is kept.)

The Lv.50 cap and everything else are untouched. The patch is verified to reproduce the final
ROM byte-for-byte with a stock `xdelta3`.

## Gen 5 - Black 2 / White 2  (`gen5_bw2/`)

Removes the **ban list, Species Clause, and Item Clause** from **Battle Subway, Battle Institute,
and the PWT** - on both games - while **keeping the legal party-size limit** (so the game never
crashes on team confirm) and **keeping the PWT cup index** (so the bracket never black-screens).

- **`gen5_bw2/bw2_nobanlist.py`** - the patcher. Works on a clean Black 2 *or* White 2 `.nds`
  (pure stdlib, length-preserving, in-place edit of regulation NARC `a/1/0/6`):
  ```
  python bw2_nobanlist.py "Pokemon Black 2.nds"          # -> *_norestrictions.nds
  ```
- **`gen5_bw2/Black2_NoRestrictions.xdelta`** / **`gen5_bw2/White2_NoRestrictions.xdelta`** -
  prebuilt patches for users who'd rather not run Python; apply each to its clean dump.

How it works: each restriction is a fixed 188-byte regulation record in `a/1/0/6`. The patcher
zeroes the species ban bitfield (`0x1C-0x77`) and sets the two clause flags at **`0x08` (species)
and `0x09` (item)** to `01` (= duplicates allowed), while deliberately leaving the party-size
field (`0x02/0x03`) and the cup index (`0xBA`) alone. This is what fixes the two classic problems
with the old "zero everything from C0 to the end" method: that wiped `0xBA` (freezing PWT) and
the party cap (crashing on more than 3 Pokemon). Building on MeroMero's banlist research; see
References.

## Gen 6 - Omega Ruby / Alpha Sapphire  (`gen6_oras/`)

**All-in-one (recommended): `gen6_oras/oras_no_restrictions.py`** removes every Battle Maison
entry restriction in one pass on a decrypted `.cia` or `.3ds`, for both OR and AS:
ban list, Species Clause, Item Clause, team-size limit, and the **510 EV-total cap**.

```
python3 gen6_oras/oras_no_restrictions.py "Pokemon Alpha Sapphire.3ds"
python3 gen6_oras/oras_no_restrictions.py game.cia --verify   # dry-run, report current state
```

It zeroes the Maison rule-config in the RomFS rule GARC and raises the EV-cap literal(s) in the
ExeFS, then recomputes every hash a loader/installer checks (RomFS IVFC, NCCH superblocks, ExeFS
`.code`, CIA TMD). A `.3ds` output loads via *File -> Load File* with no install or re-encryption,
and your save carries over. **Granular tools:** `oras_evcap.py` (just the EV cap) and
`oras_nobanlist.py` (just the ban list + clauses + team-size). Details in `PATCHES.md`.

## Gen 7 - Ultra Sun / Ultra Moon  (`unnerf.py`, `gametext.py`, numbered `.bat` files)

One patcher, both games (auto-detected). Modes:

- **`nbl`** - unbans the Battle Tree's forbidden Pokemon and removes the **Species Clause** and **Item Clause**.
- **`prankster`** - removes Gen-7 Prankster's "no effect on Dark types" rule.
- **`galewings`** - Gale Wings priority at any HP (not only at full HP); fixes the ability text.
- **`parentalbond`** - restores the Gen-6 0.5x second hit (instead of Gen-7's 0.25x).
- **`souldew`** - restores Gen-6 Soul Dew (+50% Sp.Atk **and** Sp.Def for Latios/Latias incl. Megas); fixes the item text.
- **`all`** - everything above plus the matching in-game ability/item descriptions.

```
python3 unnerf.py --cia "Pokemon Ultra Moon.cia" --mode all
python3 unnerf.py --cia game.cia --mode all --verify    # dry-run, no writes
```

Windows: double-click a numbered `.bat` (or drag your `.cia` onto it). `gametext.py` must stay
next to `unnerf.py`. Full technical details in `PATCHES.md`.

## Gen 6/7 - Forme persistence  (`gen67_formepersist/`)

`gen67_formepersist/formepersist.py` (+ `apply_formepersist.bat`) - keeps alternate formes from
reverting where ORAS / USUM would normally reset them. See `gen67_formepersist/README.md`.

## Gen 8 - Sword / Shield  (`gen8_swsh/`)

`gen8_swsh/swsh_dynamax_unlock.py` builds LayeredFS mods (the per-title folders
`01008DB008C2C000` and `0100ABF008968000`) that give **Crowned Zacian/Zamazenta and Eternamax
persistence** and remove the **Battle Tower Species + Item Clause** - confirmed on both Sword and
Shield. See `gen8_swsh/README.md`.

## Switch - Brilliant Diamond / Shining Pearl  (`bdsp/`)

`bdsp/bdsp_nobanlist.py` plus the prebuilt mods in `bdsp/BrilliantDiamond/` and
`bdsp/ShiningPearl/`. Removes the Battle Tower **ban list, Species Clause, and Item Clause**.
The ban list is data (zeroed in `global-metadata.dat`); the two clauses are code (eight
`bl -> mov w0,#0` edits in the team-select methods, shipped as a per-build `.pchtxt`/`.ips` to
drop in `<TitleID>/exefs/`). Full instructions in `bdsp/README.md`.

---

## PKHaX - patched PKHeX save editor  (`PKHaX/`)

A full copy of [PKHeX](https://github.com/kwsch/PKHeX) rebuilt as **PKHaX**, with two hackmons
features on top of stock PKHeX. Every changed line is tagged with a `// PKHaX` comment (see
`PLACEMENT.md` for the exact file list and where each goes in a fresh PKHeX tree). It's a
framework-dependent build and needs the **.NET 10 Desktop Runtime**.

- **Gen 1 (RBY): sprite desync + arbitrary typing** - lets the list/header species (the sprite)
  differ from the data species, and lets Type 1 / Type 2 be set freely (e.g. a Mewtwo that shows
  the Gyarados sprite and is typed Water/Ghost). Credit: PikaSav.
- **Gen 3 (RSE/FRLG): any ability on any Pokemon** - stores an arbitrary ability in the unused PK3
  byte `0x1E`; pairs with the Emerald AnyAbility / Full ROM patch.

HaX (illegal-edit) mode turns on because the executable is named **`PKHaX.exe`** - do not rename
the DLLs. Build (Windows, .NET 10 SDK):
```
cd PKHaX
dotnet publish PKHeX.WinForms -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
```
then rename the output `PKHeX.exe` to `PKHaX.exe`. Write-ups: `PKHaX/PKHEX_PIKASAV_CHANGES.md`,
`PKHaX/PKHaX_README.md`, and `PLACEMENT.md`.

---

## Repo layout

```
unnerf.py, gametext.py, 1..6_*.bat, PATCHES.md      # Gen 7 (USUM)
PLACEMENT.md                                         # where each PKHaX file goes in a PKHeX tree
gen3_emerald/    1_..._SoulDew.ips, 2_..._AnyAbility.ips, 3_..._Full_Hackmons_v3.ips, README.md
gen4_platinum/   Platinum_unbanned_species_item_clause_formefix.xdelta
gen5_bw2/        bw2_nobanlist.py, Black2_NoRestrictions.xdelta, White2_NoRestrictions.xdelta
gen6_oras/       oras_no_restrictions.py, oras_evcap.py, oras_nobanlist.py, apply_*.bat
gen67_formepersist/  formepersist.py, apply_formepersist.bat, README.md
gen8_swsh/       swsh_dynamax_unlock.py, 01008DB008C2C000/, 0100ABF008968000/, README.md
bdsp/            bdsp_nobanlist.py, apply_bdsp_nobanlist.bat, BrilliantDiamond/, ShiningPearl/, README.md
PKHaX/           full PKHeX source rebuilt as PKHaX (Gen-1 sprite/typing + Gen-3 any-ability)
```

## Safety and scope

- **Use your own legally-dumped games.** No game code, ROMs, CIAs, or saves are distributed here.
- For **personal, single-player / casual** use (emulators, personal CFW). Not for official online ladders.
- Keep a backup of your original. NDS/Switch patches were verified by reproducing known-working
  edits byte-for-byte where a reference existed; still, test before relying on them.

## Credits and references

Community research that made these possible: **MeroMero, ABZB, SmolJoltik, isleep2late**, and
others on the Project Pokemon forums; **kwsch (Kurt)** for PKHeX; **PikaSav** for the Gen-1 layout;
**Nifyr** for Imposter's Ordeal (used in the BDSP work); **SciresM** and **Ritchie** for tooling.
Reverse-engineering and patch authoring by the author, parts assisted by an AI coding assistant,
with findings verified by hand against known-working data where possible.

- Hackmons forum: https://hackmons.com/forum/category/rom-hacking
- Gen 4 (Platinum formes): https://projectpokemon.org/home/forums/topic/67882-dspre-removing-form-restrictions-in-pok%C3%A9mon-platinums-battle-frontier-solved/
- Gen 5 (Subway/PWT banlist): https://projectpokemon.org/home/forums/topic/39257-gen-5-remove-all-battle-subway-restrictions-including-soul-dew-item-clause-species-clause-amp-more-than-3-pokemon/
- Gen 5 (banlist format): https://projectpokemon.org/home/forums/topic/36108-gen-v-edit-the-banlist-of-battle-subway-and-pwt/
- Gen 6/7 (Maison/Tree): https://projectpokemon.org/home/forums/topic/39258-solved-some-progress-i-mightve-made-in-removing-battle-maisonbattle-tree-restrictions-banlist/
- BDSP: https://projectpokemon.org/home/forums/topic/60041-bdsp-removing-battle-tower-banlist/
- Imposter's Ordeal: https://github.com/Nifyr/Imposters-Ordeal/releases/tag/v2.14.1

Need help? Discord **@tirelessgolem** or YouTube **https://www.youtube.com/@isleep2late**.

-IS2L
