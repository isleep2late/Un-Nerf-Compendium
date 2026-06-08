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
| 3 | Emerald | Battle Frontier **ban list, level cap, Species Clause, Item Clause**; **Soul Dew un-nerf** (works in-Frontier); **ANY ability on ANY Pokémon** | `.ips` patches + patched PKHeX |
| 4 | Platinum | Battle Frontier **ban list** (banned legendaries can enter) | Python patcher (`.nds`) |
| 4 | Platinum | Battle Frontier **form restrictions** (Origin Giratina, Rotom/Shaymin forms stop reverting) | Python patcher (`.nds`) + `.ups` |
| 5 | Black 2 / White 2 | Battle Subway + PWT **ban list, Soul Dew ban, item clause, species clause, 3-Pokémon cap [please note the 3-P cap may break the game]** | Python patcher (`.nds`) |
| 6 | Omega Ruby / Alpha Sapphire | Battle Maison **ALL entry restrictions** — ban list, **Species Clause**, **Item Clause**, **team-size limit**, **and the 510 EV-total cap** | Python patcher (`.cia` or `.3ds`) |
| 7 | Ultra Sun / Ultra Moon | Battle Tree **ban list + Species Clause + Item Clause**; **Prankster**, **Gale Wings**, **Parental Bond**, **Soul Dew** un-nerfs; matching in-game text | Python patcher (`.cia`) |
| Switch | Brilliant Diamond / Shining Pearl | Battle Tower **ban list, now including species/item clause lifted** | exefs mod |
| 8 (Switch) | Sword / Shield | **Crowned Zacian/Zamazenta + Eternamax persistence** AND **Battle Tower species + item clause removal** — both **CONFIRMED WORKING** (Sword & Shield) | LayeredFS `.pchtxt` (`gen8_swsh/`) |


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
**All restrictions in one tool — `oras_no_restrictions.py` (recommended):**
```
python3 gen6_oras/oras_no_restrictions.py "Pokemon Alpha Sapphire.3ds"
python3 gen6_oras/oras_no_restrictions.py game.cia --verify   # dry-run, report current state
```
Removes **every** Battle Maison entry restriction in a single pass, for **both** OR and AS, on a
decrypted **`.cia` or `.3ds`**:

- **Ban list** (legendaries, Soul Dew / item bans),
- **Species Clause** (no duplicate species/formes),
- **Item Clause** (no duplicate held items),
- **Team-size limit** and the other entry rules,
- **510 EV-total cap** (maxed-EV mons become eligible).

It zeroes the Maison rule-config records in the RomFS rule GARC (file `"0"`) — which clears the
rule-flags word at GARC offset `0x26` (`0xFFFF`→0) that gates the clauses/limits — **and** raises the
EV-cap literal(s) in the executable (ExeFS `.code`: eligibility cap `0x1E9734`, plus the online
Battle-Spot cap, picked by Title ID — AS `0x4474B8`, OR `0x4474C0`). All the hashes a loader/installer
checks are recomputed (RomFS IVFC + NCCH RomFS/ExeFS superblocks, ExeFS `.code`, and the CIA TMD). The
`.3ds` output loads via *File → Load File* with **no install and no re-encryption**, and your save
carries over (saves are keyed by Title ID). Technical details: `PATCHES.md` (Gen 6 section).
(Windows: `apply_oras_no_restrictions.bat`.)

**Granular tools** (if you only want one thing): `oras_evcap.py` does just the 510 EV cap;
`oras_nobanlist.py` does just the RomFS rule-config (ban list + clauses + team-size).

## Gen 5 — Black 2 / White 2  (`gen5_black2/`)
```
python3 gen5_black2/black2_nobanlist.py "Pokemon Black 2.nds"
```
Zeroes the restriction fields in the rule NARC (`a/1/0/6`) — banlist, Soul Dew, item clause,
species clause, and the 3-Pokémon cap, for both Battle Subway and PWT. (Windows: `apply_black2_nobanlist.bat`.)
*Credits to MeroMero for figuring this out! Please see references.*

## Gen 4 — Platinum  (`gen4_platinum/`)
```
python3 gen4_platinum/platinum_nobanlist.py "Pokemon Platinum.nds"   # banned species can enter
python3 gen4_platinum/platinum_forms.py     "Pokemon Platinum.nds"   # alt forms stop reverting
```
The first zeroes the banned-species list in `arm9` so banned legendaries can enter the Frontier.
The second removes the Battle Frontier **form restriction** so Origin Giratina, the Rotom
appliance forms and Sky Shaymin no longer revert to their base form on entry. Both edits are
length-preserving (no NARC repack) so they **compose in any order**; run one or both. The form
patcher needs `ndspy` (`pip install ndspy`).

The form patch automates SmolJoltik's DSPRE discovery (PP topic 67882): in each Frontier facility
script the game runs a per-Pokemon "form check" — `CompareVarValue 32780 255` / `JumpIf EQUAL` to a
revert handler. Rather than deleting the run (which would shift every following script jump target),
the patcher rewrites the compare constant `255 → 0xFFFF`; the sentinel is a byte so the equality —
and the revert jump — can never fire. It edits exactly the 13 checks across scripts 367 (Battle
Tower), 377 (Hall), 378 (Castle) and 379 (Arcade), leaving the 14 identical-looking checks elsewhere
untouched. A CRC-verified **`platinum_forms.ups`** binary patch (clean Platinum USA, src CRC32
`1D8A5220`) is included for users without Python. The original manual DSPRE walkthrough is preserved
in `gen4_platinum/PLATINUM_FORMS.md`. *Shoutout to SmolJoltik for discovering this — see References.*

## Gen 3 — Emerald  (`gen3_emerald/`)
Three **IPS** patches for a clean Emerald `.gba` (apply with Lunar IPS / Flips / MultiPatch),
plus a patched **PKHeX** for setting abilities. Saves are cross-compatible.

- `patches/1_Emerald_FrontierUnlock_SoulDew.ips` — removes **all** Battle Frontier entry
  restrictions (ban list, **level cap, Species Clause, Item Clause**) and **un-nerfs Soul Dew**
  inside the Frontier. Covers Singles/Doubles/Multi/Link + all facilities.
- `patches/2_Emerald_AnyAbility.ips` — **any ability on any Pokémon**. Gen 3 has no per-mon
  ability ID (just a 1-bit slot), so this adds one in the unused PK3 byte `0x1E` and patches
  every engine site that sets a battler's ability to read it.
- `patches/3_Emerald_Full_Hackmons_v3.ips` — **everything** (1 + 2 + a flash-save fix). The full build.

Set abilities with PKHaX.exe located in the Release page (Gen-3 Ability dropdown now lists all 78 — pick
one, save; needs the .NET 10 Desktop Runtime). Source changes in `PKHeX/PKHeX_CHANGES.md`. Full
details + offsets in `gen3_emerald/README.md`; forum write-ups in `gen3_emerald/forum_posts/`.


## Switch — Brilliant Diamond / Shining Pearl  (`bdsp/`)
BDSP's Battle Tower ban list now including species and item clause removal in addition to legendary Pokemon legality.
This builds an exefs mod folder you drop into your emulator's mod directory (Ryujinx
`mods\contents\<TitleID>\`, Yuzu `load\<TitleID>\`; BD `0100000011D90000`, SP `010018E011D92000`).
See `bdsp/README.md`. 

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
gen4_platinum/  platinum_nobanlist.py, platinum_forms.py (+.ups), apply_*.bat, PLATINUM_FORMS.md
gen5_black2/    black2_nobanlist.py, apply_*.bat
gen6_oras/      oras_no_restrictions.py (all-in-one), oras_evcap.py, oras_nobanlist.py, apply_*.bat
gen67_formepersist/  formepersist.py, apply_*.bat, README.md, FORUM_POST_*.md   # forme persistence (Gen 6/7)
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
https://projectpokemon.org/home/forums/topic/67882-dspre-removing-form-restrictions-in-pok%C3%A9mon-platinums-battle-frontier-solved/

Please do NOT hestitate to contact me on Discord ( @tirelessgolem ), or YouTube ( https://www.youtube.com/@isleep2late ) if you need help with anything.

-IS2L
