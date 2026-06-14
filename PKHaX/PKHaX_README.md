# PKHaX — save editor add-ons (Gen 1 sprite/typing + Gen 3 any-ability)

PKHeX, rebuilt as **PKHaX**, with three hackmons features for this compendium's projects:

- **Gen 1 (RBY):** give any Pokémon any **sprite** (desynced from its real species) and any
  **typing** (mono/dual) — the PikaSav feature, now native to PKHeX and round-trip safe.
- **Gen 3 (RSE/FRLG):** **any ability on any Pokémon** (pairs with the Emerald un-nerf ROM patch).
- **Gen 3 Deoxys forms:** select any of the four forms (Normal/Attack/Defense/Speed) with the
  correct per-form base stats and sprite (stored in PK3 `0x1F`; pairs with the Emerald engine patch).

Built on **upstream PKHeX `master` @ `452ddcb` (version 26.05.05)**. Every PKHaX edit is tagged with
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

## Re-basing onto a newer upstream PKHeX
This tree carries `upstream` → `https://github.com/kwsch/PKHeX`. To pull in newer upstream changes:
```
git fetch upstream
git merge upstream/master      # keep both upstream's changes and our // PKHaX blocks
build_pkhax.bat                # rebuild; produces PKHaX.exe
```
Because every edit is tagged `// PKHaX`, conflicts are easy to resolve by keeping both sides.
