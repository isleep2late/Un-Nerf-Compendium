**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**
# Emerald hack - Pokemon SoulGold GBA (v1.1.4) [Emerald]: Battle Frontier entry rules removed

Status: **untested**. Built from static analysis only (reference builds of the hack's engine and a disassembly of the plain hack). Nobody has booted the patched game, entered a Battle Frontier challenge or fought a battle with it yet. Keep a backup of your save before you try it.

This is a patch **from the plain hack** (Pokemon SoulGold GBA (v1.1.4) [Emerald], engine: pokeemerald-expansion (its header says 1.15.2), closed source; its code matches no reference build) to an Un-Nerf build of the same hack. It is the Frontier-entry subset of the Emerald set in `gen3_emerald/` (the same rules `1_Emerald_FrontierUnlock_SoulDew.ips` removes from vanilla Emerald), placed at this hack's own code. Every change is in place: the RAM layout, the save layout and the ROM size are the plain hack's, so saves move both ways between the plain hack and this build.

## What it changes

- **Ban list emptied**: every Pokemon the hack bans from the Battle Frontier (its `isFrontierBanned` species flag) may enter.
- **Species Clause removed** from the registration check (two of the same Pokemon count as eligible).
- **Item Clause removed** from the registration check (two Pokemon holding the same item count as eligible).
- **Party menu**: the same-species and same-item refusals at Confirm are gone.
  In the party menu banned Pokemon show as able because the ban flag itself is cleared.
- F2 Lv 50 limit: already absent (N/A-native): SoulGold's registration check reads the level and drops it (its `AppendIfValid` takes no level and has no level test), and the party menu's cap is 100 for every Frontier facility (30 only for the Union Room). Nothing to remove.

Eggs, fainted Pokemon (in modes that check HP) and the Battle Pyramid's held-item rule are left as the hack has them.

## Files

| File | What |
|---|---|
| `SoulGold_UnNerf.ips` | the whole change as IPS (708 bytes, 116 records; does not check the input) |
| `bps/SoulGold_UnNerf.bps` | the same change as BPS (676 bytes; refuses an input whose CRC32 is not the plain hack's) |

## Input (exact), how to apply, output

| | File | Size | SHA-1 | CRC32 |
|---|---|---|---|---|
| input | `Pokemon SoulGold GBA (v1.1.4) [Emerald].gba` (the plain hack, unpatched) | 33554432 | `ea5d369cc8a31cbf1cfacb7c9470ea670f08957b` | `ba7394a6` |
| output | `Pokemon SoulGold GBA (v1.1.4) [Emerald] [Un-Nerf UNTESTED].gba` | 33554432 | `a4b73b25fb1211e7691669154d5063d0100a6e94` | `516db9af` |

Check the input's SHA-1 first: any other version or build of the hack has other code addresses. Apply with Floating IPS:

```
flips --apply SoulGold_UnNerf.ips "Pokemon SoulGold GBA (v1.1.4) [Emerald].gba" out.gba
flips --apply bps/SoulGold_UnNerf.bps "Pokemon SoulGold GBA (v1.1.4) [Emerald].gba" out.gba
```

Lunar IPS, Rom Patcher JS or any IPS/BPS tool works the same way. The result must have the output SHA-1 above. The ROM size and the cartridge header are unchanged, so no header checksum fix is needed.

## Where (ROM offsets in the plain hack)

No signature from any reference build (expansion 1.9.2, 1.9.4, 1.15.2 or Heart & Soul) matches SoulGold, so every site here was read by hand from the hack's own disassembly (method asm) and each is pinned by the 24 bytes around it, which occur exactly once in the hack. The ban flag was first picked out by comparing every species-row bit with the expansion ban list, then confirmed by five code readers in the hack (the registration check twice and the party-menu able check of each of SoulGold's three party-menu styles), all loading row byte `+0x9F` bit 5. SoulGold has three party-menu implementations (chosen by a menu option; `InitChooseHalfPartyForBattle` at `0x081D16D0` picks one); each has its own Confirm check, and all three are edited.

| Feature | Offset | Before -> after | Function | Change | Method |
|---|---|---|---|---|---|
| F3 | `0x16D6C0` | `06 d0` -> `06 e0` | `AppendIfValid` | Species Clause: beq over the return (taken when no earlier entry has the species) made an unconditional b, so a duplicate species is appended | asm |
| F4 | `0x16D720` | `cf d1` -> `c0 46` | `AppendIfValid` | Item Clause: bne to the return after the duplicate-item loop removed; falls through to the append | asm |
| F5 | `0x17F26C` | `51 d0` -> `c0 46` | party-menu Confirm check, menu style 1 | same-species message (beq to movs r4,#0x12) removed | asm |
| F5 | `0x17F2A0` | `14 d1` -> `14 e0` | party-menu Confirm check, menu style 1 | same-item message, first compare: bne made an unconditional b (movs r4,#0x13 never reached) | asm |
| F5 | `0x17F2E6` | `b0 d1` -> `b0 e7` | party-menu Confirm check, menu style 1 | same-item message, second compare: bne back into the loop made an unconditional b | asm |
| F5 | `0x1CBD84` | `51 d0` -> `c0 46` | party-menu Confirm check, menu style 2 | same-species message (beq to movs r4,#0x12) removed | asm |
| F5 | `0x1CBDB8` | `14 d1` -> `14 e0` | party-menu Confirm check, menu style 2 | same-item message, first compare: bne made an unconditional b (movs r4,#0x13 never reached) | asm |
| F5 | `0x1CBDFE` | `b0 d1` -> `b0 e7` | party-menu Confirm check, menu style 2 | same-item message, second compare: bne back into the loop made an unconditional b | asm |
| F5 | `0x253114` | `2b d0` -> `c0 46` | party-menu Confirm check, default menu style | same-species message (beq to ldr r4,="Pokemon can't be the same.") removed | asm |
| F5 | `0x253130` | `e0 d1` -> `e0 e7` | party-menu Confirm check, default menu style | same-item message: bne back into the loop made an unconditional b (ldr r4,="No identical hold items." never reached) | asm |

**F1 ban list (data).** The hack's ban test reads one bit of each `gSpeciesInfo` row: rows of 280 bytes from ROM `0x7D44A8`, byte `+0x9F`, bit 5 (found in the hack's own reader code; readers seen at `0x16DB5C`, `0x16DCC2`, `0x1774FA`, `0x1C4012`, `0x24C7EA`). The bit is cleared in the 106 rows that have it, one byte each (offset = `0x7D44A8` + species x 280 + 0x9F). The rows are:

150 Mewtwo, 151 Mew, 249 Lugia, 250 Ho-Oh, 251 Celebi, 382 Kyogre, 383 Groudon, 384 Rayquaza, 385 Jirachi, 386 Deoxys, 483 Dialga, 484 Palkia, 487 Giratina, 490 Manaphy, 491 Darkrai, 492 Shaymin, 493 Arceus, 494 Victini, 643 Reshiram, 644 Zekrom, 646 Kyurem, 647 Keldeo, 648 Meloetta, 649 Genesect, 716 Xerneas, 717 Yveltal, 718 Zygarde, 719 Diancie, 720 Hoopa, 721 Volcanion, 789 Cosmog, 790 Cosmoem, 791 Solgaleo, 792 Lunala, 800 Necrozma, 801 Magearna, 802 Marshadow, 807 Zeraora, 808 Meltan, 809 Melmetal, 888 Zacian, 889 Zamazenta, 890 Eternatus, 893 Zarude, 898 Calyrex, 919 Mewtwo, 920 Mewtwo, 952 Diancie, 953 Rayquaza, 954 Kyogre, 955 Groudon, 1054 Deoxys, 1055 Deoxys, 1056 Deoxys, 1069 Dialga, 1070 Palkia, 1071 Giratina, 1072 Shaymin, 1073 Arceus, 1074 Arceus, 1075 Arceus, 1076 Arceus, 1077 Arceus, 1078 Arceus, 1079 Arceus, 1080 Arceus, 1081 Arceus, 1082 Arceus, 1083 Arceus, 1084 Arceus, 1085 Arceus, 1086 Arceus, 1087 Arceus, 1088 Arceus, 1089 Arceus, 1104 Kyurem, 1105 Kyurem, 1106 Keldeo, 1107 Meloetta, 1108 Genesect, 1109 Genesect, 1110 Genesect, 1111 Genesect, 1163 Xerneas, 1164 Zygarde, 1165 Zygarde, 1166 Zygarde, 1167 Zygarde, 1168 Hoopa, 1207 Necrozma, 1208 Necrozma, 1209 Necrozma, 1210 Magearna, 1227 Zacian, 1228 Zamazenta, 1229 Eternatus, 1231 Zarude, 1232 Calyrex, 1233 Calyrex, 1400 Koraidon, 1401 Miraidon, 1431 Terapagos, 1432 Terapagos, 1433 Terapagos, 1434 Pecharunt, 1435 Lugia.

Every reader of this flag in the expansion source is a Battle Frontier check (registration, the caught-banned-species message, the party-menu able check), and every reader a scan of this hack's code found sits in those Frontier functions, so clearing it should change nothing outside the Frontier.

## Not included, and why

- **F6 Soul Dew**: not applied. The Soul Dew rule the design restores (R8-3) is a Battle Frontier test in front of the Gen 3 Soul Dew boost. These pokeemerald-expansion engines are configured with `B_SOUL_DEW_BOOST = GEN_LATEST` (expansion 1.9.2, 1.9.4 and 1.15.2, and Heart & Soul's own source), which compiles the Gen 7 effect (Latias/Latios Psychic and Dragon moves x1.2) with no Frontier test, so there is no gate to remove. Going back to the Gen 3 effect needs new code in a cave, which this beta does not add. For the closed-source hacks the setting was not read from the binary (DESIGN-6 M6-3 is still open).
- **F7 any ability**: not applied: needs new code and a free box field (DESIGN-6 S6-13 Full build), not part of the Frontier subset.
- **F8 6-Pokemon Battle Tower**: not applied: needs a save-layout change (DESIGN-6 S6-13 Full build), not part of the Frontier subset.
- **F9 Deoxys forms**: not needed: the Deoxys forms are separate species (rows 386, 1054-1056; DESIGN-6 M6-9, N/A-native).

## Notes

- SoulGold applied its bans only in Lv 50 mode (Open Level already skipped them); with the flag cleared there are no bans in either mode.
- SoulGold's Battle Frontier is its own map group (group 22). An earlier static survey (DESIGN-6 M6-10) found its maps call the Frontier party special, but the registration was never walked in game.
- Whether the hack lets you reach its Battle Frontier in normal play is the hack's own story; this patch only changes the entry rules.
