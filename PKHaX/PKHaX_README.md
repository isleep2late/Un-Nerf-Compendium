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

Built on **upstream PKHeX `master` @ `74b88906e` (2026-08-27)**. Every PKHaX edit is tagged with
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
This tree carries `upstream` → `https://github.com/kwsch/PKHeX`, but it shares no git history with
upstream, so `git merge upstream/master` does not work. The working method is a recorded-base
diff-apply: take the base commit recorded in the "Built on" line above, then

```
git fetch upstream
git diff <recorded-base>..upstream/master | git apply --3way --directory=PKHaX
bash build_pkhax.sh            # rebuild; produces PKHaX.exe
```

Afterwards verify every `// PKHaX` tag survived (`git grep -c "// PKHaX" -- '*.cs'` before and
after should match), run the Core tests, and update the "Built on" line above to the new upstream
commit — future syncs diff from whatever is recorded there.
