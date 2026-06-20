# PKHaX change: Gen 1/2 status condition, level-255, and Showdown "desync" export/import

Builds on [LEVEL255_CHANGES.md](LEVEL255_CHANGES.md). Makes the Gen-1 "desync" hackmons attributes
(disguise **sprite**, custom **typing**, pre-applied **status**) and the **level (up to 255)** survive a
Showdown copy/paste round-trip, and surfaces status + level on the slot hover. Gen 2 carries **status +
level only** (it has no disguise/custom-typing — "no desync").

The text format is the canonical one shared with the PureHackmonsNoNerfs Showdown server
(`DESYNC-FORMAT.md` in that repo): three optional lines after the first/ability line —
```
Sprite: <Species>          (Gen 1 only)
Types: <Type1>[ / <Type2>] (Gen 1 only)
Status: <Burn|Paralysis|Sleep|Poison|Freeze>   (Gen 1 and Gen 2)
```
plus the standard `Level:` line carrying values up to 255.

All logic is gated on `PK1`/`PK2`/`GBPKM`, so no other generation changes.

## Files changed

- **`PKHeX.Core/Editing/BattleTemplate/GBHaxFormat.cs`** (new) — shared text helpers: Gen-1 type
  name⇄byte, status byte⇄canonical word (accepts the word or the 3-letter code on import).
- **`PKHeX.Core/Editing/BattleTemplate/Showdown/ShowdownSet.cs`**
  - New fields `PhSpriteSpecies`, `PhType1/2`, `PhStatusByte`.
  - PKM constructor: exports `Level = max(Stat_Level, CurrentLevel)` for GB mons (so 255 round-trips and
    box mons still show their real level); captures sprite/types/status into the new fields.
  - `ParseLine`: parses the `Sprite:`/`Types:`/`Status:` lines (before the generic tokenizer, so they
    never count as invalid lines).
  - `GetSetLines`: injects those lines on full-set exports only (guarded on the order containing
    `FirstLine`, which the hover order omits — keeps the hover free of duplicates).
  - `ParseLevel`: allows up to 255 (was capped at 100).
- **`PKHeX.Core/Editing/CommonEdits.cs`** — `ApplySetDetails`: clamps EXP at L100 but stamps
  `GBPKM.Stat_Level` with the requested level (255 support); applies the parsed sprite/types/status to
  PK1 (and status to PK2) **after** `ResetPartyStats()` (which clears status).
- **`PKHeX.WinForms/Controls/Slots/SummaryPreviewer.cs`** — the Gen-1 hover block now also shows
  `Status:`; added a Gen-2 block (status). Append helper now handles PK1 and PK2.
- **`PKHeX.WinForms/Controls/Slots/PokePreview.cs`** — preview box renders the Gen-2 block too.
- **`PKHeX.WinForms/Controls/PKM Editor/G1Editor.cs`** — the Gen-1 panel gained a **Status dropdown**
  (None/Sleep/Poison/Burn/Freeze/Paralysis) and now also serves **Gen 2** (status-only; sprite/type rows
  hidden). Wired into Gen-1 and Gen-2 load/save (`EditPK1.cs`, `EditPK2.cs`) and shown for format 1 and 2
  (`PKMEditor.cs`, with the label reading "Gen-1:"/"Gen-2:"). This is the dropdown that drives what the
  export and hover show.
- **`PKHeX.WinForms/Controls/PKM Editor/StatusConditionView.cs`** + `PKMEditor.cs` — the old status *icon*
  is now hidden for Gen 1/2 (the dropdown owns those) and kept only for Gen 3+, so there is exactly one
  status control per generation (no duplicate / out-of-sync editors).

Level itself shows on hover via the normal `Level:` line (now using the stored byte), so level 255 is
visible on hover for Gen 1 and Gen 2.

## Build
`dotnet publish PKHeX.WinForms/PKHeX.WinForms.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:EnableWindowsTargeting=true`
(needs the .NET 10 Desktop Runtime on the target Windows machine). Verified: PKHeX.Core builds clean and
all 31 Showdown/Simulator/BattleTemplate Core tests pass.
