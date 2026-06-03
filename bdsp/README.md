# BDSP - Battle Tower No Ban List (LayeredFS mod)

BDSP stores its Battle Tower banned-Pokemon list as the **same Gen-7 ban bit-string** used in
Sun/Moon/USUM and ORAS, kept as a constant byte array inside the IL2CPP **`global-metadata.dat`**.
This tool zeroes those ban bytes (size unchanged) and packages the result as a LayeredFS romfs mod
- exactly the edit isleep2late/ABZB documented (originally done by hand in Imposter's Ordeal), but
one-click and without needing the GUI tool.

## Requirements
- **Python 3** (pure stdlib).
- Your own **`global-metadata.dat`**, dumped from your legally-owned BDSP cartridge/eShop title.
  In the game's RomFS it's at `Data/Managed/Metadata/global-metadata.dat`.
- A Switch emulator (Ryujinx / Yuzu) or CFW that supports LayeredFS mods.

## Use
1. Drag your `global-metadata.dat` onto **`apply_bdsp_nobanlist.bat`** (or run
   `python bdsp_nobanlist.py global-metadata.dat`).
2. It writes a mod folder: `BDSP_NoBanList_Mod\romfs\Data\Managed\Metadata\global-metadata.dat`.
3. Copy `BDSP_NoBanList_Mod` into your emulator's mod path:
   - **Ryujinx:** `mods\contents\<TitleID>\`
   - **Yuzu:** `load\<TitleID>\`
   - Title IDs: **Brilliant Diamond `0100000011D90000`**, **Shining Pearl `010018E011D92000`**.
4. Enable the mod in the emulator and start a fresh Battle Tower challenge.

## Scope / notes
- Removes the **banned-Pokemon list** for the Battle Tower (Single + Double, incl. Master Class).
- **Does NOT** remove the **species clause** or **item clause** - that part is still in progress
  (the 0E/0F flags sit near the banlist but zeroing them errors out; same open problem the
  research notes).
- The ban-record signature is the verified Gen-7 string; the tool reports how many records it
  cleared. Test in a fresh challenge.
