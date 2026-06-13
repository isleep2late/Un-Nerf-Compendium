# Changelog — v2 (2026-06)

Reverse-engineered and verified against retail USUM/ORAS dumps (capstone/unicorn); all originals
byte-checked. Drop-in for the existing repo.

## `gen67_formepersist/formepersist.py` — fixed + extended
- **Sister-game tables added.** The old `--full` table matched only **Alpha Sapphire** and
  **Ultra Moon**; **Omega Ruby** and **Ultra Sun** silently got only the auto-located Mega/Primal
  subset (Hoopa/Furfrou/Shaymin reverts were left in). v2 ships **verified revert tables for all
  four** games (OR = AS+8, US = UM−4), keyed by Title ID and re-validated instruction-by-instruction
  (sisters share `ChangeFormNo`, so the table is confirmed against the actual code before use).
- **Hoopa destructive-reset fix (auto-located, all four games).** Hoopa's Unbound→Confined revert
  is not a single `ChangeFormNo` — it's a block of `ChangeFormNo(base)` + 1–2 adjacent `CoreParam`
  "extra reset" setters, and it appears in several out-of-battle contexts (party load, PC/box,
  field). The old per-game table **missed these entirely on OR/AS** (so ORAS Hoopa never persisted —
  it reverted on load) and missed one of two blocks on US/UM. `--full` now **auto-detects** every
  such block by signature (`find_forme_destructive_resets`) and NOPs all of its calls — no hardcoded
  addresses, version-independent. Verified: AS/OR need **11** Hoopa NOPs each (4 blocks), US/UM need
  the remaining **2** (the other block was already covered).
- **Works on already-patched builds.** If the Mega/Primal loop is already NOP'd (auto-locate
  signature gone), the script recovers `ChangeFormNo` from the Title ID — so you can apply just the
  Hoopa fix to an older build without re-patching everything.
- Verified Title IDs: OR `000400000011C400`, AS `000400000011C500`, US `00040000001B5000`,
  UM `00040000001B5100`.

## `gen67_arceus_typefix/` — NEW
- `arceus_silvally_typefix.py` gives **Arceus (#493)** and **Silvally (#773)** real per-form types
  so a PKHeX-set form behaves as that type with **no Plate/Memory held**. Root cause: both ship
  `FormStatsIndex = 0` (no per-form personal entries → every form is Normal/Normal). Form index ==
  gen-6/7 type id, so the fix is an identity map. Rebuilds personal GARC `a/0/1/7`; all original
  entries byte-identical, +17 typed leaf entries per species. Length-changing → repack via a normal
  RomFS rebuild (pk3DS/pkNX). Works OR/AS + US/UM. See its README.

## Not yet shipped (needs a live trace)
- **Protean / Color Change / Soak etc. blocked on Arceus / Kecleon / Silvally.** This is a Gen 6/7
  type-change guard keyed on the type-defining abilities (Multitype 0x79 / RKS System 0xEB / Color
  Change 0x10). It lives in Battle.cro's data-driven effect dispatch and is **not** an inline
  species/ability compare (confirmed: the species don't form a table; the ability constants aren't
  in an adjacent cmp chain). Pinning the exact branch needs a GDB trace on Azahar (same method that
  cracked Prankster), then a 1–2 byte NOP. Tracked as an open item.

## `gen45_nds_arceus_typefix/` — NEW (gen 4/5 NDS)
- `bw2_arceus_typefix.py` — Black2/White2 Arceus form-driven typing (data-only; BW2 personal has
  FormStatsIndex). Builds patched `.nds` via ndspy. Verified.
- Gen 4 Platinum spec (data + arm9 `Pokemon_GetFormNarcIndex` Arceus case) documented in the README.

## Hoopa fix superseded the v2 hardcoded approach
- `formepersist.py` now auto-detects ALL Hoopa destructive-reset blocks (`find_forme_destructive_resets`);
  the earlier hardcoded USUM pair is removed. ORAS Hoopa (4 blocks / 11 NOPs each game) was entirely
  unpatched by the original tables and is now covered. AS/OR/UM/US all build clean.
