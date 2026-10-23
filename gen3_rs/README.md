# Gen 3 - Pokemon Ruby / Sapphire: Battle Tower unlock, Soul Dew, any ability
**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**

Status: **untested**. These patches were built from static analysis only (the pokeruby decomp's symbols and a
disassembly of the clean dumps). Nobody has booted the patched games, played a Battle Tower challenge, or checked an
ability in battle yet. Keep a backup of your save before you try them.

Brings Ruby and Sapphire in line with the Emerald set in `gen3_emerald/` (in place, same RAM and save layout as the
clean game):

- **Battle Tower rules removed**: the ban list is empty (Mew, Mewtwo, Lugia, Ho-Oh, Celebi, Kyogre, Groudon,
  Rayquaza, Jirachi, Deoxys may enter), the **Lv 50 limit** is gone in Lv 50 mode, and **Species Clause / Item
  Clause** are gone (two of the same Pokemon, or two holding the same item, are allowed).
- **Soul Dew un-nerf**: the Battle Tower gate on the Soul Dew boost is removed, so Latias/Latios keep +50 % Sp. Atk and
  Sp. Def inside the Tower too (outside the Tower the game already gave it).
- **Any ability**: any Pokemon can have any of the 77 Gen 3 abilities, stored in PK3 byte `0x1E` exactly like the
  Emerald Un-Nerf (`0` = the normal ability from the PID bit, `2..77` = that ability; `1` and `78..255` are ignored).

## Files

| File | For | What |
|---|---|---|
| `Ruby_UnNerf.ips` | Ruby | everything above, IPS (359 bytes, 26 records) |
| `Sapphire_UnNerf.ips` | Sapphire | everything above, IPS (359 bytes, 26 records) |
| `bps/Ruby_UnNerf.bps` | Ruby | the same change as a BPS (checks the input CRC32) |
| `bps/Sapphire_UnNerf.bps` | Sapphire | the same change as a BPS (checks the input CRC32) |

## Input (exact), how to apply, output

| Game | Clean input (No-Intro) | Input SHA-1 | Input CRC32 | Output SHA-1 | Output CRC32 |
|---|---|---|---|---|---|
| Ruby | `Pokemon - Ruby Version (USA).gba` (rev 0) | `f28b6ffc97847e94a6c21a63cacf633ee5c8df1e` | `f0815ee7` | `3875e1f705d98a582b075c16653385733a13d9cc` | `02550727` |
| Sapphire | `Pokemon - Sapphire Version (USA).gba` (rev 0) | `3ccbbd45f8553c36463f13b938e833f652b793e4` | `554dedc4` | `a6cc34bba09524007314b14864e26fa245281786` | `18f3a9b8` |

Only rev 0 is supported. Rev 1 and rev 2 dumps (and European or Japanese ones) have different code addresses; check your
dump's SHA-1 first. The IPS files do not check the input; the BPS files refuse a wrong input.

Apply with Floating IPS (`flips --apply Ruby_UnNerf.ips "Pokemon - Ruby Version (USA).gba" out.gba`), Lunar IPS,
Rom Patcher JS or any IPS/BPS tool; the BPS files work the same way. The result must have the output SHA-1 above. The
ROM size (16 MiB) and the cartridge header are unchanged, so no checksum fix is needed.

Saves are compatible both ways with the clean game (code-only change). A clean game ignores byte `0x1E` and shows the
normal ability.

## What each change does (ROM offsets; Ruby = Sapphire unless both are given)

| Feature | Offset | Before -> after | Where (pokeruby) |
|---|---|---|---|
| Lv 50 limit | `0x12207A` | `05 d9` -> `05 e0` | `IsMonAllowedInBattleTower`: the party menu lets a Pokemon above Lv 50 enter in Lv 50 mode |
| Lv 50 limit | `0x13526E` | `2e d8` -> `c0 46` | `CheckMonBattleTowerBanlist`: the lobby's "three eligible Pokemon" count accepts it too |
| Ban list | R `0x405EC4` / S `0x405F1C` | `97 00` -> `ff ff` | `gBattleTowerBannedSpecies`: the list now ends at its first entry (empty) |
| Species Clause | `0x122168` | `c6 d0` -> `c0 46` | party menu check that refused two of one species |
| Item Clause | `0x12218C` | `b6 d0` -> `c0 46` | party menu check that refused two holding the same item |
| Species Clause | `0x135292` | `1c d1` -> `c0 46` | lobby count no longer skips a second Pokemon of a species |
| Item Clause | `0x1352B8` | `09 d1` -> `c0 46` | lobby count no longer skips a second Pokemon holding the same item |
| Soul Dew | `0x03BD02`, `0x03BD3A` | `11 d1` -> `c0 46` | `CalculateBaseDamage`: attacker and defender `BATTLE_TYPE_BATTLE_TOWER` gate removed |
| Any ability | R `0x073D84` / S `0x073D88` (176 bytes) | new routines | written over `unref_sub_8073D84` / `unused_sub_8073DFC`, palette helpers nothing calls or points to |
| Any ability | `0x03DB16` | BL | `GetAbilityBySpecies`: an ability number of 2..77 is returned as the ability |
| Any ability | `0x03DB70` | BL | `GetMonAbility` reads byte `0x1E` |
| Any ability | `0x09FE0A` | BL | summary screen shows the stored ability |
| Any ability | `0x011460`, `0x022686` | BL | battle start and switch-in: the battler's ability comes from its party Pokemon's byte `0x1E` |
| Any ability | `0x03E0EE` | BL | `CopyPlayerPartyMonToBattleData` (the same for that path) |
| Any ability | `0x028982` | BL | Heal Bell's Soundproof check uses the stored ability |
| Any ability | `0x02AFA0`, `0x02AFA4` | BL + branch | Pickup reads the full ability |

Every one of the 6 calls to `GetAbilityBySpecies` and the 1 call to `GetMonAbility` in the ROM is covered. The
routines are the shared Gen 3 any-ability module of the FireRed/LeafGreen Un-Nerf, re-targeted to the pokeruby
symbols.

### Setting an ability

Use the Compendium's PKHaX (`PKHaX/`): open a Gen 3 Pokemon from a Ruby or Sapphire save, pick any ability in the
Ability box and save. Picking one of the species' own two abilities clears the override; anything else writes byte
`0x1E`.

## Notes

- The Battle Tower attendants still explain the old rules; the text is unchanged.
- Link battles and trades with a custom-ability Pokemon need both players on these patched games; a clean game uses
  the normal ability and a link battle can desync.
- The enemy AI still guesses abilities from the species, as in the clean game.
- The Tower record of your team that other games receive by record mixing keeps only the normal ability.

## Not in this beta (skipped, and why)

- **6-Pokemon Battle Tower singles and Deoxys formes (the "Full" build)**: these need a relinked pokeruby build and,
  for Deoxys, the other formes' pictures taken from other games; not part of this in-place beta.
- **A separate "live" IPS** (only the two-byte Tower and Soul Dew edits, for applying while the game runs): not made
  for this beta; the IPS above contains those edits plus the any-ability code.
- **Rev 1 / rev 2 and non-USA dumps**: not supported.
