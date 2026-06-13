# Platinum — Full Hackmons, all Battle Tower modes  (BUILT)

`platinum_fullhackmons_allmodes.patch` is the **complete** Platinum un-nerf against pret
[`pokeplatinum`](https://github.com/pret/pokeplatinum). It supersedes the separate
`platinum_arceus_formtype.patch` + `platinum_6pokemon_singles.patch` (it contains both, plus the
all-mode unlock). Built and **compile-verified** here; a ready ROM is in
`patched_roms/Platinum_FullHackmons_AllModes_d2e19d62.nds`.

> Source patch, not a byte patch — it resizes runtime `BattleTower` arrays, so it must be recompiled
> (`mwccarm` + NitroSDK). A clean pokeplatinum builds **byte-identical** to retail (sha1 `0862ec35…`),
> confirming the toolchain; this patch then layers on top.

## What it does — applied to **singles, doubles, multi, and multi-link**

| Restriction (vanilla) | After patch |
|---|---|
| Frontier ban-list (legendaries/mythicals) | **Removed** — `Pokemon_IsOnBattleFrontierBanlist` → always `FALSE`, so every facility's per-mon check passes |
| Species clause (no duplicate species) | **Removed** — neutered in the selection menu (`CheckDuplicateValues`), the registered re-check (`BattleTower_CheckDuplicateSpeciesAndHeldItems`), and the central `BattleRegulation_ValidatePartySelection` |
| Item clause (no duplicate held items) | **Removed** — same three sites |
| Total-level cap | **Removed** — `MAX_TOTAL_LEVEL` rule forced to 0 in the validator |
| Soul Dew nerfed inside the Frontier | **Un-nerfed** — dropped the `BATTLE_TYPE_FRONTIER` guard on both Latios/Latias `HOLD_EFFECT_LATI_SPECIAL` branches in `battle_lib.c` |
| Bring exactly 3 (singles) | **1–6**, your choice |
| Bring exactly 4 (doubles) | **1–6** (you bring up to 6; the **opponent** is clamped to 4 so its fixed buffers never overflow) |
| Multi / multi-link fixed at 2 | **Min lowered to 1** (max stays 2 per trainer — the partner fills the rest; 2-per-player is architectural) |
| Arceus reverts to Normal | **Permanent form-typing** (the Arceus patch, included) |

### How the flexible count is made safe
`BattleTower_GetPartySizeForChallengeMode` sets a *max* per mode, but the populate loop
(`sub_0204A378`) now **stops at the first unpicked slot and sets `partySize` to the real count**, so
bringing fewer than the max never reads garbage, and the opponent generator matches your actual count.
The clause re-check arrays were widened `[4]→[6]` to be overflow-safe at 6 mons (and the clause itself
is disabled).

## Build
```sh
git clone https://github.com/pret/pokeplatinum && cd pokeplatinum
./tools/devtools/get_metroskrew.sh   # portable mwccarm + clean-room NitroSDK
git apply ../platinum_fullhackmons_allmodes.patch
meson setup build && ninja -C build  # -> build/pokeplatinum.us.nds
```

## ⚠️ Needs your in-game test (I can't boot an NDS here)
Compile-verified only. Like the Emerald hackmons build, runtime behavior should be confirmed:
1. **Singles** — bring 6 (and try 2) legendaries with duplicate species/items; all should register and
   all should appear in battle.
2. **Doubles** — bring 6 of the same uber; confirm it accepts and the opponent shows up.
3. **Multi / multi-link** — confirm you can submit 1 (down from the forced 2).
4. **Soul Dew** — Latios/Latias damage should be boosted *inside* the Tower now.

Report any mode that misbehaves and I'll rebuild — the toolchain is hot, so each fix is one build.
Note: this is **clean Platinum + everything above**; it is not layered on your binary-patched `.nds`
(a recompiled ROM can't take the xdelta) — this *is* the replacement for it, built from source.
