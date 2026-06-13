# The Un-Nerf Compendium

A one-stop collection of patches that restore Pokemon games to how a lot of us wish they still played:
un-nerfed abilities and items, lifted Battle Frontier / Subway / Maison / Tree / Tower ban lists and
clauses, form-driven typing, and permanent alternate formes, across **Generations 3 through 8 plus
BDSP** (with bonus Gen 1 and Gen 3 save-editing support in PKHaX).

> You supply your own **legally-dumped** games. **Nothing copyrighted is distributed here, so do NOT
> ask for ROMs, CIAs, or saves** - only small patch tools and patch files.

---

## 2026 update (v4)

- **Arceus form-driven typing is final** across gens 4-7: hold the Plate -> Multitype type as in
  vanilla; hold no Plate -> the PKHeX form's type (Ghost form reads Ghost, etc.); the form persists.
  Length-neutral code patch for ORAS and USUM (USUM also covers **Silvally**), a source patch for
  Platinum, and a personal-data fix for Black 2 / White 2.
- **Protean on Arceus and Silvally now works in USUM** (gen 7): the type-lock species list
  `{Arceus, Silvally}` in the battle module is cleared, so Protean re-types them. **In ORAS (gen 6)
  this is NOT possible** - Arceus's type is re-derived from its form every move inside the move
  pipeline; it could not be removed without breaking move processing. Castform and Kecleon Protean
  work in ORAS (no species block on them). See `FEATURES_AND_DISABLE_GUIDE.md` and the project notes.
- **Hoopa-Unbound persistence is correctly fixed.** Earlier versions claimed it persisted via the
  single `ChangeFormNo` NOP; it did not. Hoopa reverts via a destructive multi-call reset block; the
  tool now auto-detects those (AS/OR: 11, US/UM: 6).
- **Emerald is feature-complete**: any-ability (PK3 0x1E), fully playable **Deoxys forms**, 6-Pokemon
  Battle Tower, Soul Dew un-nerf, full Frontier unban.
- **Platinum** adds 6-Pokemon Tower, permanent Giratina-Origin / Sky Shaymin, Arceus typing in
  doubles, and **AbilityLock** (hacked abilities survive forme changes).
- **PKHaX** now also allows the **Gen-1 RBY sprite/type "desync"** combinations (the mismatched
  species-sprite/type pairings the stock editor blocks).
- **Repo reorg:** the USUM tooling (the `.bat` runners + `unnerf.py` + `gametext.py`) now lives in
  `gen7_usum/`; the old `gen67_formepersist/` is folded into `gen6_oras/` and `gen7_usum/` as
  `formepersist.py` in each.
- **Known-not-done:** Protean-on-Arceus in ORAS (above); 6-Pokemon facility teams in BW2 and USUM
  (raising the party-size limit above legal crashes on team confirm - the team buffer is fixed-size,
  and only the decomp games could rebuild it); permanent Giratina-O / Shaymin-Sky in BW2 (binary-RE).

---

## What is included

| Gen | Game(s) | What it removes / restores | Delivered as | Folder |
|-----|---------|----------------------------|--------------|--------|
| 1 | Red/Blue/Yellow | RBY sprite/type "desync" combos in the save editor | PKHaX | `PKHaX/` |
| 3 | Emerald | Frontier ban list + level cap + Species/Item Clause; Soul Dew un-nerf; any-ability; Deoxys forms; 6-Pokemon Tower | IPS + source patch + PKHaX | `gen3_emerald/`, `PKHaX/` |
| 4 | Platinum | Frontier ban list + Species/Item Clause; permanent Giratina-O/Rotom/Sky-Shaymin; Soul Dew un-nerf; Arceus form-typing (incl. doubles); 6-Pokemon Tower; AbilityLock | xdelta + source patches | `gen4_platinum/` |
| 5 | Black 2 / White 2 | Subway + Institute + PWT ban list + Species/Item Clause (legal party size kept, no PWT freeze); Arceus form-typing | Python + xdelta | `gen5_bw2/`, `gen45_nds_arceus_typefix/` |
| 6 | Omega Ruby / Alpha Sapphire | Maison ban list + clauses + team-size + 510 EV cap; forme persistence (full Hoopa); Arceus form-typing | Python (cia/3ds) | `gen6_oras/`, `gen67_arceus_typefix/` |
| 7 | Ultra Sun / Ultra Moon | Tree ban list + clauses; Prankster/Gale Wings/Parental Bond/Soul Dew un-nerfs (+ matching text); forme persistence; Arceus+Silvally form-typing; **Protean-Arceus/Silvally** | Python (cia) | `gen7_usum/`, `gen67_arceus_typefix/` |
| 8 | Sword / Shield | Tower Species/Item Clause; Crowned + Eternamax persistence; Dynamax unlock | LayeredFS pchtxt + Python | `gen8_swsh/` |
| Switch | Brilliant Diamond / Shining Pearl | Tower ban list + Species/Item Clause | exefs ips/pchtxt + Python | `bdsp/` |

PKHaX (a patched PKHeX save editor) lives in `PKHaX/`; the built `PKHeX.exe` is included and is what
you attach as a GitHub Release.

To keep some features and drop others, read **`FEATURES_AND_DISABLE_GUIDE.md`** - it lists, per game,
exactly which patch / tagged block / byte to omit for each feature.

---

## General requirements

- **Python 3** (3.8+), standard library only - nothing to install. Windows users can double-click the
  `.bat` runners (the USUM ones are now in `gen7_usum/`).
- Your own **dumped** game file. 3DS must be **decrypted** (e.g. a GodMode9 dump). For xdelta patches
  (gen 4/5) use Delta Patcher or `xdelta3 -d -s "clean.nds" patch.xdelta out.nds`. For IPS use Lunar
  IPS / Flips. For Switch use LayeredFS.
- Every tool checks the original bytes before writing and outputs a new file, so it cannot silently
  corrupt the wrong input. **Test in a fresh battle after a clean boot, not from an old save state.**

---

## PKHaX

A patched PKHeX. On top of stock it adds: Gen-1 RBY sprite/type desync combos; a Gen-3 any-ability
dropdown (all 78, written to PK3 0x1E for the patched Emerald); Deoxys form box icons; and loosened
legality where the un-nerf ROMs make otherwise-"illegal" mons valid. Source is in `PKHaX/`; rebuild on
Windows with `dotnet publish -c Release -r win-x64`. The committed `PKHeX.exe` is the current build.

---

## Credits

Builds on community findings, including SmolJoltik and ABZB (Platinum Frontier forme/banlist),
MeroMero (gen-5 Subway/PWT regulation offsets), and the broader projectpokemon.org and hackmons.com
research threads.
