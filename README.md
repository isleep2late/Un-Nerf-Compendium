# The Un-Nerf Compendium

A one-stop collection of patches that restore Pokemon games to how a lot of us wish they still played:
un-nerfed abilities and items, lifted Battle Frontier / Subway / Maison / Tree / Tower ban lists and
clauses, form-driven typing, and permanent alternate formes, across **Generations 3 through 9 plus
BDSP** (with bonus Gen 1/2 and Gen 3 save-editing support in PKHaX).

> You supply your own **legally-dumped** games. **Nothing copyrighted is distributed here, so do NOT
> ask for ROMs, CIAs, or saves** - only small patch tools and patch files.

Join our Discord: https://discord.gg/hackmons

---

## August 2026 update (v6)

- **Gen 8/9 has been "de-nerfed" with all the pre-update goodies.** New `gen9_sv/` folder: a
  self-validating patcher restores the **version 1.0.0 Treasures of Ruin stats** that the
  Scarlet/Violet day-one update nerfed (Wo-Chien 90 Atk/100 SpA, Chien-Pao 130 Atk, Ting-Lu
  165 HP/130 Def, Chi-Yu 145 SpA — the only base-stat patch nerfs in series history), with an
  optional `--gen8-legends` restore of the Gen 8 Zacian / Zamazenta / Cresselia stats that the
  Gen 9 transition lowered. Fact check inside: no Sword/Shield update ever changed a base stat —
  these all live in SV — and the Neutralizing Gas behavior change (SV 3.0.0) is code-side, so the
  folder documents your real options instead of pretending to patch it.
- **PKHaX user interface updated/enhanced** — see the PKHaX section: Gen 6/7 **battle team
  editing** (team-locked slots are now editable, plus a battle-team manager), and a new
  **pop-out Team + PC window** for every generation that shows your party, battle teams (where
  the save has them), and PC boxes side by side with free click-and-drag between all of them.

## July 2026 update (v5)

- **Pokéstar Studios "props" are now usable Pokémon in Black 2 / White 2.** The 33 internal Pokéstar
  opponent species (BW2 indices 652–684) can be placed in your party and no longer turn into **Bad Eggs**.
  The fix is a tiny, targeted arm9 patch (a species-aware skip in the per-mon checksum validator); the
  Bad Egg was proven to be a non-deterministic checksum-desync race, not a species gate — see
  **"Pokéstar Studios props"** below and `POKESTAR.md`. PKHaX gained full prop support: the 17
  user-facing props import/export under their exact Showdown names, appear in the B2W2 species
  dropdown/search, carry BST 100 (Smeargle keeps real stats), show the correct prop name on hover
  (not the colliding Gen-6 dex name), and preview their extracted sprite.

## June 2026 update (v4)

- **Arceus form-driven typing is final** across gens 4-7: hold the Plate -> Multitype type as in
  vanilla; hold no Plate -> the PKHeX form's type (Ghost form reads Ghost, etc.); the form persists.
  Length-neutral code patch for ORAS and USUM (USUM also covers **Silvally**), a source patch for
  Platinum, and a personal-data fix for Black 2 / White 2.
- **Protean on Arceus and Silvally now works in USUM** (gen 7): the type-lock species list
  `{Arceus, Silvally}` in the battle module is cleared, so Protean re-types them. **In ORAS (gen 6)
  this is NOT possible** - Arceus's type is re-derived from its form every move inside the move
  pipeline; it could not be removed without breaking move processing. Castform and Kecleon Protean
  work in ORAS (no species block on them). See **Features / how to disable each one** below and the project notes.
- **Hoopa-Unbound persistence is correctly fixed.** Earlier versions claimed it persisted via the
  single `ChangeFormNo` NOP; it did not. Hoopa reverts via a destructive multi-call reset block; the
  tool now auto-detects those (AS/OR: 11, US/UM: 6).
- **Emerald is feature-complete**: any-ability (PK3 0x1E), fully playable **Deoxys forms**, 6-Pokemon
  Battle Tower, Soul Dew un-nerf, full Frontier unban.
- **Platinum** adds 6-Pokemon Tower, permanent Giratina-Origin / Sky Shaymin, Arceus typing in
  doubles, and **AbilityLock** (hacked abilities survive forme changes).
- **PKHaX** now also allows the **Gen-1 RBY sprite/type "desync"** combinations (the mismatched
  species-sprite/type pairings the stock editor blocks), an **all-games level-255 cap** (set any
  Pokemon's stored party level up to 255 in every generation Gen 1-9/LGPE/BDSP/LA; the game reads the
  party level byte directly and never clamps it to 100 on load — party-only and volatile),
  and the **Gen-1 "No Move" glitch move** (move ID `0x00` — Fissure's animation, 102 power, glitch type,
  ~31.6% accuracy on Yellow) as a selectable move distinct from `(None)`, written to the save with real
  PP so it is usable from the FIGHT menu on cartridge. **Slot 1 only:** the Gen 1 menu treats `0x00`
  as the move-list terminator, so in any other slot it is unreachable and hides the moves after it;
  in slot 1 all rows show `-` and the first row uses the corrupted move (save first — it can freeze
  the game if the hit doesn't KO).
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
| 1 | Red/Blue/Yellow | RBY sprite/type "desync" combos; **level up to 255**; **"No Move" glitch move (0x00)** | PKHaX | `PKHaX/` |
| 2 | Gold/Silver/Crystal | **level up to 255** in the save editor;  **"No Move" glitch move (0x00)** | PKHaX | `PKHaX/` |
| 3 | Emerald | Frontier ban list + level cap + Species/Item Clause; Soul Dew un-nerf; any-ability; Deoxys forms; 6-Pokemon Tower | **level up to 255** in the save editor | IPS + source patch + PKHaX | `gen3_emerald/`, `PKHaX/` |
| 4 | Platinum | Frontier ban list + Species/Item Clause; permanent Giratina-O/Rotom/Sky-Shaymin; Soul Dew un-nerf; Arceus form-typing (incl. doubles); 6-Pokemon Tower; AbilityLock | **level up to 255** in the save editor | xdelta + source patches | `gen4_platinum/` |
| 5 | Black 2 / White 2 | Subway + Institute + PWT ban list + Species/Item Clause (legal party size kept, no PWT freeze); Arceus form-typing; **Pokéstar Studios props usable (no Bad Egg)** | **level up to 255** in the save editor | Python + xdelta + PKHaX | `gen5_bw2/`, `gen45_nds_arceus_typefix/` |
| 6 | Omega Ruby / Alpha Sapphire | Maison ban list + clauses + team-size + 510 EV cap; forme persistence (full Hoopa); Arceus form-typing | **level up to 255** in the save editor | Python (cia/3ds) | `gen6_oras/`, `gen67_arceus_typefix/` |
| 7 | Ultra Sun / Ultra Moon | Tree ban list + clauses; Prankster/Gale Wings/Parental Bond/Soul Dew un-nerfs (+ matching text); forme persistence; Arceus+Silvally form-typing; **Protean-Arceus/Silvally** | **level up to 255** in the save editor | Python (cia) | `gen7_usum/`, `gen67_arceus_typefix/` |
| 8 | Sword / Shield | Tower Species/Item Clause; Crowned + Eternamax persistence; Dynamax unlock | **level up to 255** in the save editor | LayeredFS pchtxt + Python | `gen8_swsh/` |
| Switch | Brilliant Diamond / Shining Pearl | Tower ban list + Species/Item Clause | **level up to 255** in the save editor| exefs ips/pchtxt + Python | `bdsp/` |
| 9 | Scarlet / Violet | **v1.0.0 Treasures of Ruin stats restored** (the day-one 1.0.1 nerf undone), optional Gen 8 Zacian/Zamazenta/Cresselia stats; **level up to 255** in the save editor | Python (extracted romfs personal data) + PKHaX | `gen9_sv/` |

*Please note that lvl 255 is an experimental feature across all games. YMMV.*

PKHaX (a patched PKHeX save editor) lives in `PKHaX/`; the built `PKHeX.exe` is included and is what
you attach as a GitHub Release.

To keep some features and drop others, see **[Features / how to disable each one](#features--how-to-disable-each-one)**
at the bottom - it lists, per game, exactly which patch / tagged block / byte to omit for each feature.

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

A patched PKHeX. On top of stock it adds: Gen-1 RBY sprite/type desync combos; an **all-games level-255
cap** (HaX mode lets you set any Pokemon's stored party level up to 255 in every generation and it
persists in-game — see "Level 255" below); the **Gen-1 "No Move" glitch move** (a dropdown entry for move `0x00`,
separate from `(None)`, that keeps its PP and slot so the corrupted move is selectable in battle on
real hardware); a Gen-3 any-ability dropdown (all 78, written to PK3 0x1E for the patched
Emerald); Deoxys form box icons; status-condition editing in every generation (a dropdown for Gen 1/2,
a lower-left clickable icon for Gen 3+ — see "Status condition editing" below); and loosened legality
where the un-nerf ROMs make otherwise-"illegal" mons valid. Source is in `PKHaX/`; rebuild on Windows with `dotnet publish -c Release -r win-x64`. The
committed `PKHeX.exe` is the current build.

### Team + PC pop-out, and battle team editing (new in v6)

Two long-standing PKHeX limitations are gone.

**Battle teams are editable.** Stock PKHeX padlocks the box slots your registered battle teams point
at and refuses to write them, so the Pokemon on a battle team could not be edited at all. PKHaX now
allows writes to *battle-team* locks specifically (other lock types, e.g. starters, stay protected),
toggleable in Settings → SlotWrite → `AllowBattleTeamEdits`. A new **Battle Team Manager** lists every
team with its six slots (sprite plus the box/slot it points at), and lets you assign a slot, clear a
slot, clear a whole team, and flip each team's Locked flag. Generation 7 teams are index lists into
your boxes (editing a slot edits the box Pokemon it references); Generation 6's Battle Box is separate
storage and is handled as such.

**Pop-out "Team + PC" window (all generations).** Opens with **Ctrl+T**, or from the box pop-out menu
next to "Single Box" / "All Boxes". It shows your **party**, your **battle teams** (only for saves that
have them), and a full **PC box** with its box switcher side by side, and you can **click and drag
Pokemon freely between all three** — party to box, box to team, team to party, and across to the main
window's box view, exactly like PKHeX's normal drag-and-drop. Dropping onto a Generation 7 team slot
writes the box slot that team slot references; the panels refresh live.

### Level 255 (all games)

Every generation stores a party Pokemon's level as a single byte that the game engine reads directly (the box format has no level byte — only EXP). RBY and GSC read this byte, and so does every later game; no game
clamps a pre-existing level byte back to 100 (the level-up routine only refuses to *raise* a mon past
100, and Gen 2's `CorrectPartyErrors` clamp is dead/unreferenced code). So a save-edited level-255 mon
persists. In HaX mode, PKHaX lets you type a level up to 255 for any Gen-1 (PK1) or Gen-2 (PK2) mon; it
writes the value to the stored level byte (`Stat_Level`) and stops the party-stat refresh from resetting
it. Three small source changes implement this — see `PKHaX/LEVEL255_CHANGES.md`.

Caveats: the 8-bit stat formula overflows at very high levels, so stats may look wrapped rather than
cleanly huge (the level itself stays 255); legality will flag the mon (expected for a HaX feature); and
a >100 mon cannot be traded Gen-1 -> Gen-2 over the Time Capsule (`ValidateOTTrademon` rejects level
>100), so edit each game's save directly.

### Status condition editing (all generations)

You can now set a Pokemon's status condition (Sleep / Poison / Burn / Freeze / Paralysis) in every
generation:

- **Gen 1 and Gen 2:** an explicit **Status dropdown** in the editor's "Gen-1:" / "Gen-2:" panel
  (the same panel that holds the Gen-1 sprite/type controls). For Gen 1/2 the chosen status is also
  carried in the Showdown set export/import and shown when you hover a Pokemon's sprite (see "Gen 1/2
  Showdown desync export/import" / `PKHaX/GEN12_DESYNC_CHANGES.md`).
- **All other generations (3+):** a clickable status icon in the **lower-left corner** of the entity
  editor. It is blank (effectively invisible) until a status is set — just click that lower-left area to
  open the picker and choose a status; once set, the matching status sprite appears there. (Visibility is
  controlled by the existing "Show Status Condition" setting, which is on by default.)

---

## Pokéstar Studios props (Black 2 / White 2)

Pokéstar Studios movie opponents live in BW2 at internal species indices **652–684** — above the
National-Dex max of 649. They have real personal data (base stats, types) and battle sprites, but the
game never lets you *own* one, and injecting one made it show up as a **Bad Egg**.

**What the Bad Egg actually was (the myth, busted).** It is *not* a species check. A prop written with a
valid checksum is a fully valid Pokémon; the game's per-mon validator (arm9 `0x201DDC8`) only flags
`checksumFailed` when a freshly-computed checksum ≠ the stored one — there is **no species gate** in that
path (confirmed against the Gen-4/5 decomp and by disassembly). The Bad Egg was a **timing race**: during
party-load the game briefly modifies a prop's data block, and if the validator runs in that window it
sees a transient mismatch and sets the *sticky* `checksumFailed` bit. melonDS usually wins the race
(prop shows fine); DeSmuME loses it consistently (Bad Egg). This was proven by diffing two melonDS save
states — identical clean arm9, byte-identical valid prop, the only difference being that one had the
`checksumFailed` flag set. (Full write-up in `POKESTAR.md` / the project log.)

**The fix (ROM).** A minimal arm9 code-cave: at the validator's flag-set site, read the prop's decrypted,
PID-unshuffled species; if it is a prop (652–684), take the "valid" path and never set `checksumFailed`.
Non-props are untouched, so genuinely corrupt saves still show Bad Eggs. It is folded into the BW2 patch
alongside the ban-list removal, clause removal, Arceus form-typing, and the prop back-sprite fix; apply
the provided xdelta to a clean Black 2 / White 2, or run `gen5_bw2/tools/bw2_pokestar_build.py`.
Verified by Unicorn-emulating the patched validator across every PID shuffle value.

**The fix (PKHaX).** The 17 user-facing props (`Pokestar UFO`, `Pokestar F-00`, `Pokestar Smeargle`, …)
carry their exact Showdown names, import/export as sets, appear in the B2W2 species dropdown and search,
and are treated as registered dex mons with **BST 100** (Smeargle keeps real Smeargle stats). Because
652–684 collide with Gen-6 National-Dex numbers (652 = Chesnaught, 660 = Diggersby, …), **every prop
lookup is gated to Gen-5 context**, so Gen-6+ editing is completely unaffected. Props now show their
Pokéstar name on hover (not "Diggersby"/"Delphox") and preview an extracted sprite instead of the Gen-6
Pokémon. Drag a prop onto a party slot, save, and load the patched ROM.

---

## Features / how to disable each one

The "I want everything EXCEPT feature X" guide. Each game ships as a small set of patches; for every
feature below you get the exact thing to remove. Two patch kinds:

- **Source patches** (`.patch` for the decomp games, `.py` scripts): editable as TEXT. Every feature is
  wrapped in a tagged block - search for the tag and delete that block, then re-apply / rebuild. Tags
  used: `// UN-NERF [name]`, `// PKHaX [name]`, `# UNNERF: [name]`.
- **Binary patches** (`.ips`, `.xdelta`, exefs `.ips`/`.pchtxt`): you cannot comment a binary diff. To
  drop a feature, use the per-feature **single-purpose patch** instead of the all-in-one, or skip the
  listed byte edit. The per-feature breakdown below lets you rebuild a custom binary.

**Gen 3 - Emerald (`gen3_emerald/`).** For **everything in one file** (Frontier unban + clauses + Soul
Dew + any-ability + **Deoxys forms**), apply the prebuilt **`Emerald_UnNerf_Full.xdelta`** to a clean
Emerald — it reproduces the finished decomp build (`Emerald_UNBANNED_..._DEOXYS.gba`). The `.ips` files
below are a lighter, retail-native subset (they do **not** include Deoxys forms or 6-mon, which are
decomp-source changes):
- **All-in-one IPS** `3_Emerald_Full_Hackmons_v3.ips` = patch 1 + patch 2 + flash-save fix (Frontier
  unban + clauses + Soul Dew + any-ability). **No Deoxys / 6-mon** — use the full xdelta for those.
- **Frontier unban + clauses + Soul Dew un-nerf** -> apply only `1_Emerald_FrontierUnlock_SoulDew.ips`
  (decomp tags `// UN-NERF Frontier`, `// UN-NERF SoulDew`).
- **Any-ability (PK3 0x1E)** -> apply only `2_Emerald_AnyAbility.ips` (decomp tag `// PKHaX AnyAbility`).
- **Deoxys forms** -> in the decomp `.patch`, delete the `// UN-NERF Deoxys*` blocks (stats / sprites /
  icons / summary / default-form / trade); each sub-feature is its own tag. (Already in the full xdelta.)
- **6-Pokemon party** -> `emerald_6pokemon_full.patch`; omit (or delete `// UN-NERF PartySize`) for legal sizes.

**Gen 4 - Platinum (`gen4_platinum/`).** The prebuilt **`Platinum_UnNerf_Full.xdelta`** is the full
build — it includes **every** feature below (ban list + clauses + Soul Dew + Giratina-O/Rotom/Shaymin
persistence + Arceus form-typing + Arceus doubles + 6-Pokemon + AbilityLock). Apply it to a clean
Platinum for the complete un-nerf. The source `.patch` files below tag each feature so you can build a
custom subset instead.
- **Ban list** -> arm9 banned-species zero (tag `// UN-NERF BanList`).
- **Species + Item Clause** -> two arm9 gates (tag `// UN-NERF Clauses`).
- **Forme persistence (Giratina-O/Rotom/Shaymin-Sky)** -> overlay 5 `0x021F6DC2: C0 46 -> 01 D1` to
  revert (tag `// UN-NERF FormePersist`).
- **Giratina-O no-orb battle revert kill** -> overlay 16 `0x02259FBD: E0 -> D1` to restore stock
  (tag `// UN-NERF GiratinaBattleRevert`).
- **Soul Dew un-nerf** -> overlay `0x0225A5F0` + `0x0225A61E` (restore stock bytes; tag `// UN-NERF SoulDew`).
- **Arceus doubles typing** -> the doubles eligibility/distinctness neutralization (tag `// UN-NERF ArceusDoubles`).
- **6-Pokemon** -> `platinum_6pokemon_singles.patch`; omit for legal sizes (tag `// UN-NERF PartySize`).
- **AbilityLock** -> `Platinum_AbilityLock_pokemon_c.patch`; omit to recompute default ability (tag `// PKHaX AbilityLock`).
- **Arceus form-typing (incl. the ??? form!)** -> `platinum_arceus_formtype.patch`; makes a no-plate
  Arceus keep its PKHeX-set form's type (form id == type id, so the Gen-4-only **???** form = type 9
  works and is Battle-Tower-legal). Omit for stock plate-only typing (tag `// UN-NERF ArceusFormType`).

**Gen 5 - Black 2 / White 2 (`gen5_bw2/`, `gen45_nds_arceus_typefix/`).** One patch, one script - both do
everything (ban list + clauses + Arceus form-typing + Pokéstar props usable + prop back-sprites):
- **Everything at once** -> apply the prebuilt `gen5_bw2/*_UnNerf_Pokestar.xdelta`, **or** run
  `gen5_bw2/bw2_nobanlist.py` (byte-identical result; the script needs `pip install ndspy`).
- **Ban list / Species Clause / Item Clause only** -> `bw2_nobanlist.py --banlist-only` (pure-stdlib, no deps).
- **Drop or rebuild individual pieces** -> the steps live in `gen5_bw2/tools/` (`bw2_pokestar_build.py`,
  `bw2_pokestar_validator_fix.py`, `bw2_pokestar_sprites.py`) plus `gen45_nds_arceus_typefix/`; run only
  the ones you want. See `gen5_bw2/POKESTAR.md`.
- **Arceus type-by-form** -> `gen45_nds_arceus_typefix/bw2_arceus_typefix.py`; don't run it to keep stock
  Multitype-only typing.
- (6-mon and forme-persistence are not included for BW2.)

**Gen 6 - ORAS (`gen6_oras/`, `gen67_arceus_typefix/`).** Python patchers - each feature is a function/flag:
- **Maison unban / clauses / team-size / EV cap** -> `oras_nobanlist.py` + `oras_no_restrictions.py` +
  `oras_evcap.py`; run only the ones you want (team-size and EV-cap are separate tagged blocks).
- **Forme persistence + Hoopa** -> `gen6_oras/formepersist.py` (`--full` for Hoopa).
- **Arceus form-typing (getter cave)** -> `gen67_arceus_typefix/`; skip for stock plate-only typing.

**Gen 7 - USUM (`gen7_usum/`, `gen67_arceus_typefix/`).**
- **Battle Tree unban / clauses** -> `gen7_usum/unnerf.py --mode nbl`.
- **Prankster / Gale Wings / Parental Bond / Soul Dew un-nerfs** -> `unnerf.py --mode prankster` /
  `galewings` / `parentalbond` / `souldew` (or `all`); each is independent, matching text via `gametext.py`.
- **Forme persistence + Hoopa** -> `gen7_usum/formepersist.py`.
- **Arceus + Silvally form-typing** -> `gen67_arceus_typefix/` (USUM path).
- **Protean on Arceus/Silvally** -> the Battle.cro species-list clear (`{0x1ED,0x305} -> 0xFFFF` at
  +0x102670); skip that 4-byte edit to keep their type locked.

**Gen 8 - SwSh (`gen8_swsh/`).** LayeredFS - per title-ID you get `NoTowerClause/`, `FormePersist/`,
`DynamaxCandyAll/`. Include only the subfolders for the features you want; each is self-contained.

**BDSP (`bdsp/`).** LayeredFS - the exefs `noclause` overlay (Tower banlist + clauses) is the only
feature; omit it to keep stock.

> **Why not "one patch per game with inline comments" for the binary games?** A binary diff
> (`.ips`/`.xdelta`) is just "write these bytes here" - there's no text to comment. So the binary games
> get feature granularity as **separate single-purpose patches** (above), while the decomp games
> (Emerald, Platinum) carry the inline `// UN-NERF` / `// PKHaX` tags you delete to drop a feature.

---

## Credits

Builds on community findings, including SmolJoltik and ABZB (Platinum Frontier forme/banlist),
MeroMero (gen-5 Subway/PWT regulation offsets), theSLAYER’s prior work on Gen 5 Pokestar editing,
Kurt (kwsch) for creating and maintaining [PKHeX](https://github.com/kwsch/PKHeX) — the save editor
our PKHaX build is based on — and the broader projectpokemon.org and hackmons.com
research threads.
