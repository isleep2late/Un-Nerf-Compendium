# BDSP Battle Tower — Full No-Restrictions Patch (Species + Item Clause + Legendary Ban)

Pokémon **Brilliant Diamond / Shining Pearl** (Switch, Unity IL2CPP), game version **1.3.0 (v393216)**.

This removes **all three** Battle Tower team-registration restrictions:

- **Species clause** — register more than one of the same Pokémon.
- **Item clause** — register more than one of the same held item.
- **Legendary ban** — register the normally-banned box legendaries / mythicals.

It's a small, surgical **exefs code patch** (8 four-byte edits per game). The Lv.50 cap and every other
check are left untouched. Works on emulator (Ryujinx / Eden / Sudachi / Citron / Yuzu-lineage) and real
hardware (Atmosphère LayeredFS).

---

## What's in here

```
NoTowerClause_BrilliantDiamond/exefs/
    94CEAE325C205C4B9D6F7235552F28FD.ips   (IPS32, 8 records)
    noclause.pchtxt                        (IPSwitch pchtxt, self-IDs via @nsobid)
NoTowerClause_ShiningPearl/exefs/
    38F59CBDA2EB9C44B72F94C4D25935A2.ips
    noclause.pchtxt
bdsp_tower_clause_repatch.py               (regenerate for any game version)
re_tools/                                  (RE tooling: NSP/NSO extractors, disasm, clean masterdatas)
SaveData_ShiningPearl_converted.bin        (helper: a BD save converted to SP, see "Saves" below)
```

| Game | Title ID | Build ID (1.3.0) |
|---|---|---|
| Brilliant Diamond | `0100000011D90000` | `94CEAE325C205C4B9D6F7235552F28FD` |
| Shining Pearl | `010018E011D92000` | `38F59CBDA2EB9C44B72F94C4D25935A2` |

> Diamond and Pearl are **separate executables** — use the file for the game you're actually running.
> The patch is keyed to the 1.3.0 Build ID; if yours differs, regenerate (see below).

---

## Install

**Emulator (Ryujinx / Eden / etc.)**

Drop the game's folder into the mod directory and enable it:

```
<emulator>/load/<TitleID>/NoTowerClause/exefs/<BuildID>.ips
```

- Ryujinx/Yuzu desktop: right-click game → *Open Mods Directory*.
- Eden (Android): *Add-Ons → Install* → point at the `NoTowerClause_<Game>` folder (the one that
  contains `exefs`), then toggle it on.
- The `.ips` filename **must equal the Build ID**. Alternatively use `noclause.pchtxt`, which carries
  `@nsobid-<BuildID>` and self-matches.

**Real hardware (Atmosphère)**

```
sd:/atmosphere/contents/<TitleID>/exefs/<BuildID>.ips
```

---

## How it works (RE summary)

The clause/ban logic lives in **`Dpr.UI.UIBattleMatchingTeamSelect`** (the team-select UI the Tower
uses), methods `GetRegulations` / `GetRegulation` (three overloads). Per party member each method:

- reads a regulation flag byte: `[reg+0x2E]` = "no same Pokémon", `[reg+0x2F]` = "no same item";
- if set, calls a duplicate-finder whose `bool` (bit0 = a duplicate exists) is tested by the next
  `tbz/tbnz w0`;
- separately calls `Dpr.PokeRegulation.CheckLegend(species)` (`bool`, true = banned legendary).

Each relevant `bl <check>` is replaced with **`mov w0,#0`** (00 00 80 52) → "no violation / not banned".
8 sites: 3 species + 3 item + 2 legend.

The banned-species list itself is data — `int32[18]` in `global-metadata.dat` (file offset `0x666A32`,
the `<PrivateImplementationDetails>` "4DBCB2BA…" blob): dex 150,151,249–251,382–386,483,484,487,489–493.
You can clear that instead/as well, but this patch handles the ban in code so no romfs edit is needed.

**Dead ends (so you don't chase them):** `PokeRegulation.CheckBothPoke/CheckBothItem` have zero callers;
`PokeDupeChecker.*` is the anti-clone box flagger; `EvCmd_BTWR_SUB_CHK_ENTRY_POKE` is a `return 1` stub.

**Soul Dew is NOT banned** in the BDSP Tower — the regulation has no banned-item list, and Latias/Latios
aren't in the species list. (Older-gen "nullify" behavior is a separate engine matter, not a ban.)

---

## Regenerate for another version

```
pip install capstone
python3 bdsp_tower_clause_repatch.py global-metadata.dat main_decompressed.bin <YOUR_BUILDID>
```

Inputs: your game's `global-metadata.dat` (romfs `Data/Managed/Metadata`, base-resident; any dump works)
and the **decompressed** `main` (LZ4-decompress the NSO0 segments to their mem offsets; text base 0 —
see `re_tools/nso_decompress.py`). It auto-locates the methods and reports the (should be 6) clause
sites. Legend sites are at the two `bl CheckLegend` calls inside `GetRegulations`/`GetRegulation`.

---

## Saves (important gotcha)

BDSP `SaveData.bin` (PKHeX `SAV8BS`):

- Size `0xEF0A4` for 1.3.0; first 4 bytes = revision (`0x34` = 1.3.0).
- A **byte at MyStatus+0x2B (file `0x79BDF`) is the version: `0`=Diamond, `1`=Pearl**.
- A 16-byte **MD5 integrity hash at `0xE9818`** over the whole file (the 16 bytes are zeroed, then the
  entire file is MD5'd, then the result is written back). The game rejects a save whose hash is wrong.

**Loading a Diamond save in Pearl (or vice-versa) = black screen on load.** If you convert a save between
versions, flip the version byte and recompute the MD5 (`SaveData_ShiningPearl_converted.bin` here is a
BD→SP example). Emulator save export/import wraps it as a zip with the single entry
`<TitleID>/SaveData.bin` (DEFLATE).

---

## Credits

Banlist research building on isleep2late / ABZB (ProjectPokemon thread 60041). Clause + legend code
patches reverse-engineered from the 1.3.0 binaries. Single-player / legally-dumped use.
