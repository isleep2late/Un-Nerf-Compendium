# PKHaX — save editor add-ons (Gen 1 sprite/typing + Gen 3 any-ability)

PKHeX, rebuilt as **PKHaX**, with two hackmons features for this compendium's projects:

- **Gen 1 (RBY):** give any Pokémon any **sprite** (desynced from its real species) and any
  **typing** (mono/dual) — the PikaSav feature, now native to PKHeX and round-trip safe.
- **Gen 3 (RSE/FRLG):** **any ability on any Pokémon** (pairs with the Emerald un-nerf ROM patch).

## What's in this folder
- `PKHaX_win-x64_net10.zip` — ready-to-run build. Unzip and run **`PKHaX.exe`**. The name ending in
  `HaX` turns on illegal-edit mode (title bar shows "PKHaX"). Requires the **.NET 10 Desktop
  Runtime** (https://dotnet.microsoft.com/download/dotnet/10.0). Do not rename the DLLs.
- `PKHEX_PIKASAV_CHANGES.md` — full documentation of every code change and why.
- `patched_files/` — the 9 changed/new source files in repo layout, plus `PLACEMENT.md` saying
  exactly where each goes. This is what you hand to Claude Code.
- `CLAUDE_CODE_PROMPT.md` — a paste-ready prompt that has Claude Code add an `upstream` remote, re-apply
  these patches onto the latest PKHeX, rebuild as PKHaX, and update the repo README.

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

## To put the full source on GitHub as a sub-directory of this repo
Your modified PKHeX source tree is in `PKHeX-master/` (every change tagged `// PKHaX`). To add it as
a subdirectory of the compendium:
```
cp -r /path/to/PKHeX-master  Un-Nerf-Compendium/PKHaX/source
cd Un-Nerf-Compendium
git add PKHaX
git commit -m "Add PKHaX (PKHeX + Gen1 sprite/typing + Gen3 any-ability)"
git push
```
(Or keep the source as its own repo and reference it; the `patched_files/` bundle + Claude Code
prompt are enough to reproduce the build against upstream at any time.)
