# The Un-Nerf Compendium

*A growing, one-stop collection of patches that restore Pokémon games to how a lot of us wish
they still played — un-nerfed abilities and items, and lifted facility ban lists / clauses —
across **Generations 3 through 7 and BDSP**.*

> You supply your own **legally-dumped** games. **Nothing copyrighted is distributed here, so do NOT ask me to provide any** —
> only small patch tools and patch files.

---

## What's included

| Gen | Game(s) | What it removes / restores | How it's delivered |
|-----|---------|----------------------------|--------------------|
| 3 | Emerald | Battle Frontier/Tower **ban list** | `.ips` patch |
| 4 | Platinum | Battle Frontier **ban list** (banned legendaries can enter) | Python patcher (`.nds`) |
| 4 | Platinum | Battle Frontier **form restrictions** (Origin Giratina, Rotom/Shaymin forms) | **manual** DSPRE steps (see `gen4_platinum/PLATINUM_FORMS.md`) |
| 5 | Black 2 / White 2 | Battle Subway + PWT **ban list, Soul Dew ban, item clause, species clause, 3-Pokémon cap [please note the 3-P cap may break the game]** | Python patcher (`.nds`) |
| 6 | Omega Ruby / Alpha Sapphire | Battle Maison **ban list** | Python patcher (`.cia` or `.3ds`) |
| 7 | Ultra Sun / Ultra Moon | Battle Tree **ban list + Species Clause + Item Clause**; **Prankster**, **Gale Wings**, **Parental Bond**, **Soul Dew** un-nerfs; matching in-game text | Python patcher (`.cia`) |
| Switch | Brilliant Diamond / Shining Pearl | Battle Tower **ban list** | LayeredFS mod builder + installer |

**Still in progress** (not yet done): the **510-EV limit** removal (all gens) and the **BDSP
species/item clause**. Those are flagged where relevant and remain on the to-do list.

---

## General requirements

- **Python 3** (3.8+). Everything here is pure standard library — nothing to `pip install`.
  Runs on **Windows, macOS, and Linux**. (Windows users can also just double-click the `.bat`
  files, which find Python for you.)
- Your own **dumped, decrypted** game file:
  - **GBA/NDS** (`.gba`, `.nds`) — a standard cart dump.
  - **3DS** (`.cia`, `.3ds`) — must be **decrypted** (e.g. a GodMode9 dump). An encrypted CIA
    won't parse.
  - **BDSP** — your dumped RomFS file `global-metadata.dat` (+ a Switch emulator/CFW with
    LayeredFS mods).
- Each tool checks the original bytes before writing and **outputs a new file** (your original
  is untouched), so it can't silently corrupt the wrong file.
- **Test in a fresh battle after a clean boot**, never a loaded save state.

---

## Gen 7 — Ultra Sun / Ultra Moon  (`unnerf.py`, `gametext.py`, `*.bat`)

The most complete tool. One patcher, both games (auto-detected). Modes:

- **`nbl`** — "No Restrictions": unbans the Battle Tree's forbidden Pokémon **and** removes the
  **Species Clause** (no duplicate species) and **Item Clause** (no duplicate held items).
- **`prankster`** — removes Gen-7 Prankster's "no effect on Dark types" rule.
- **`galewings`** — Gale Wings priority at any HP (not only at full HP); also fixes the ability text.
- **`parentalbond`** — restores the Gen-6 0.5× second hit (not the Gen-7 0.25×).
- **`souldew`** — restores Gen-6 Soul Dew (+50% Sp. Atk **and** Sp. Def for Latios/Latias incl.
  Mega forms) via a code injection; also fixes the item text.
- **`all`** — everything above, plus the matching in-game ability/item descriptions.

Windows: double-click a numbered `.bat` (or drag your `.cia` onto it). Any OS:
```
python3 unnerf.py --cia "Pokemon Ultra Moon.cia" --mode all
python3 unnerf.py --cia game.cia --mode souldew --out custom.cia
python3 unnerf.py --cia game.cia --mode all --verify    # dry-run, no writes
```
`gametext.py` must stay next to `unnerf.py` (used for the description edits). Technical details of
every Gen-7 patch are in `PATCHES.md`.

## Gen 6 — Omega Ruby / Alpha Sapphire  (`gen6_oras/`)
```
python3 gen6_oras/oras_nobanlist.py "Pokemon Alpha Sapphire.3ds"
```
Works on a decrypted **`.cia` or `.3ds`**, for **both** OR and AS. Zeroes the Battle Maison ban
records in the rule GARC and rebuilds the RomFS IVFC + NCCH hashes. (Windows: `apply_oras_nobanlist.bat`.)

## Gen 5 — Black 2 / White 2  (`gen5_black2/`)
```
python3 gen5_black2/black2_nobanlist.py "Pokemon Black 2.nds"
```
Zeroes the restriction fields in the rule NARC (`a/1/0/6`) — banlist, Soul Dew, item clause,
species clause, and the 3-Pokémon cap, for both Battle Subway and PWT. (Windows: `apply_black2_nobanlist.bat`.)
*Credits to MeroMero for figuring this out! Please see references.*

## Gen 4 — Platinum  (`gen4_platinum/`)
```
python3 gen4_platinum/platinum_nobanlist.py "Pokemon Platinum.nds"
```
Zeroes the banned-species list in `arm9` so banned legendaries can enter the Frontier.
The **form-restriction** removal is a separate, manual DSPRE procedure — see
`gen4_platinum/PLATINUM_FORMS.md` (it edits compiled field-script bytecode, which isn't safe to
auto-patch blindly). *Shoutout to SmolJoltik for discovering how to do this - please see References.*

## Gen 3 — Emerald  (`gen3_emerald/`)
Apply `gen3_emerald/Emerald_NoBanList.ips` to a clean Emerald `.gba` with any IPS patcher
(Lunar IPS / Flips / MultiPatch). See `gen3_emerald/README.md`.

## Switch — Brilliant Diamond / Shining Pearl  (`bdsp/`)
BDSP's Battle Tower ban list is the same Gen-7 ban bit-string, stored in `global-metadata.dat`.
```
python3 bdsp/bdsp_nobanlist.py global-metadata.dat
```
This builds a LayeredFS mod folder you drop into your emulator's mod directory (Ryujinx
`mods\contents\<TitleID>\`, Yuzu `load\<TitleID>\`; BD `0100000011D90000`, SP `010018E011D92000`).
See `bdsp/README.md`. **Does not** remove the BDSP species/item clause yet.

---

## Repo layout
```
unnerf.py, gametext.py, 1..6_*.bat, PATCHES.md   # Gen 7 (USUM)
gen3_emerald/   Emerald_NoBanList.ips, README.md
gen4_platinum/  platinum_nobanlist.py, apply_*.bat, PLATINUM_FORMS.md
gen5_black2/    black2_nobanlist.py, apply_*.bat
gen6_oras/      oras_nobanlist.py, apply_*.bat
bdsp/           bdsp_nobanlist.py, apply_*.bat, README.md
```

## Safety & scope
- **Use your own legally-dumped games.** No game code, ROMs, CIAs, or save data are distributed.
- For **personal, single-player / casual** use (emulators, personal CFW). Not for official online ladders.
- Keep a backup of your original. NDS/Switch patches were verified by reproducing known-working
  edits byte-for-byte where a reference was available, but always test before relying on them.

## Credits / References
Reverse-engineering and tooling by the author, with parts assisted by an AI coding assistant and
all findings verified by hand against known-working data where possible. Community research that
made the multi-gen patches possible: MeroMero, isleep2late, ABZB, SmolJoltik and others on the
Project Pokémon forums; the BDSP method builds on the Imposter's Ordeal tool by Nifyr.

https://hackmons.com/forum/category/rom-hacking

Gen 6/7: https://projectpokemon.org/home/forums/topic/39258-solved-some-progress-i-mightve-made-in-removing-battle-maisonbattle-tree-restrictions-banlist/

BDSP: https://projectpokemon.org/home/forums/topic/60041-bdsp-removing-battle-tower-banlist/
https://github.com/Nifyr/Imposters-Ordeal/releases/tag/v2.14.1

Gen 5: https://projectpokemon.org/home/forums/topic/39257-gen-5-remove-all-battle-subway-restrictions-including-soul-dew-item-clause-species-clause-amp-more-than-3-pokemon/
https://projectpokemon.org/home/forums/topic/36108-gen-v-edit-the-banlist-of-battle-subway-and-pwt/

Gen 4: https://projectpokemon.org/home/forums/topic/67882-dspre-removing-form-restrictions-in-pok%C3%A9mon-platinums-battle-frontier-solved/

Please do NOT hestitate to contact me on Discord ( @tirelessgolem ), or YouTube ( https://www.youtube.com/@isleep2late ) if you need help with anything.

-IS2L
