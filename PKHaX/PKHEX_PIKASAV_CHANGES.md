# PKHaX — PikaSav (Gen 1) + Any-Ability (Gen 3) add-ons for PKHeX

This is PKHeX with two hackmons features added, intended to be run as **PKHaX** (rename the
executable so its name ends in `HaX` to enable PKHeX's illegal-edit mode — the title bar then
reads "PKHaX"). It is a standard framework-dependent build; it needs the **.NET 10 Desktop
Runtime** installed.

Two features:

1. **Gen 1 (Red/Blue/Yellow): any sprite + any typing** — the PikaSav feature set. A Pokémon's
   on-screen **sprite** can be desynced from its actual species, and **Type 1 / Type 2** can be set
   to anything (mono = both equal, dual = different).
2. **Gen 3 (Ruby/Sapphire/Emerald/FR/LG): any ability on any Pokémon** — the same engine pairing
   used by the Emerald un-nerf patch.

---

## 1. Gen 1 sprite desync + arbitrary typing

### Why it needs a code change
In Gen 1 the species is stored **twice**: once in the box/party **list header** (one byte per slot,
this is what the game draws the **sprite** from) and once inside the **data structure** (the
"real" species used for stats/battle). PikaSav lets you set these two independently, and also lets
you write the per-Pokémon **Type1/Type2** bytes (data offsets 0x05/0x06) freely. Vanilla PKHeX
keeps the two species bytes equal and derives types from the species, so it cannot represent a
"looks-like-X, is-Y, typed-Z" Pokémon — and worse, **saving** such a file in stock PKHeX would
overwrite the desynced sprite. These changes make PKHeX preserve and edit all of it.

### Core changes
**`PKHeX.Core/PKM/PK1.cs`**
- New property `HeaderSpeciesInternal` (a field, not part of the 33-byte body): the raw Gen-1
  list/sprite index. `0` means "synced to the data species".
- Helpers `SpriteSpeciesInternal` (effective sprite index = header if set, else data species) and
  `IsSpriteDesynced`.
- `Clone()` copies `HeaderSpeciesInternal` so box-to-box moves keep the sprite.

**`PKHeX.Core/PKM/Shared/PokeList1.cs`**
- `ReadFromList(...)`: when parsing a slot, if the list header byte differs from the data-structure
  species, it is captured into `HeaderSpeciesInternal` (this is what makes PKHeX *accept and retain*
  PikaSav-edited saves instead of discarding the sprite).
- `GetHeaderIdentifierMark(pk)`: when writing a slot, it emits `SpriteSpeciesInternal` (the override
  if present, else the data species) so the desync round-trips byte-for-byte.

`Type1`/`Type2` already existed as raw byte accessors in `PK1`; no Core change was needed for typing
beyond surfacing them in the UI.

### UI changes
**`PKHeX.WinForms/Controls/PKM Editor/G1Editor.cs`** (new control) — three drop-downs:
- **Sprite**: all 256 Gen-1 indices (named where they map to a real species, "(glitch)" otherwise),
  so you can pick any sprite including MissingNo-style glitch sprites.
- **Type 1 / Type 2**: the 15 Gen-1 types by their correct Gen-1 byte values (which are *not* the
  same as PKHeX's modern type indices), plus a "mono/dual" indicator. Unknown/glitch type bytes
  already in a save are preserved.

**`PKHeX.WinForms/Controls/PKM Editor/PKMEditor.cs`** — creates the `G1Editor`, places it on the main
editor tab (row 17, mirroring the Catch Rate control), and shows it only for Gen-1 (`format == 1`).

**`PKHeX.WinForms/Controls/PKM Editor/EditPK1.cs`** — loads the control on populate and, on save,
writes the sprite + types **last** (after the species field, which otherwise resets types to the
species default — writing last is what lets custom types stick).

**`PKHeX.WinForms/Controls/Slots/SummaryPreviewer.cs`** + **`PokePreview.cs`** — the box **hover preview**
(the same panel that shows the moves/set) now includes a Gen-1 block for PK1 at the top: **Data species**,
**Sprite** (with "[desynced]" when applicable) and the exact **Type 1 / Type 2** (mono/dual). The info is
rendered *inside* the existing preview box — no extra tooltip popup is added (stock PKHeX shows no text
tooltip in preview mode). In the alternate text-hover mode the same block is prepended to the set text.

### Limitation
The sprite override lives in the save's list header, which is **not** part of the single-`.pk1`
export format. So the desync persists inside a save (and box-to-box within PKHaX) but resets if you
export a single Pokémon to `.pk1` and re-import it. This is the same limitation PikaSav has.

### Verified
Loading the supplied `Yellow.sav` (which contains, e.g., a Mewtwo that displays the Gyarados sprite
and is typed Water/Ghost), reading every party/box slot, then saving and reloading reproduces the
sprite desync and custom types **byte-for-byte**. A fresh edit (set sprite + types in the editor)
also persists through save/reload.

---

## 2. Gen 3 any-ability (unchanged from the Emerald un-nerf project)

**`PKHeX.Core/PKM/PK3.cs`** — `AbilityOverride` stored in the unused Sanity low byte `0x1E`
(outside the checksum): `0` = no override, `>=2` = force that ability ID.

**`PKHeX.Core/PKM/Shared/G3PKM.cs`** — the `Ability` getter returns the override for PK3, so
tooltips/summary/Showdown export all show the real ability.

**`EditPK3.cs` / `PKMEditor.cs`** — the Gen-3 Ability drop-down lists all abilities (not just the
two species slots); selecting one writes the slot bit when it matches a slot ability, otherwise the
`AbilityOverride` byte. This pairs with the Emerald `AnyAbility`/`Full` ROM patch (PKHaX writes
0x1E, the ROM reads it).

---

## Activating illegal mode
HaX mode turns on when the executable's filename ends with `HaX` (PKHeX checks `Environment.ProcessPath`).
The shipped build is therefore named **`PKHaX.exe`**; the apphost still loads `PKHeX.dll`, so do not
rename the DLLs. (You can also pass `HaX` as a command-line argument, or set Force HaX in settings.)
