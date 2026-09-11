# PKHaX — save editor add-ons (Gen 1 sprite/typing + Gen 3 any-ability)

PKHeX, rebuilt as **PKHaX**, with three hackmons features for this compendium's projects:

- **Gen 1 (RBY):** give any Pokémon any **sprite** (desynced from its real species) and any
  **typing** (mono/dual) — the PikaSav feature, now native to PKHeX and round-trip safe.
- **Gen 3 (RSE/FRLG):** **any ability on any Pokémon** (pairs with the Emerald un-nerf ROM patch).
- **Gen 3 Deoxys forms:** select any of the four forms (Normal/Attack/Defense/Speed) with the
  correct per-form base stats and sprite (stored in PK3 `0x1F`; pairs with the Emerald engine patch).
- **Gen 1/2 "No Move":** the glitch move ID `0x00` as a selectable entry in the move dropdowns
  for Gen 1 and Gen 2 saves — distinct from `(None)`. In Gen 1 it is Fissure's animation, 102
  power, glitch type, 81/256 accuracy (Yellow); in Gen 2 it is a Rapid Spin animation, glitch
  type, ~20% accuracy Toxic-effect move (power 5 in Gold/Silver, 9 in Crystal). It writes the raw `0x00` move byte with your chosen PP kept intact
  (slots holding it are not compacted away like empty slots), so the save loads on real hardware
  with the corrupted move selectable from the FIGHT menu.
  **It must be in move slot 1 to be usable in-game:** the Gen 1 FIGHT menu treats a `0x00` move
  byte as the end of the move list, so a No Move in slots 2-4 is unreachable (and hides every
  move after it). With it in slot 1 all four menu rows display as `-` and only the first row
  (No Move itself) is selectable; moves in slots 2-4 are hidden while it is equipped. If the
  move hits without KOing the target, the game may freeze (its garbage effect byte jumps into
  Echo RAM) — save first.
- **Gen 1 save corrupter (Any% 2-swap):** `Tools > Corrupt Gen 1 save (Any% 2-swap)...` writes a
  timestamped backup, then patches the file exactly as it is on disk (byte-identical to the standalone
  `corrupt-save-gen1.py`): party count 255, valid checksum — the starting point of the Any% 2-swap route.
  The Trainer ID is never touched; unsaved editor changes are not included (export first). Desktop,
  Android and iOS. See below.

Built on **upstream PKHeX `master` @ `77dcd3a78` (2026-09-10)**. Every PKHaX edit is tagged with
a `// PKHaX` comment, so `grep -r "// PKHaX"` lists every change.

## What's in this folder
- `PKHaX_win-x64_net10.zip` — ready-to-run build. Unzip and run **`PKHaX.exe`**. The name ending in
  `HaX` turns on illegal-edit mode (title bar shows "PKHaX"). Requires the **.NET 10 Desktop
  Runtime** (https://dotnet.microsoft.com/download/dotnet/10.0). Do not rename the DLLs.
- `PKHEX_PIKASAV_CHANGES.md` — full documentation of every code change and why.
- Full modified PKHeX source tree — every change is tagged with a `// PKHaX` comment.
  Build with `build_pkhax.bat` (Windows) or `build_pkhax.sh`.

## Using it
1. Unzip, run `PKHaX.exe`, open your save (`File > Open`, or drag it in).
2. **Gen 1:** click a Pokémon. On the main tab (under Catch Rate) you'll see **Sprite**, **Type 1**,
   **Type 2** drop-downs. Set the sprite to any species/glitch index and the two types freely
   (equal = mono, different = dual). Hovering a box slot shows the data species, the sprite, and the
   exact typing. Set, then save.
3. **Gen 3:** the Ability drop-down lists every ability; pick any one and save.

## Quick facts
- Gen-1 idea & data layout credit: **PikaSav**. Gen-1 type bytes use Gen-1's own values (not modern
  type indices).
- Known limit: the Gen-1 sprite desync is stored in the save's list header, so it persists in-save
  but not across single `.pk1` export/import (same as PikaSav).

## Gen 1 save corrupter (Any% 2-swap)
`Tools > Corrupt Gen 1 save (Any% 2-swap)...` (enabled when a Red/Blue/Yellow save that lives as a plain
raw 32 KiB file on disk is loaded — not one opened out of an emulator save state). It does exactly what the
standalone `corrupt-save-gen1.py` does, byte for byte, **to the file as it is on disk**, on both the
international and the Japanese layout:

| | International | Japanese |
|---|---|---|
| Party count byte (`SAV1Offsets.Party`) → `0xFF`, and the species-list terminator right after it → `0xFF` | `0x2F2C` / `0x2F2D` | `0x2ED5` / `0x2ED6` |
| Checksum byte (`SAV1Offsets.ChecksumOfs`) = bitwise NOT of the byte sum over `[OT, ChecksumOfs)` | `0x3523` (sum over `0x2598..0x3522`) | `0x3594` (sum over `0x2598..0x3593`) |
| Trainer ID (`SAV1Offsets.TID16`) — **never modified** | `0x2605` | `0x25FB` |

- **What gets corrupted is the file exactly as it is on disk** (desktop) / **exactly as it was opened or last
  saved back** (mobile). The bytes are read from the file, the three bytes above are patched, nothing else is
  touched — byte-identical to running `corrupt-save-gen1.py` on the same file (measured with `cmp` on a real
  Blue and a real Yellow save: identical output, identical backups).
- **Unsaved editor changes are NOT included.** The editor's in-memory save is never serialised by the
  corrupter. If the editor has unsaved edits the confirmation prompt says so in plain words; cancel, export
  the save first (`File > Export SAV...` on desktop, "Save changes to file" on mobile), then corrupt.
  Why: `SaveFile.Write()` re-packs the party and box lists and rewrites the checksum, and its output is not
  byte-identical to the file even for an unedited save (measured: 6 bytes on a real Blue save, 112 on a real
  Yellow save, and two consecutive `Write()` calls on that Yellow save differ by 10,922 bytes). Corrupting a
  re-serialised save would therefore not be "the file with three bytes changed"; corrupting the disk bytes is.
- **Backup first, always.** Before anything is written, the file on disk is copied to
  `<file>.bak-YYYYmmdd-HHMMSS` (desktop; never overwrites an existing backup). If the copy fails, nothing is
  corrupted. On mobile the bytes exactly as opened are written to the app's `backups/` folder and offered
  through the share sheet.
- **Sanity check:** the Trainer ID in the file on disk must equal the Trainer ID of the loaded save; if the
  file has been replaced since it was opened, the corrupter refuses and changes nothing.
- **The corrupted file is the Any% 2-swap starting point.** In game: CONTINUE, then START > POKEMON,
  never move the cursor past index 54, swap slot 7 with 21, then 20 with 22, mash B.
- **TID high byte `$40` is needed for the route — the corrupter does not set it.** Set the Trainer ID
  on the Trainer Info tab, export the save, and only then corrupt; the corrupter leaves whatever is on disk.
- **PKHaX cannot reopen the corrupted file** (`SaveUtil` rejects a party count above 20/30 as "not a
  save"), so after corrupting, the editor keeps the pre-corruption save loaded; the backup is the file
  to reopen for further editing.
- Yellow needs no special handling: the Gen 1 layout is identical across Red, Blue and Yellow at every
  offset touched (verified on a real Yellow save — stored checksum `0x39` matched the computed one, and a
  full run took party 1 → 255 with a valid checksum). The only gate is the 32 KiB size.
- Implementation: `PKHeX.Core/Saves/Util/Gen1SaveCorrupter.cs` — `ApplyInPlace(raw, offsets)` is the
  whole corruption; `CorruptFileOnDisk(path, offsets, expectedTID16)` is what the desktop menu item calls
  (read, check, backup, patch, write; returns the backup path); mobile calls `ApplyInPlace` on its private
  copy of the opened bytes. `IsApplied` / `IsChecksumValid` for checks; the checksum routine is
  `SAV1.GetRBYChecksum`, shared with `SAV1` itself. There is deliberately no "corrupt the `SaveFile`
  object" API. Tests: `Tests/PKHeX.Core.Tests/Saves/Gen1SaveCorrupterTests.cs` (the expected bytes are
  computed in the test from the constants above, independently of the corrupter; includes a regression test
  showing `Write()` re-serialisation differs from the disk bytes while `ApplyInPlace` changes exactly three, and
  one for the mobile contract: a copy taken before `SaveUtil.GetSaveFile` stays equal to the opened bytes after
  OT/money/party edits and a `Write()`, which all land in the aliased array).

## Re-basing onto a newer upstream PKHeX
This tree carries `upstream` → `https://github.com/kwsch/PKHeX`, but it shares no git history with
upstream, so `git merge upstream/master` does not work. The working method is a recorded-base
diff-apply: take the base commit recorded in the "Built on" line above, then

```
git fetch upstream
git diff --binary <recorded-base>..upstream/master | git apply --3way --directory=PKHaX
bash build_pkhax.sh            # rebuild; produces PKHaX.exe
```

**`--binary` is not optional.** A plain `git diff` describes a binary file only as
`Bin 3410 -> 3472 bytes` with no blob data, so `git apply` refuses it with *"cannot apply binary
patch ... without full index line"* and changes NOTHING. `git apply` does return 1, but the failure
is silent in EFFECT: the tree is untouched, so the `// PKHaX` tag count still matches, the tests
still pass, and the sync looks complete when nothing happened. The 2026-08-29 sync
(`74b88906e` -> `e15d2467b`) was **entirely** two `.pkl` resources, so without `--binary` it would
have applied nothing at all. Always confirm the sync moved something:

```
git status --porcelain          # must list the files the upstream diff claimed
```

A second trap worth knowing: changing a `.pkl` resource under an incremental build can leave the
test assembly stale, which produced one non-reproducing `SimulatorGetEncounters` failure here.
If a test fails right after a resource sync, delete `Tests/PKHeX.Core.Tests/{bin,obj}` and re-run
before believing it.

Afterwards verify every `// PKHaX` tag survived (`git grep -c "// PKHaX" -- '*.cs'` before and
after should match), run the Core tests, and update the "Built on" line above to the new upstream
commit — future syncs diff from whatever is recorded there.
