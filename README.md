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
| 1 | Red/Blue/Yellow | RBY sprite/type "desync" combos; **level up to 255**; **"No Move" glitch move (0x00)**; **Any% 2-swap save corrupter** (patches the save file exactly as it is on disk: party count 255 + valid checksum, timestamped backup first, Trainer ID untouched, unsaved editor changes not included — desktop and mobile) | PKHaX | `PKHaX/` |
| 2 | Gold/Silver/Crystal | **level up to 255** in the save editor;  **"No Move" glitch move (0x00)** | PKHaX | `PKHaX/` |
| 2 proto | **Space World '97 demo** (Gold/Silver prototype) | **save-state (RAM) editing**: party, level 255, DVs, moves, **persistent disguises**, **volatile battle typing** — the prototype cannot reload its own save, so RAM is the only place edits survive | PKHaX + PKHaX Mobile | `PKHaX/`, `PKHaX-Mobile/` |
| 1-7 | Retail games in an emulator | **save-state editing** (mGBA, BGB, SameBoy, VBA-M, melonDS, DeSmuME, Citra/Azahar): the embedded cartridge save opens in the full editor and writes back into the state; the live in-RAM party is editable directly — incl. persistent Gen 1 typing/sprite desyncs and mid-battle typing edits for Gens 2-5 | PKHaX + PKHaX Mobile | `PKHaX/`, `PKHaX-Mobile/` |
| 3 | Emerald | Frontier ban list + level cap + Species/Item Clause; Soul Dew un-nerf; any-ability; Deoxys forms; 6-Pokemon Tower | **level up to 255** in the save editor | IPS + source patch + PKHaX | `gen3_emerald/`, `PKHaX/` |
| 4 | Platinum | Frontier ban list + Species/Item Clause; permanent Giratina-O/Rotom/Sky-Shaymin; Soul Dew un-nerf; Arceus form-typing (incl. doubles); 6-Pokemon Tower; AbilityLock | **level up to 255** in the save editor | xdelta + source patches | `gen4_platinum/` |
| 5 | Black 2 / White 2 | Subway + Institute + PWT ban list + Species/Item Clause (legal party size kept, no PWT freeze); Arceus form-typing; **Pokéstar Studios props usable (no Bad Egg)** | **level up to 255** in the save editor | Python + xdelta + PKHaX | `gen5_bw2/`, `gen45_nds_arceus_typefix/` |
| 6 | Omega Ruby / Alpha Sapphire | Maison ban list + clauses + team-size + 510 EV cap; forme persistence (full Hoopa); Arceus form-typing; **hacked Abilities stay on Xerneas**; **any Pokemon can use Hyperspace Fury** | **level up to 255** in the save editor | Python (cia/3ds) + Azahar load/mods + **one complete patch** | `gen6_oras/`, `gen67_arceus_typefix/` |
| 7 | Ultra Sun / Ultra Moon | Tree ban list + clauses; Prankster/Gale Wings/Parental Bond/Soul Dew un-nerfs (+ matching text); forme persistence; Arceus+Silvally form-typing; **Protean-Arceus/Silvally**; **any Pokemon can use Hyperspace Fury and Dark Void** | **level up to 255** in the save editor | Python (cia; complete patch takes .3ds too) + Azahar load/mods + **one complete patch** | `gen7_usum/`, `gen67_arceus_typefix/` |
| 8 | Sword / Shield | Tower Species/Item Clause; Crowned + Eternamax persistence; Dynamax unlock | **level up to 255** in the save editor | LayeredFS pchtxt + Python | `gen8_swsh/` |
| Switch | Brilliant Diamond / Shining Pearl | Tower ban list + Species/Item Clause | **level up to 255** in the save editor| exefs ips/pchtxt + Python | `bdsp/` |
| 9 | Scarlet / Violet | **v1.0.0 Treasures of Ruin stats restored** (the day-one 1.0.1 nerf undone), optional Gen 8 Zacian/Zamazenta/Cresselia stats; **level up to 255** in the save editor | Python (extracted romfs personal data) + PKHaX | `gen9_sv/` |
| **UNTESTED BETA** 3 | Ruby / Sapphire (rev 0) | Battle Tower ban list + Lv 50 limit + Species/Item Clause; Soul Dew Tower gate; any-ability (PK3 byte 0x1E) | IPS + BPS | `gen3_rs/` |
| **UNTESTED BETA** 4 | Diamond / Pearl (Rev 5) | Battle Tower ban list + Species/Item Clause + level rules; Soul Dew in the Tower; Arceus form-typing | xdelta + BPS + Python | `gen4_dp/` |
| **UNTESTED BETA** 4 | HeartGold / SoulSilver | Battle Frontier ban list (all five facilities) + Species/Item Clause; Soul Dew in the Frontier; Arceus form-typing; permanent Giratina-O/Shaymin-Sky/Rotom | xdelta + BPS | `gen4_hgss/` |
| **UNTESTED BETA** 5 | Black / White | Subway/link ban list + Species/Item Clause; Arceus form-typing | xdelta + BPS + Python | `gen5_bw/` |
| **UNTESTED BETA** 6 | X / Y | Maison ban list + Soul Dew ban; Maison 510 EV cap; hacked Abilities stay on Xerneas; Arceus form-typing; forme persistence | Python (3ds) + Azahar load/mods (1.0 and 1.5) | `gen6_xy/` |
| **UNTESTED BETA** 7 | Sun / Moon | Tree ban list + clauses; Prankster/Gale Wings/Parental Bond/Soul Dew un-nerfs (+ matching text); forme persistence; Arceus+Silvally form-typing; any Pokemon can use Hyperspace Fury and Dark Void | Python (3ds) + xdelta + Azahar load/mods (1.0 and 1.2) | `gen7_sm/` |
| **UNTESTED BETA** 3 (hack) | Heart & Soul 2.0.6 (Emerald hack) | Frontier ban list + Species/Item Clause; party-menu duplicate checks | IPS + BPS from the plain hack | `hacks/gen3_emerald/heart-and-soul-2.0.6/` |
| **UNTESTED BETA** 3 (hack) | Lazarus 2.0 (Emerald hack) | Frontier ban list + Lv 50 limit + Species/Item Clause; party-menu level cap and duplicate checks | IPS + BPS from the plain hack | `hacks/gen3_emerald/lazarus-2/` |
| **UNTESTED BETA** 3 (hack) | Emerald Seaglass 3.0 (Emerald hack) | Frontier ban list + Lv 50 limit + Species/Item Clause; party-menu level cap and duplicate checks | IPS + BPS from the plain hack | `hacks/gen3_emerald/seaglass-3.0/` |
| **UNTESTED BETA** 3 (hack) | SoulGold GBA 1.1.4 (Emerald hack) | Frontier ban list + Species/Item Clause; party-menu duplicate checks | IPS + BPS from the plain hack | `hacks/gen3_emerald/soulgold-gba-1.1.4/` |
| **UNTESTED BETA** 3 (hack) | Gaia 3.2 (FireRed hack) | Union Room Lv 30 limit; any-ability (byte 0x1E, 2..135); FireRed parity edits | IPS + BPS from the plain hack | `hacks/gen3_frlg/gaia-3.2/` |
| **UNTESTED BETA** 3 (hack) | Odyssey 4.1 (FireRed hack) | Union Room Lv 30 limit; any-ability (byte 0x1E, 2..252); FireRed parity edits | IPS + BPS from the plain hack | `hacks/gen3_frlg/odyssey-4.1/` |
| **UNTESTED BETA** 3 (hack) | Vega Fairy Edition EX 1.4 (FireRed hack) | Union Room Lv 30 limit; any-ability (byte 0x1E, 2..77); FireRed parity edits | IPS + BPS from the plain hack | `hacks/gen3_frlg/vega-1.4/` |
| **UNTESTED BETA** 3 (hack) | Ghost Grey 1.03 (FireRed hack) | Union Room Lv 30 limit; any-ability (byte 0x1E, 2..138); FireRed parity edits | IPS + BPS from the plain hack | `hacks/gen3_frlg/ghostgrey-1.03/` |
| **UNTESTED BETA** 3 (hack) | Unbound 2.1.1.1 (FireRed hack, CFRU) | Union Room Lv 30 invite/accept check only | IPS + BPS from the plain hack | `hacks/gen3_frlg/unbound-2.1.1.1/` |
| **UNTESTED BETA** 3 (hack) | Radical Red 4.1 (FireRed hack, CFRU) | Union Room Lv 30 invite/accept check only | IPS + BPS from the plain hack | `hacks/gen3_frlg/radicalred-4.1/` |
| **UNTESTED BETA** 4 (hack) | Following Renegade Platinum 2.0 (Platinum hack) | Frontier ban list + Species/Item Clause + level and rule caps; Soul Dew in the Frontier; Arceus form-typing; permanent Giratina-O/Sky-Shaymin; AbilityLock | xdelta + BPS + Python from the plain hack | `hacks/gen4_platinum/frp-2.0/` |
| **UNTESTED BETA** 6 (hack) | Rav-Yin Y (Y hack) | the X/Y set (Maison rules, EV cap, Xerneas Ability, Arceus form-typing, forme persistence) | Azahar load/mods (IPS + BPS) from the plain hack | `hacks/gen6_xy/rav-yin-y/` |
| **UNTESTED BETA** 6 (hack) | Xtreme-Yang X (X hack) | the X/Y set (Maison rules, EV cap, Xerneas Ability, Arceus form-typing, forme persistence) | Azahar load/mods (IPS + BPS) from the plain hack | `hacks/gen6_xy/xtreme-yang-x/` |
| **UNTESTED BETA** 7 (hack) | Nova Sun Expanded 1.9 (Sun hack) | the Sun/Moon set; the two new descriptions go into the hack's own text | xdelta + Azahar load/mods (IPS + BPS) from the plain hack | `hacks/gen7_sm/nova-sun-1.9/` |
| **UNTESTED BETA** 7 (hack) | Umbra Moon Expanded 1.9 (Moon hack) | the Sun/Moon set; the two new descriptions go into the hack's own text | xdelta + Azahar load/mods (IPS + BPS) from the plain hack | `hacks/gen7_sm/umbra-moon-1.9/` |
| **UNTESTED BETA** 7 (hack) | Supernova Sun Expanded 1.0.5 (Ultra Sun hack) | the complete Ultra Sun / Ultra Moon set; the two new descriptions merged into the hack's own text | Python (3ds) + Azahar load/mods from the plain hack | `hacks/gen7_usum/supernova-sun-1.0.5/` |
| **UNTESTED BETA** 7 (hack) | Penumbra Moon Expanded 1.0.5 (Ultra Moon hack) | the complete Ultra Sun / Ultra Moon set; the two new descriptions merged into the hack's own text | Python (3ds) + Azahar load/mods from the plain hack | `hacks/gen7_usum/penumbra-moon-1.0.5/` |

### Untested beta folders

The folders marked **UNTESTED BETA** in the table above were built on 2026-10-23 with no in-game test: nobody has
booted a patched game, entered a facility or fought a battle with them yet. Each was made from static analysis
(decompilations, reference builds and disassembly), and every check was static: the main one is that every patch
re-applies to its stated input and gives the expected output byte for byte. Each folder's README says UNTESTED BETA at the top and names the
exact input dump it needs. Keep a backup of your save before you try one, and please report what misbehaves.

- `gen3_rs/` (Ruby / Sapphire), `gen4_dp/` (Diamond / Pearl), `gen4_hgss/` (HeartGold / SoulSilver),
  `gen5_bw/` (Black / White), `gen6_xy/` (X / Y), `gen7_sm/` (Sun / Moon)
- `hacks/gen3_emerald/`: `heart-and-soul-2.0.6/`, `lazarus-2/`, `seaglass-3.0/`, `soulgold-gba-1.1.4/`
- `hacks/gen3_frlg/`: `gaia-3.2/`, `odyssey-4.1/`, `vega-1.4/`, `ghostgrey-1.03/`, `unbound-2.1.1.1/`, `radicalred-4.1/`
- `hacks/gen4_platinum/frp-2.0/`
- `hacks/gen6_xy/`: `rav-yin-y/`, `xtreme-yang-x/`
- `hacks/gen7_sm/`: `nova-sun-1.9/`, `umbra-moon-1.9/`
- `hacks/gen7_usum/`: `supernova-sun-1.0.5/`, `penumbra-moon-1.0.5/`

The `hacks/` patches go from the **plain hack** (as its author ships it) to the Un-Nerf build of that hack; they do
not apply to the vanilla game.

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
real hardware); the **Gen-1 Any% 2-swap save corrupter** (`Tools > Corrupt Gen 1 save...` on desktop, a
button on mobile: always writes a timestamped backup first, then patches the file exactly as it is on disk — party
count 255, checksum recomputed so CONTINUE loads it, the Trainer ID never touched, unsaved editor changes not
included; byte-identical to the standalone `corrupt-save-gen1.py`); a Gen-3 any-ability dropdown (all 78, written to PK3 0x1E for the patched
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
- **Hyperspace Fury for any Pokemon** -> `gen6_oras/oras_hyperspace_fury.py` (one byte in the RomFS
  `DllBattle.cro`, `0xBC55F`: `1A` -> `EA`); skip it to keep Hyperspace Fury Hoopa-Unbound-only. Dark
  Void needs nothing in ORAS: Gen 6 never restricted it.
- **Everything at once** -> `gen6_oras/complete_unnerf.py` (see "One complete patch per generation").

**Gen 7 - USUM (`gen7_usum/`, `gen67_arceus_typefix/`).**
- **Battle Tree unban / clauses** -> `gen7_usum/unnerf.py --mode nbl`.
- **Prankster / Gale Wings / Parental Bond / Soul Dew un-nerfs** -> `unnerf.py --mode prankster` /
  `galewings` / `parentalbond` / `souldew` (or `all`); each is independent, matching text via `gametext.py`.
- **Forme persistence + Hoopa** -> `gen7_usum/formepersist.py`.
- **Arceus + Silvally form-typing** -> `gen67_arceus_typefix/` (USUM path).
- **Protean on Arceus/Silvally** -> the Battle.cro species-list clear (`{0x1ED,0x305} -> 0xFFFF` at
  +0x102670); skip that 4-byte edit to keep their type locked.
- **Hyperspace Fury / Dark Void for any Pokemon** -> `unnerf.py --mode hyperspacefury` / `darkvoid`
  (both are in `all`); one byte each in `Battle.cro` (`0xC8267` and `0xC45C3`: `1A` -> `EA`). Skip a mode
  to keep that move locked to Hoopa-Unbound / Darkrai.
- **Everything at once** -> `gen7_usum/complete_unnerf.py` (see "One complete patch per generation").

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

- **Arceus cannot change type in ORAS** - neither through Protean nor through Conversion, Conversion 2,
  Reflect Type or Camouflage, which refuse an Arceus user (an inline species check in `DllBattle.cro`).
  In USUM the same moves read the `{Arceus, Silvally}` list that the Arceus feature already clears.
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

## One complete patch per generation (added 2026-10)

`gen6_oras/complete_unnerf.py` (Omega Ruby / Alpha Sapphire) and `gen7_usum/complete_unnerf.py` (Ultra Sun /
Ultra Moon) put **every** un-nerf for the game into a new copy of your decrypted dump in one pass. They are
the same file; each copy reads the patch set that sits next to it, `azahar-mods/<title id>/all/`, so the
complete patch is always exactly the sum of the per-feature patches in this repo:

- **Omega Ruby / Alpha Sapphire:** Battle Maison ban list + Species/Item Clause + team size + 510 EV cap,
  forme persistence (full Hoopa), Arceus form-typing, hacked Abilities stay on Xerneas, Hyperspace Fury for
  any Pokemon.
- **Ultra Sun / Ultra Moon:** Battle Tree ban list + Species/Item Clause, Prankster / Gale Wings / Parental
  Bond / Soul Dew un-nerfs and their rewritten descriptions, forme persistence (full Hoopa), the Poke Resort
  forme fix, Arceus + Silvally form-typing, Protean on Arceus/Silvally, Hyperspace Fury and Dark Void for any
  Pokemon.

```
python gen6_oras/complete_unnerf.py "Pokemon Omega Ruby (USA) (En,Ja,Fr,De,Es,It,Ko) (Rev 2).3ds"
python gen7_usum/complete_unnerf.py "Pokemon Ultra Moon (USA) (En,Ja,Fr,De,Es,It,Zh,Ko).3ds" --out "UM complete.3ds"
python gen7_usum/complete_unnerf.py game.3ds --verify      (check only, write nothing)
```

Windows users can drag the dump onto `complete_unnerf.bat`. The input is only read; the result is written
next to it as `<name> [Un-Nerf Complete].3ds` unless `--out` says otherwise. It takes a decrypted `.3ds` (the
No-Intro dumps work; a trimmed `.3ds` gives a trimmed result) or a decrypted `.cia` whose `.code` is stored
flat (all US/UM `.cia`s). Every patch is applied as BPS, so a dump that is not the expected v1.0 file is
refused before anything is written; the RomFS hash tree of the input is checked first as well. The No-Intro Omega Ruby / Alpha Sapphire dumps store
`.code` compressed, so the tool stores the patched `.code` uncompressed, clears the compression flag and
moves the RomFS and the later partitions to make room (an untrimmed dump keeps its size). Every hash a loader can
check is rewritten: exheader, ExeFS, every touched RomFS hash block at all three levels, both NCCH
superblock hashes, and for a `.cia` the TMD. (The battle modules' own CRO hash slots are left as they are,
like every earlier `Battle.cro` un-nerf: emulators do not check them; real hardware needs a custom firmware
that skips the CRO/CRR check.)

**Do not install the game's update data** (USUM 1.2, ORAS 1.4) for a patched game: the update ships its own
`.code` and battle engine (`Battle.cro` and the text archive for USUM, `DllBattle.cro` for ORAS), which
replace the patched ones and switch most un-nerfs off. The patches are for the v1.0 base game only, and the
`load/mods` IPS files must not be used with an update installed (they would land on moved code).

## Hyperspace Fury and Dark Void (added 2026-10)

From Gen 6, Hyperspace Fury fails unless Hoopa-Unbound uses it ("But X can't use the move!", or "But Hoopa
can't use it the way it is now!" for Hoopa-Confined), and from Gen 7 Dark Void fails unless Darkrai uses it.
In the battle engine both are per-move event handlers that run before the move executes: the handler reads
the user's species (and, for Hyperspace Fury, its forme) and, if it is the wrong Pokemon, writes the failure
reason the game then prints. The patch turns the handler's first conditional branch into an unconditional
jump to its own return, so the handler never writes a failure reason, for any user. Nothing else changes:
the legitimate users already took that exit, the handlers belong to these two moves only, and Hyperspace
Fury's other handlers (the ones that break through protection) are untouched.

| game | file | handler | patched byte | effect |
|---|---|---|---|---|
| Omega Ruby / Alpha Sapphire | RomFS `DllBattle.cro` | `0xBC544` | `0xBC55F`: `1A` -> `EA` | Hyperspace Fury for any Pokemon |
| Ultra Sun / Ultra Moon | RomFS `Battle.cro` | `0xC824C` | `0xC8267`: `1A` -> `EA` | Hyperspace Fury for any Pokemon |
| Ultra Sun / Ultra Moon | RomFS `Battle.cro` | `0xC45A8` | `0xC45C3`: `1A` -> `EA` | Dark Void for any Pokemon |

Omega Ruby and Alpha Sapphire carry Dark Void with no user restriction at all, so ORAS needs no Dark Void
patch. The tools: `gen6_oras/oras_hyperspace_fury.py`, `gen7_usum/unnerf.py --mode hyperspacefury` /
`--mode darkvoid` (both included in `--mode all`), the `features/` and `all/` load-time patches under each
`azahar-mods/`, and the complete patches above.

## Load-time patch sets (added 2026-09)

The whole-ROM xdeltas are also available as BPS keyed to the No-Intro dumps, with a `SOURCES.md` naming the expected dump (name, size, SHA-1, CRC32): `gen3_emerald/bps/`, `gen4_platinum/bps/`, `gen5_bw2/bps/`. The 3DS un-nerfs are available as Azahar `load/mods` IPS/BPS sets that patch the untouched game at load time instead of rewriting the dump: `gen6_oras/azahar-mods/` and `gen7_usum/azahar-mods/`, each with a README covering layout, base-file hashes and the update caveat. These were derived from the existing xdeltas and scripts and add no new features. The 2026-10 additions
(Hyperspace Fury for ORAS and USUM, Dark Void for USUM) were derived the same way: run the tool on a copy of
the dump, diff the file it changed. **Correction (2026-10-11):** Azahar applies `romfs_ext/` patches only to
files at the RomFS root, so through `load/mods` the `.code`, `Battle.cro` and `DllBattle.cro` features work
but the nested `a/1/4/1`, `a/1/7/0` and `a/0/3/2` patches (Battle Tree / Maison ban lists and clauses, the
USUM descriptions) are silently skipped. The complete patches and the Python tools are not affected.
**Correction (2026-10-14):** the Black 2 / White 2 Arceus form-typing appended 16 personal rows but no
learnset (`a/0/1/8`) or evolution (`a/0/1/9`) rows for them, so levelling up an Arceus holding a plate
crashed the game ("An error has occurred"). `gen45_nds_arceus_typefix/bw2_arceus_typefix.py` now adds those
rows, and the `gen5_bw2/` xdeltas and `gen5_bw2/bps/` files were rebuilt; the new target hashes are in
`gen5_bw2/bps/SOURCES.md`. The `gen5_bw2/live/` ban-list patches do not touch these files and are unchanged.

## Live (in-place) patches (added 2026-09)

Some of the un-nerfs are same-size, in-place edits and can be applied to a game that is already running; others are rebuilt ROMs and cannot. Each folder now says which: `gen5_bw2/live/` holds the Black 2 / White 2 ban-list + clause removal as length-preserving IPS32 and BPS patches (the Arceus and Pokéstar parts stay whole-ROM), and `gen3_emerald/LIVE.md`, `gen4_platinum/LIVE.md`, `gen6_oras/azahar-mods/LIVE.md`, `gen7_usum/azahar-mods/LIVE.md` state what is live-safe there and what a running game would see. No new features; the same edits, classified.
