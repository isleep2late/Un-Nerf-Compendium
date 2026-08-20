# The Un-Nerf Compendium

A one-stop collection of patches that restore Pokemon games to how a lot of us wish they still played:
un-nerfed abilities and items, lifted Battle Frontier / Subway / Maison / Tree / Tower ban lists and
clauses, form-driven typing, and permanent alternate formes, across **Generations 3 through 9 plus
BDSP**. The included **PKHaX** save editor (desktop and mobile) adds Gen 1-2 save editing, an all-games
level-255 cap, and **emulator save-state editing for Gens 1-7** — including the Space World '97
prototype and live in-battle typing edits.

> You supply your own **legally-dumped** games. **Nothing copyrighted is distributed here, so do NOT
> ask for ROMs, CIAs, or saves** - only small patch tools and patch files.

Join our Discord: https://discord.gg/hackmons

---

## What is included

| Gen | Game(s) | What it removes / restores | Delivered as | Folder |
|-----|---------|----------------------------|--------------|--------|
| 1 | Red/Blue/Yellow | RBY sprite/type "desync" combos; **level up to 255**; **"No Move" glitch move (0x00)** | PKHaX | `PKHaX/` |
| 2 | Gold/Silver/Crystal | **level up to 255** in the save editor;  **"No Move" glitch move (0x00)** | PKHaX | `PKHaX/` |
| 2 proto | **Space World '97 demo** (Gold/Silver prototype) | **save-state (RAM) editing**: party, level 255, DVs, moves, **persistent disguises**, **volatile battle typing** — the prototype cannot reload its own save, so RAM is the only place edits survive | PKHaX + PKHaX Mobile | `PKHaX/`, `PKHaX-Mobile/` |
| 1-7 | Retail games in an emulator | **save-state editing** (mGBA, BGB, SameBoy, VBA-M, melonDS, DeSmuME, Citra/Azahar): the embedded cartridge save opens in the full editor and writes back into the state; the live in-RAM party is editable directly — incl. persistent Gen 1 typing/sprite desyncs and mid-battle typing edits for Gens 2-5 | PKHaX + PKHaX Mobile | `PKHaX/`, `PKHaX-Mobile/` |
| 3 | Emerald | Frontier ban list + level cap + Species/Item Clause; Soul Dew un-nerf; any-ability; Deoxys forms; 6-Pokemon Tower | **level up to 255** in the save editor | IPS + source patch + PKHaX | `gen3_emerald/`, `PKHaX/` |
| 4 | Platinum | Frontier ban list + Species/Item Clause; permanent Giratina-O/Rotom/Sky-Shaymin; Soul Dew un-nerf; Arceus form-typing (incl. doubles); 6-Pokemon Tower; AbilityLock | **level up to 255** in the save editor | xdelta + source patches | `gen4_platinum/` |
| 5 | Black 2 / White 2 | Subway + Institute + PWT ban list + Species/Item Clause (legal party size kept, no PWT freeze); Arceus form-typing; **Pokéstar Studios props usable (no Bad Egg)** | **level up to 255** in the save editor | Python + xdelta + PKHaX | `gen5_bw2/`, `gen45_nds_arceus_typefix/` |
| 6 | Omega Ruby / Alpha Sapphire | Maison ban list + clauses + team-size + 510 EV cap; forme persistence (full Hoopa); Arceus form-typing; **hacked Abilities stay on Xerneas** | **level up to 255** in the save editor | Python (cia/3ds) | `gen6_oras/`, `gen67_arceus_typefix/` |
| 7 | Ultra Sun / Ultra Moon | Tree ban list + clauses; Prankster/Gale Wings/Parental Bond/Soul Dew un-nerfs (+ matching text); forme persistence; Arceus+Silvally form-typing; **Protean-Arceus/Silvally** | **level up to 255** in the save editor | Python (cia) | `gen7_usum/`, `gen67_arceus_typefix/` |
| 8 | Sword / Shield | Tower Species/Item Clause; Crowned + Eternamax persistence; Dynamax unlock | **level up to 255** in the save editor | LayeredFS pchtxt + Python | `gen8_swsh/` |
| Switch | Brilliant Diamond / Shining Pearl | Tower ban list + Species/Item Clause | **level up to 255** in the save editor| exefs ips/pchtxt + Python | `bdsp/` |
| 9 | Scarlet / Violet | **v1.0.0 Treasures of Ruin stats restored** (the day-one 1.0.1 nerf undone), optional Gen 8 Zacian/Zamazenta/Cresselia stats; **level up to 255** in the save editor | Python (extracted romfs personal data) + PKHaX | `gen9_sv/` |

*Please note that lvl 255 is an experimental feature across all games. YMMV.*

*On the Gen 9 stat restore: no Sword/Shield update ever changed a base stat — the Treasures of Ruin
and the Zacian/Zamazenta/Cresselia values all changed inside Scarlet/Violet (the day-one 1.0.1 patch
and the Gen 8→9 transition respectively), so `gen9_sv/` restores those. The Neutralizing Gas behavior
change (SV 3.0.0) is code-side rather than a stat, so the folder documents your options instead of
pretending to patch it.*

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
Emerald); **Space World '97 save-state (RAM) editing** (party, disguises, battle typing — desktop and mobile); Deoxys form box icons; status-condition editing in every generation (a dropdown for Gen 1/2,
a lower-left clickable icon for Gen 3+ — see "Status condition editing" below); and loosened legality
where the un-nerf ROMs make otherwise-"illegal" mons valid. Source is in `PKHaX/`; rebuild on Windows with `dotnet publish -c Release -r win-x64`. The
committed `PKHeX.exe` is the current build.

### Emulator save states — Gens 1-7

Save-state editing is no longer SpaceWorld-only. PKHaX opens **emulator save states** for the retail
Gen 1-7 games and finds every piece of Pokémon data inside:

| Emulator | State | Consoles |
|---|---|---|
| **mGBA** | `.ss0`-`.ss9` (PNG-wrapped or raw) | Game Boy, Game Boy Color, Game Boy Advance |
| **BGB** | `.sn0`-`.sn9`, `.sna` (BESS) | Game Boy, Game Boy Color |
| **SameBoy / Emulicious** | `.s0`-`.s9`, BESS states | Game Boy, Game Boy Color |
| **VisualBoyAdvance-M** | `.sgm` | GB and GBA |
| **melonDS** | `.ml1`-`.ml9`, `.mln` | Nintendo DS |
| **DeSmuME** | `.dst`, `.ds0`-`.ds9` | Nintendo DS |
| **PyBoy** and other raw dumps | `.state` | Game Boy |
| **Citra / Azahar / Lime3DS** | `.cst` | Nintendo 3DS |

Drop a state on `PKHaX.exe` (or open it in PKHaX Mobile) and a picker shows what was found:

- **The game's save file, when the state carries one.** mGBA and BESS states embed the cartridge
  SRAM, and DS states embed the 512 KB flash save — that is the *complete* save, so it opens in the
  **full editor**: boxes, bag, trainer, everything the save-file editor can do. Exporting offers to
  write the edits back **into the state file** (a `.bak` of the original is kept).
- **The live party in console RAM.** Gen 1/2 parties are located at the exact WRAM addresses from the
  pret disassemblies (Red/Blue, Yellow, Gold/Silver, Crystal are all distinguished automatically);
  Gen 3/4 parties are found by scanning for validly-checksummed encrypted party structures, so
  Emerald/FRLG's save-block ASLR does not matter. Party edits are synced back into RAM, so they are
  live the moment the state is loaded — no in-game save or reset needed.
- **Typing and sprites.** Gen 1 stores each Pokémon's two type bytes *in the party structure*, so Gen 1
  type edits are persistent and battle-live, and the classic sprite/type desync (list byte vs data byte)
  is editable per slot — the same levers PKHaX already exposes for Gen 1 saves. Gen 2 derives types from
  the species except during battle, so a state taken mid-battle offers the volatile battle-typing editor
  (the SpaceWorld model). **Gen 3-7 typing is battle-volatile too, and now editable**: a state taken
  mid-battle offers a battle-typing editor over the engine's live battler structures. Gens 3-5 anchor on
  your party (so Emerald/FRLG's shifting addresses don't matter) and were verified in-game — a wild
  Zigzagoon edited to Steel resists Tackle, and edited to Ghost is immune to it. **Gens 6-7 (X/Y, ORAS,
  SM, USUM on Citra/Azahar)** edit the 3DS battle engine's cached BTL_POKEPARAM type bytes, located by a
  coincidence-proof signature (the decrypted species is repeated inside the block) cross-checked against
  each Pokémon's natural typing, writing both of the engine's double-buffered copies. The offsets and
  the effect were confirmed on a live Azahar battle: writing Ghost onto a Normal-type foe flips a Ghost
  move from "No effect" to "Super effective". In every generation a species swap that keeps the stored
  stats also acts as a Gen-1-style disguise until the game next recalculates.

Coverage now runs **Gen 1 through Gen 7**: Gens 1-3 on mGBA/BGB/VBA-M states, Gens 4-5 on melonDS and
DeSmuME states, and Gens 6-7 on Citra-family `.cst` states (party located by checksum scan inside the
decompressed console RAM; edits are re-compressed into a state the same emulator build loads).
**Nintendo Switch (Gens 8-9 and Let's Go) cannot be supported this way: no Switch emulator has save
states** — not Ryujinx or any of its continuations (the current maintainers explicitly declined the
feature), and not yuzu or any of its forks. On real hardware the equivalent live-RAM route is
sys-botbase + LiveHeX (PKHeX-Plugins), and JKSV for save dumps — both work with stock PKHeX/PKHaX.

The scan never runs over compressed data (a PNG-wrapped state is properly unpacked first), so the
garbage-party false positives of build 2026-08-18 are structurally impossible now. BizHawk `.State`
files are zip+zstd archives and are refused with a clear message rather than misread.

### Space World '97 save states — RAM editing

PKHaX opens **emulator save states** for the 1997 Space World Gold/Silver prototype. Drop one on
`PKHaX.exe` and a SpaceWorld party editor opens: species, level (up to 255), DVs, stat experience,
moves with PP Ups, nickname and OT in the prototype's Japanese character map, **disguises**, and the
**battle types**. It also opens the demo's own 32 KB battery file.

This is RAM editing rather than save editing, and the prototype is the reason it has to be. Its save
routine works and writes a valid battery file, but the main menu computes the save-file check into `A`
and immediately overwrites it, so `Continue` is unreachable dead code and the game can never load what
it wrote. A save state is the only place a hacked party survives.

Two SpaceWorld-specific notes:

- **Disguises are persistent.** The game copies level, status, HP and all five stats out of the stored
  party entry and only *then* derives types from the species, so changing the species byte alone gives
  a Pokemon that shows one species' name, dex number, sprite and types while fighting with another's
  stats. The editor's "Disguise (keep stored stats)" box does exactly that.
- **Typing is volatile.** The stored entry has no type bytes at all; the editable copies live in the
  battle structure and are re-derived on every send-out, so a type edit applies to the currently active
  Pokemon and is lost when it switches. The editor only enables that section for a state taken during a
  battle.

Species, base stats, types, moves and the character map all come from the pret `pokegold-spaceworld`
disassembly. Recognition runs only after PKHeX's own save detection has already declined a file, and a
32 KB file is only treated as SpaceWorld when its party block is structurally valid **and** all three of
the prototype's checksums verify — so a Gold/Silver/Crystal save is never at risk of being read as one.

**It works on the phone too.** PKHaX Mobile opens the same states: pick one with "Open save file" and a
**SpaceWorld party** button appears, with the party list, a per-slot editor (species, level, DVs, moves,
nickname/OT, the disguise switch, max/recalculate) and the battle-type rows when the state was taken during
a battle. The PC-box, battle-team, bag and trainer pages hide themselves for a SpaceWorld state, because the
prototype has none of those in a form PKHeX models — the party is the whole product (its PC save routine is
dummied out with an unconditional `ret`).

### PKHaX Mobile — iOS and Android

PKHaX now runs on your phone. `PKHaX-Mobile/` is a **.NET MAUI** app that references this repo's own
`PKHaX/PKHeX.Core` directly, so it inherits **every** fork feature above — Gen-3 any-ability, the Gen-1
sprite/type desync, Deoxys forms, "No Move", level 255 — and picks up each upstream PKHeX sync
automatically the next time it is built. There is no second copy of the save logic to keep in step.

It edits the save **in place**, wherever your emulator keeps it: Android uses the Storage Access
Framework with a persisted read/write grant, iOS a security-scoped bookmark, so "Save changes to file"
overwrites the original rather than a copy. The UI is built for touch — a swipeable three-wide box grid,
tap a Pokemon to edit species, level, ability (the full list, so Gen-3 any-ability works exactly as on
desktop), shiny and moves. **Illegal-edit mode is on by default**, which is the whole point of the `HaX`
spelling; a legality read-out is shown but never blocks a write.

Grab `PKHaX.apk` (Android) or `PKHaX-unsigned.ipa` (iOS) from the release assets. Android installs
directly — enable "install unknown apps" for your browser or file manager. The iOS build is unsigned, so
sideload it with **AltStore**, **Sideloadly** or **iMazing**, which re-sign it with your own Apple ID; if
you have a paid Apple Developer account you can instead build an ad-hoc signed copy yourself (see
`PKHaX-Mobile/docs/ios-cloud-build.md`). The app also checks for newer builds on launch and offers a
one-tap in-place upgrade that keeps your settings.

Building it yourself: Android needs only the .NET 10 SDK plus the Android SDK and works on Linux,
Windows or macOS — run `PKHaX-Mobile/build-android-local.sh`, or
`dotnet build PKHaX-Mobile/src/PKHaX.Mobile.csproj -c Release -f net10.0-android -p:AndroidPackageFormats=apk`.
iOS binaries can only be produced on macOS (Apple's rule), so the included GitHub Actions workflow
`.github/workflows/build-mobile.yml` builds both platforms on a hosted macOS runner — no Mac of your own
required. Full notes in `PKHaX-Mobile/README.md`.

### Team + PC pop-out, and battle team editing

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
- **Hacked Abilities stay on Xerneas** -> `gen6_oras/oras_xerneas_ability.py`. Xerneas keeps Fairy
  Aura by default; what this removes is the engine putting Fairy Aura *back* on a Xerneas whose
  Ability you edited. Run it, then set the Ability in PKHaX as you would for any other Pokemon.
  Skip this script to keep the stock behaviour. Details, and why the obvious fixes do not work, are
  in **"Why Xerneas was the one Pokemon you could not re-ability"** below.
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

## Known limitations

A few things are deliberately not done, because the game engine fights back harder than a patch can
reach:

- **Protean on Arceus/Silvally works in USUM but not ORAS.** In Gen 6 the Arceus type is re-derived
  from its forme *inside the move pipeline* on every move, so it cannot be removed without breaking
  move processing; Gen 7 keeps the re-derivation out of that path, so clearing the `{Arceus, Silvally}`
  type-lock list is enough there. Castform and Kecleon Protean work in ORAS (no species block on them).
- **6-Pokémon facility teams are Emerald/Platinum only.** Raising the party-size limit above the legal
  cap in **BW2 and USUM** crashes on team confirm — the team buffer is fixed-size, and only the decomp
  games (Emerald, Platinum) can rebuild it.
- **Permanent Giratina-Origin / Sky-Shaymin in BW2** is not done (it needs binary reverse-engineering
  of the forme-revert path that the decomp Platinum patch handles in source).
- **Nintendo Switch save states do not exist.** No Switch emulator (Ryujinx and its continuations, or
  yuzu and its forks) implements save states, so Gens 8/9 and Let's Go stop at the save-file editor;
  their live-RAM route on real hardware is sys-botbase + LiveHeX. See "Emulator save states".

---

## Why Xerneas was the one Pokemon you could not re-ability

Worth writing down, because almost every guess about this turns out to be wrong.

**It is not a legality check, and it is not simply "all three Ability slots are Fairy Aura."**
Plenty of species have one Ability in all three slots — Arceus, Deoxys, Yveltal, Zygarde — and none
of them is ability-locked. Three identical slots are the *enabling condition*, not the lock.

The lock is a **runtime re-derivation**. Whenever the engine changes a Pokemon's forme it recomputes
the Ability from the personal table and stores the result back over whatever was there:

```
r0 = <personal ability getter>(species, newForm, abilitySlot)
     pml::pokepara::CoreParam::SetTokusei(mon, r0)
```

Two facts combine to make this fatal for Xerneas specifically. Its `FormStatsIndex` is 0, so Neutral
and Active share a single personal row and *every* slot index resolves to Fairy Aura. And it is put
through a forme change on the way into a battle. So the stored byte is overwritten going in, and
overwritten again coming out — which is why the old "store it as Xerneas-Active" trick only ever
survived a single battle.

**What does not work:** NOPing the forme-change call. That was the first thing tried and it fails.
`ChangeFormNo` has **32 callers**; silencing the one that is easy to find still leaves the path the
battle module actually uses, and Fairy Aura comes back exactly as before.

**What does work:** guard the *write* instead of the callers. `SetTokusei` has only **three** call
sites in the whole executable, and the two that re-derive from the personal table become

```
cmp   rN, #0x2CC        ; species 716?
blne  SetTokusei        ; store only if it is not Xerneas
```

Guarding the sink covers every path that reaches it, including calls made from the battle module.
The third call site is left alone deliberately: both of its callers zero a buffer and build a
Pokemon from a stack template, so it *constructs* a Pokemon rather than rewriting a saved one.

Nine instruction words in total. Both patched functions happen to have a spare slot immediately
after the call, so the displaced instruction is relocated into it and no code is lost. The guard
only fires for species 716, so Megas, Primals, Zygarde, Kyogre/Groudon, Hoopa and every other forme
keep re-deriving normally. Everything written lives in the ExeFS, so the RomFS and its hash chain
are never touched.

**Side effects:** none that are visible. Xerneas keeps Fairy Aura unless you change it, its Neutral
and Active formes share one personal row so their stats, typing and Abilities are identical anyway,
and the script is idempotent — running it twice is a no-op. It locates its patch sites by matching
the surrounding instructions rather than by hardcoded offsets, works on Omega Ruby and Alpha
Sapphire alike, and refuses to write anything if a site does not match.


## Credits

Builds on community findings, including SmolJoltik and ABZB (Platinum Frontier forme/banlist),
MeroMero (gen-5 Subway/PWT regulation offsets), theSLAYER’s prior work on Gen 5 Pokestar editing,
Kurt (kwsch) for creating and maintaining [PKHeX](https://github.com/kwsch/PKHeX) — the save editor
our PKHaX build is based on — and the broader projectpokemon.org and hackmons.com
research threads.
