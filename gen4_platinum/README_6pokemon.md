# Platinum — 6-Pokémon Battle Tower (singles)  [SOURCE PATCH — build required]

`platinum_6pokemon_singles.patch` makes the player select and field **6 Pokémon** in Battle Tower
Singles, against the pret [`pokeplatinum`](https://github.com/pret/pokeplatinum) decomp.

> **This is a source patch, not a byte patch.** It widens fixed arrays in the runtime `BattleTower`
> struct (`unk_2A/2E/36[4]→[6]`), which changes the struct layout — so it **must be recompiled** and
> **cannot be applied to an already-built `.nds`** (the field offsets are baked into every compiled
> access). Build it with the pokeplatinum toolchain (`mwccarm` + a NitroSDK), the same setup as the
> Arceus Platinum patch.

## What it changes
- `BattleTower` struct: `unk_2A/2E/36[4]→[6]` (the player's selected-team slot/species/item).
- `BattleTower_GetPartySizeForChallengeMode`: singles returns **6** (sets `battleTower->partySize`).
- The `unk_2A` init loop `<4 → <6`.
- Opponent generator `BattleTower_CreateRandomTrainerParty`: the `GF_ASSERT(partySize <= 4)` becomes a
  **clamp** to 4, so the opponent's fixed `[4]` buffers never overflow — you bring 6, the AI brings ≤4.
- Team-select party menu: `min/maxSelectionSlots 3→6` and the selection-order copy loop `<3→<6`.

## ⚠️ Build-time items to verify (I could not compile-test this — no DS toolchain in my environment)
1. **`param0->unk_06[]` size** in `unk_0205003C.c` (the selection-order carrier). The copy loop now
   reads indices 0–5; if that array is declared `[3]`/`[4]`, widen it too (and any sibling in the
   selection-result struct) or it reads out of bounds.
2. **Which selection path the Tower uses.** Some facilities take the **regulation** path
   (`unk_0205A0D8.c` → `BattleRegulation_GetRuleValue(TEAM_SIZE)`); if Singles uses it, also set the
   relevant `.teamSize = 3 → 6` entries in `src/battle_regulation.c` (ROM data, no save impact).
3. Confirm the **save** still fits (`pokeplatinum` will error at build if a save struct overflows, like
   pokeemerald did). The widened arrays here are in the **runtime** `BattleTower` (heap), not the save
   block, so this should be fine — but verify.
4. In-game: enter Tower Singles with 6, confirm no crash on team-confirm and that all 6 battle.

The opponent staying ≤4 avoids the opponent-buffer overflow without resizing the trainer-record
structs (which would bloat the save). This mirrors the verified gen-3 approach (player 6 vs AI fewer).
