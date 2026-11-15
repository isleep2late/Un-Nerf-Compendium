**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**
# Emerald hack - Pokemon Emerald Seaglass (v3.0) [Emerald]: Battle Frontier entry rules removed

Status: **untested**. Built from static analysis only (reference builds of the hack's engine and a disassembly of the plain hack). Nobody has booted the patched game, entered a Battle Frontier challenge or fought a battle with it yet. Keep a backup of your save before you try it.

This is a patch **from the plain hack** (Pokemon Emerald Seaglass (v3.0) [Emerald], engine: pokeemerald-expansion 1.9.2 base, closed source) to an Un-Nerf build of the same hack. It is the Frontier-entry subset of the Emerald set in `gen3_emerald/` (the same rules `1_Emerald_FrontierUnlock_SoulDew.ips` removes from vanilla Emerald), placed at this hack's own code. Every change is in place: the RAM layout, the save layout and the ROM size are the plain hack's, so saves move both ways between the plain hack and this build.

## What it changes

- **Ban list emptied**: every Pokemon the hack bans from the Battle Frontier (its `isFrontierBanned` species flag) may enter.
- **Lv 50 limit removed** from the registration check, so a Pokemon above Lv 50 counts as eligible in Lv 50 mode.
- **Species Clause removed** from the registration check (two of the same Pokemon count as eligible).
- **Item Clause removed** from the registration check (two Pokemon holding the same item count as eligible).
- **Party menu**: the same-species and same-item refusals at Confirm are gone.
  In the party menu a Pokemon above the level cap can be entered (as in the vanilla Emerald set, this lifts the party menu's level cap in every mode that uses it, including the Union Room's Lv 30); banned Pokemon show as able because the ban flag itself is cleared.

Eggs, fainted Pokemon (in modes that check HP) and the Battle Pyramid's held-item rule are left as the hack has them.

## Files

| File | What |
|---|---|
| `Seaglass_UnNerf.ips` | the whole change as IPS (126 bytes, 19 records; does not check the input) |
| `bps/Seaglass_UnNerf.bps` | the same change as BPS (118 bytes; refuses an input whose CRC32 is not the plain hack's) |

## Input (exact), how to apply, output

| | File | Size | SHA-1 | CRC32 |
|---|---|---|---|---|
| input | `Pokemon Emerald Seaglass (v3.0) [Emerald].gba` (the plain hack, unpatched) | 16777216 | `b9f4d332d30fc88c379f9e037f9eae3b2755ead4` | `14465a33` |
| output | `Pokemon Emerald Seaglass (v3.0) [Emerald] [Un-Nerf UNTESTED].gba` | 16777216 | `dc45f347062bc897c864909e004299ce604f42b9` | `4c3aaae0` |

Check the input's SHA-1 first: any other version or build of the hack has other code addresses. Apply with Floating IPS:

```
flips --apply Seaglass_UnNerf.ips "Pokemon Emerald Seaglass (v3.0) [Emerald].gba" out.gba
flips --apply bps/Seaglass_UnNerf.bps "Pokemon Emerald Seaglass (v3.0) [Emerald].gba" out.gba
```

Lunar IPS, Rom Patcher JS or any IPS/BPS tool works the same way. The result must have the output SHA-1 above. The ROM size and the cartridge header are unchanged, so no header checksum fix is needed.

## Where (ROM offsets in the plain hack)

Code sites were found by masked signature (BL offsets masked, one hit) cut from a local build of pokeemerald-expansion 1.9.2 (each site first located in 1.9.2 from the 1.9.4 site list); each hit was checked for the same original bytes, the same branch-target code and the same fall-through code as the reference.

| Feature | Offset | Before -> after | Function | Change | Method |
|---|---|---|---|---|---|
| F2 | `0x144296` | `02 d9` -> `02 e0` | `AppendIfValid` | Lv50-mode level check: bls over the return made an unconditional b (the return for level > 50 is never taken) | sig |
| F3 | `0x1442BA` | `ed d1` -> `c0 46` | `AppendIfValid` | Species Clause (bne to the return after the duplicate-species loop) removed | sig |
| F4 | `0x1442D6` | `df d1` -> `c0 46` | `AppendIfValid` | Item Clause (bne to the return after the duplicate-item loop) removed | sig |
| F5 | `0x189C92` | `ec d8` -> `c0 46` | `GetBattleEntryEligibility` | party-menu level cap (bhi to return FALSE when level > cap) removed | sig |
| F5 | `0x189E1C` | `17 d0` -> `c0 46` | `CheckBattleEntriesAndGetMessage` | same-species message (beq to movs r0,#0x12) removed | sig |
| F5 | `0x189E36` | `e3 d1` -> `e3 e7` | `CheckBattleEntriesAndGetMessage` | same-item message: bne back into the loop made an unconditional b (movs r0,#0x13 is never reached) | sig |

**F1 ban list (data).** The hack's ban test reads one bit of each `gSpeciesInfo` row: rows of 208 bytes from ROM `0x8F0780`, byte `+0x8A`, bit 2 (found in the hack's own reader code; readers seen at `0x14428A`, `0x189CC0`). The bit is cleared in the 13 rows that have it, one byte each (offset = `0x8F0780` + species x 208 + 0x8A). The rows are:

150 Mewtwo, 151 Mew, 249 Lugia, 250 Ho-Oh, 251 Celebi, 382 Kyogre, 383 Groudon, 384 Rayquaza, 385 Jirachi, 386 Deoxys, 1054 Deoxys, 1055 Deoxys, 1056 Deoxys.

Every reader of this flag in the expansion source is a Battle Frontier check (registration, the caught-banned-species message, the party-menu able check), and every reader a scan of this hack's code found sits in those Frontier functions, so clearing it should change nothing outside the Frontier.

## Not included, and why

- **F6 Soul Dew**: not applied. The Soul Dew rule the design restores (R8-3) is a Battle Frontier test in front of the Gen 3 Soul Dew boost. These pokeemerald-expansion engines are configured with `B_SOUL_DEW_BOOST = GEN_LATEST` (expansion 1.9.2, 1.9.4 and 1.15.2, and Heart & Soul's own source), which compiles the Gen 7 effect (Latias/Latios Psychic and Dragon moves x1.2) with no Frontier test, so there is no gate to remove. Going back to the Gen 3 effect needs new code in a cave, which this beta does not add. For the closed-source hacks the setting was not read from the binary (DESIGN-6 M6-3 is still open).
- **F7 any ability**: not applied: needs new code and a free box field (DESIGN-6 S6-13 Full build), not part of the Frontier subset.
- **F8 6-Pokemon Battle Tower**: not applied: needs a save-layout change (DESIGN-6 S6-13 Full build), not part of the Frontier subset.
- **F9 Deoxys forms**: not needed: the Deoxys forms are separate species (rows 386, 1054-1056; DESIGN-6 M6-9, N/A-native).

## Notes

- Whether the hack lets you reach its Battle Frontier in normal play is the hack's own story; this patch only changes the entry rules.
