**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**
# Emerald hack - Pokemon Lazarus (v2.0) [Emerald]: Battle Frontier entry rules removed

Status: **untested**. Built from static analysis only (reference builds of the hack's engine and a disassembly of the plain hack). Nobody has booted the patched game, entered a Battle Frontier challenge or fought a battle with it yet. Keep a backup of your save before you try it.

This is a patch **from the plain hack** (Pokemon Lazarus (v2.0) [Emerald], engine: pokeemerald-expansion 1.9.4 base, closed source) to an Un-Nerf build of the same hack. It is the Frontier-entry subset of the Emerald set in `gen3_emerald/` (the same rules `1_Emerald_FrontierUnlock_SoulDew.ips` removes from vanilla Emerald), placed at this hack's own code. Every change is in place: the RAM layout, the save layout and the ROM size are the plain hack's, so saves move both ways between the plain hack and this build.

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
| `Lazarus_UnNerf.ips` | the whole change as IPS (150 bytes, 23 records; does not check the input) |
| `bps/Lazarus_UnNerf.bps` | the same change as BPS (174 bytes; refuses an input whose CRC32 is not the plain hack's) |

## Input (exact), how to apply, output

| | File | Size | SHA-1 | CRC32 |
|---|---|---|---|---|
| input | `Pokemon Lazarus (v2.0) [Emerald].gba` (the plain hack, unpatched) | 33554432 | `7dcdc7e280bc4631487e13dd37e6e0cea04adea6` | `558ae42f` |
| output | `Pokemon Lazarus (v2.0) [Emerald] [Un-Nerf UNTESTED].gba` | 33554432 | `63da9306f8bb0085c7c3052a7b8be54b7b0d891d` | `e84b4761` |

Check the input's SHA-1 first: any other version or build of the hack has other code addresses. Apply with Floating IPS:

```
flips --apply Lazarus_UnNerf.ips "Pokemon Lazarus (v2.0) [Emerald].gba" out.gba
flips --apply bps/Lazarus_UnNerf.bps "Pokemon Lazarus (v2.0) [Emerald].gba" out.gba
```

Lunar IPS, Rom Patcher JS or any IPS/BPS tool works the same way. The result must have the output SHA-1 above. The ROM size and the cartridge header are unchanged, so no header checksum fix is needed.

## Where (ROM offsets in the plain hack)

Code sites were found by masked signature (BL offsets masked, one hit in the hack) cut from a local build of pokeemerald-expansion 1.9.4 (hack-unnerf refbuild `expansion-1.9.4`); each hit was checked for the same original bytes, the same branch-target code and the same fall-through code as the reference. One site (the Species Clause) differs from 1.9.4 only in the register order of its compare (`cmp r4,r3` instead of `cmp r3,r4`), so the signature does not match; it was read by hand from the disassembly and is pinned by 24 bytes around it that occur once in the hack.

| Feature | Offset | Before -> after | Function | Change | Method |
|---|---|---|---|---|---|
| F2 | `0x15361E` | `02 d9` -> `02 e0` | `AppendIfValid` | Lv50-mode level check: bls over the return made an unconditional b (the return for level > 50 is never taken) | sig |
| F3 | `0x153642` | `ed d1` -> `c0 46` | `AppendIfValid` | Species Clause (bne to the return after the duplicate-species loop) removed | asm |
| F4 | `0x15365E` | `df d1` -> `c0 46` | `AppendIfValid` | Item Clause (bne to the return after the duplicate-item loop) removed | sig |
| F5 | `0x1A13E6` | `ec d8` -> `c0 46` | `GetBattleEntryEligibility` | party-menu level cap (bhi to return FALSE when level > cap) removed | sig |
| F5 | `0x1A1570` | `17 d0` -> `c0 46` | `CheckBattleEntriesAndGetMessage` | same-species message (beq to movs r0,#0x12) removed | sig |
| F5 | `0x1A158A` | `e3 d1` -> `e3 e7` | `CheckBattleEntriesAndGetMessage` | same-item message: bne back into the loop made an unconditional b (movs r0,#0x13 is never reached) | sig |

**F1 ban list (data).** The hack's ban test reads one bit of each `gSpeciesInfo` row: rows of 212 bytes from ROM `0xC7A338`, byte `+0x8A`, bit 4 (found in the hack's own reader code; readers seen at `0x153612`, `0x1A1414`). The bit is cleared in the 17 rows that have it, one byte each (offset = `0xC7A338` + species x 212 + 0x8A). The rows are:

249 Lugia, 250 Ho-Oh, 251 Celebi, 382 Kyogre, 383 Groudon, 384 Rayquaza, 718 Zygarde, 953 Rayquaza, 954 Kyogre, 955 Groudon, 1164 Zygarde, 1166 Zygarde, 1167 Zygarde, 1431 Terapagos, 1432 Terapagos, 1433 Terapagos, 1434 Pecharunt.

Every reader of this flag in the expansion source is a Battle Frontier check (registration, the caught-banned-species message, the party-menu able check), and every reader a scan of this hack's code found sits in those Frontier functions, so clearing it should change nothing outside the Frontier.

## Not included, and why

- **F6 Soul Dew**: not applied. The Soul Dew rule the design restores (R8-3) is a Battle Frontier test in front of the Gen 3 Soul Dew boost. These pokeemerald-expansion engines are configured with `B_SOUL_DEW_BOOST = GEN_LATEST` (expansion 1.9.2, 1.9.4 and 1.15.2, and Heart & Soul's own source), which compiles the Gen 7 effect (Latias/Latios Psychic and Dragon moves x1.2) with no Frontier test, so there is no gate to remove. Going back to the Gen 3 effect needs new code in a cave, which this beta does not add. For the closed-source hacks the setting was not read from the binary (DESIGN-6 M6-3 is still open).
- **F7 any ability**: not applied: needs new code and a free box field (DESIGN-6 S6-13 Full build), not part of the Frontier subset.
- **F8 6-Pokemon Battle Tower**: not applied: needs a save-layout change (DESIGN-6 S6-13 Full build), not part of the Frontier subset.
- **F9 Deoxys forms**: not applicable: Lazarus has no Deoxys (its species rows 386 and 1054-1056 are empty; DESIGN-6 M6-9, N/A-absent).

## Notes

- Lazarus bans its own short list (the 17 rows listed above, without Mewtwo, Mew, Jirachi or Deoxys); the list is cleared exactly as the hack defines it.
- Whether the hack lets you reach its Battle Frontier in normal play is the hack's own story; this patch only changes the entry rules.
