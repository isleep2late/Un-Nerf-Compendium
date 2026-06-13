# Emerald — 6-Pokémon Battle Tower (singles)

`emerald_6pokemon_singles.patch` lets you select and field **6 Pokémon** in the Battle Tower
**Singles** challenge (instead of 3), against the pret [`pokeemerald`](https://github.com/pret/pokeemerald)
decomp. Built and verified to compile + fit the save; **needs an in-game test** (battle a Tower
singles run with a 6-mon team).

## Why it's a source patch (not a byte patch)
The player's chosen team is held in fixed arrays sized by `MAX_FRONTIER_PARTY_SIZE`, and that same
constant sizes the **saved Battle Tower records** (`EmeraldBattleTowerRecord.party[]`, several of them
in the save). Bumping it to 6 overflows `SaveBlock2` (pokeemerald's `STATIC_ASSERT` catches it). So
this introduces a **separate** constant for just the player's selection and leaves the records at 4:

- `FRONTIER_SELECT_SIZE = 6` (new) — sizes `gSelectedOrderFromParty[]` (EWRAM) and
  `selectedPartyMons[]` (save, +4 bytes only, fits) and the loops that fill/read them
  (`frontier_util.c`, `ReducePlayerPartyToSelectedMons`).
- `MAX_FRONTIER_PARTY_SIZE` stays 4 → saved records unchanged, save fits.
- Battle Tower Singles lobby script sets the selection count to 6 (`VAR_0x8005`), and the singles
  eligibility (`CheckPartyIneligibility`) requires 6.
- The **opponent** stays 3 (`FillFrontierTrainerParty(FRONTIER_PARTY_SIZE)`), so you field 6 vs 3.
  (The opponent generator `gFrontierTempParty[0..3]` is hardwired to ≤4, so leaving it at 3 avoids any
  overflow.)

## Build
```sh
git clone https://github.com/pret/pokeemerald && cd pokeemerald
# set up agbcc (or a modern arm-none-eabi toolchain) per pokeemerald INSTALL.md
git apply ../emerald_6pokemon_singles.patch
make            # -> pokeemerald.gba  (a clean Emerald + 6-mon Tower singles)
```
A pre-built ROM is in `patched_roms/Emerald_6Pokemon_BattleTower.gba`.

## Notes / to verify in-game
- Only **Battle Tower Singles** is bumped to 6 here. Doubles/Multi and the other facilities (Palace,
  Arena, Pike, Pyramid, Factory, Dome) are untouched (each has its own lobby `setvar VAR_0x8005`); the
  same one-line change extends them, but some facilities have 3-mon-specific UI/logic worth testing
  before enabling.
- You must have **6 eligible** mons to enter (min == max == 6, mirroring vanilla's "exactly 3").
- Combining with your existing unnerf: apply this patch **on top of your pokeemerald fork that has the
  ban-list / clause / Soul Dew / any-ability source changes**, then build once for a single ROM. (The
  clause removal lives in the same `CheckPartyIneligibility` this patch edits, so they compose cleanly.)
