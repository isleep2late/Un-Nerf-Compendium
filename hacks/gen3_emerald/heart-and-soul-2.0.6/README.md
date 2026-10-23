**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**
# Emerald hack - Pokemon Heart & Soul (v2.0.6) [Emerald]: Battle Frontier entry rules removed

Status: **untested**. Built from static analysis only (reference builds of the hack's engine and a disassembly of the plain hack). Nobody has booted the patched game, entered a Battle Frontier challenge or fought a battle with it yet. Keep a backup of your save before you try it.

This is a patch **from the plain hack** (Pokemon Heart & Soul (v2.0.6) [Emerald], engine: pokeemerald-expansion 1.15.2 base, public source (tag Release-v2.0.6)) to an Un-Nerf build of the same hack. It is the Frontier-entry subset of the Emerald set in `gen3_emerald/` (the same rules `1_Emerald_FrontierUnlock_SoulDew.ips` removes from vanilla Emerald), placed at this hack's own code. Every change is in place: the RAM layout, the save layout and the ROM size are the plain hack's, so saves move both ways between the plain hack and this build.

## What it changes

- **Ban list emptied**: every Pokemon the hack bans from the Battle Frontier (its `isFrontierBanned` species flag) may enter.
- **Species Clause removed** from the registration check (two of the same Pokemon count as eligible).
- **Item Clause removed** from the registration check (two Pokemon holding the same item count as eligible).
- **Party menu**: the same-species and same-item refusals at Confirm are gone.
  In the party menu banned Pokemon show as able because the ban flag itself is cleared.
- F2 Lv 50 limit: already absent (N/A-native): Heart & Soul's Lv 50 mode compares the level with 100, not 50 (`cmp r3,#0x64` at `0x15812C` in `AppendIfValid.part.0`), and the party menu's cap is 100 for every Frontier facility (30 only for the Union Room; `0x19DCC6` in `GetBattleEntryEligibility`). Nothing to remove.

Eggs, fainted Pokemon (in modes that check HP) and the Battle Pyramid's held-item rule are left as the hack has them.

## Files

| File | What |
|---|---|
| `HeartAndSoul_UnNerf.ips` | the whole change as IPS (593 bytes, 97 records; does not check the input) |
| `bps/HeartAndSoul_UnNerf.bps` | the same change as BPS (634 bytes; refuses an input whose CRC32 is not the plain hack's) |

## Input (exact), how to apply, output

| | File | Size | SHA-1 | CRC32 |
|---|---|---|---|---|
| input | `Pokemon Heart & Soul (v2.0.6) [Emerald].gba` (the plain hack, unpatched) | 33554432 | `79ee6df0869c1773c8c6a5f764afc1f8d833d8bb` | `01713508` |
| output | `Pokemon Heart & Soul (v2.0.6) [Emerald] [Un-Nerf UNTESTED].gba` | 33554432 | `19f648a89dbe8177c5675f0e1dcd3dc5c986cbaf` | `a12d2174` |

Check the input's SHA-1 first: any other version or build of the hack has other code addresses. Apply with Floating IPS:

```
flips --apply HeartAndSoul_UnNerf.ips "Pokemon Heart & Soul (v2.0.6) [Emerald].gba" out.gba
flips --apply bps/HeartAndSoul_UnNerf.bps "Pokemon Heart & Soul (v2.0.6) [Emerald].gba" out.gba
```

Lunar IPS, Rom Patcher JS or any IPS/BPS tool works the same way. The result must have the output SHA-1 above. The ROM size and the cartridge header are unchanged, so no header checksum fix is needed.

## Where (ROM offsets in the plain hack)

Every code site was found by a masked signature (BL offsets masked, one hit in the hack) cut from a local build of Heart & Soul's own Release-v2.0.6 source (hack-unnerf refbuild `heart-and-soul-2.0.6`, not byte-identical to the release, so each hit was also checked: same original bytes, same branch target code and same fall-through code as the reference). In this engine `AppendIfValid` is split by the compiler: its ban test is inlined into `CheckPartyIneligibility` and the rest is `AppendIfValid.part.0`; `CheckBattleEntriesAndGetMessage` is inlined into `Task_ValidateChosenHalfParty`.

| Feature | Offset | Before -> after | Function | Change | Method |
|---|---|---|---|---|---|
| F3 | `0x158152` | `ed d1` -> `c0 46` | `AppendIfValid.part.0` | Species Clause (bne to the return after the duplicate-species loop) removed | sig |
| F4 | `0x158186` | `d3 d1` -> `c0 46` | `AppendIfValid.part.0` | Item Clause (bne to the return after the duplicate-item loop) removed | sig |
| F5 | `0x1A5B0A` | `2b d0` -> `c0 46` | `Task_ValidateChosenHalfParty` | same-species message (beq to movs r6,#0x12) removed (CheckBattleEntriesAndGetMessage inlined) | sig |
| F5 | `0x1A5B24` | `e3 d1` -> `e3 e7` | `Task_ValidateChosenHalfParty` | same-item message: bne back into the loop made an unconditional b (movs r6,#0x13 is never reached) | sig |

**F1 ban list (data).** The hack's ban test reads one bit of each `gSpeciesInfo` row: rows of 268 bytes from ROM `0x7E6544`, byte `+0x92`, bit 5 (found in the hack's own reader code; readers seen at `0x1587E4`, `0x1588C0`, `0x19DD0E`). The bit is cleared in the 93 rows that have it, one byte each (offset = `0x7E6544` + species x 268 + 0x92). The rows are:

150 MEWTWO, 151 MEW, 249 LUGIA, 250 HO-OH, 251 CELEBI, 382 KYOGRE, 383 GROUDON, 384 RAYQUAZA, 385 JIRACHI, 386 DEOXYS, 483 DIALGA, 484 PALKIA, 487 GIRATINA, 489 PHIONE, 490 MANAPHY, 491 DARKRAI, 492 SHAYMIN, 493 ARCEUS, 494 VICTINI, 643 RESHIRAM, 644 ZEKROM, 646 KYUREM, 647 KELDEO, 648 MELOETTA, 649 GENESECT, 716 XERNEAS, 717 YVELTAL, 718 ZYGARDE, 719 DIANCIE, 720 HOOPA, 721 VOLCANION, 789 COSMOG, 790 COSMOEM, 791 SOLGALEO, 792 LUNALA, 800 NECROZMA, 801 MAGEARNA, 802 MARSHADOW, 807 ZERAORA, 808 MELTAN, 809 MELMETAL, 888 ZACIAN, 889 ZAMAZENTA, 890 ETERNATUS, 893 ZARUDE, 898 CALYREX, 1054 DEOXYS, 1055 DEOXYS, 1056 DEOXYS, 1069 DIALGA, 1070 PALKIA, 1071 GIRATINA, 1072 SHAYMIN, 1073 ARCEUS, 1074 ARCEUS, 1075 ARCEUS, 1076 ARCEUS, 1077 ARCEUS, 1078 ARCEUS, 1079 ARCEUS, 1080 ARCEUS, 1081 ARCEUS, 1082 ARCEUS, 1083 ARCEUS, 1084 ARCEUS, 1085 ARCEUS, 1086 ARCEUS, 1087 ARCEUS, 1088 ARCEUS, 1089 ARCEUS, 1106 KELDEO, 1107 MELOETTA, 1108 GENESECT, 1109 GENESECT, 1110 GENESECT, 1111 GENESECT, 1163 XERNEAS, 1164 ZYGARDE, 1165 ZYGARDE, 1166 ZYGARDE, 1167 ZYGARDE, 1168 HOOPA, 1210 MAGEARNA, 1227 ZACIAN, 1228 ZAMAZENTA, 1229 ETERNATUS, 1231 ZARUDE, 1400 KORAIDON, 1401 MIRAIDON, 1431 TERAPAGOS, 1432 TERAPAGOS, 1433 TERAPAGOS, 1434 PECHARUNT.

Every reader of this flag in the expansion source is a Battle Frontier check (registration, the caught-banned-species message, the party-menu able check), and every reader a scan of this hack's code found sits in those Frontier functions, so clearing it should change nothing outside the Frontier.

## Not included, and why

- **F6 Soul Dew**: not applied. The Soul Dew rule the design restores (R8-3) is a Battle Frontier test in front of the Gen 3 Soul Dew boost. These pokeemerald-expansion engines are configured with `B_SOUL_DEW_BOOST = GEN_LATEST` (expansion 1.9.2, 1.9.4 and 1.15.2, and Heart & Soul's own source), which compiles the Gen 7 effect (Latias/Latios Psychic and Dragon moves x1.2) with no Frontier test, so there is no gate to remove. Going back to the Gen 3 effect needs new code in a cave, which this beta does not add. For the closed-source hacks the setting was not read from the binary (DESIGN-6 M6-3 is still open).
- **F7 any ability**: not applied: needs new code and a free box field (DESIGN-6 S6-13 Full build), not part of the Frontier subset.
- **F8 6-Pokemon Battle Tower**: not applied: needs a save-layout change (DESIGN-6 S6-13 Full build), not part of the Frontier subset.
- **F9 Deoxys forms**: not needed: the Deoxys forms are separate species in this engine (DESIGN-6 M6-9, N/A-native).

## Notes

- Heart & Soul's own challenge option that lifts the Frontier bans (`tx_Features_FrontierBans`) still works and now has nothing left to lift.
- Whether the hack lets you reach its Battle Frontier in normal play is the hack's own story; this patch only changes the entry rules.
