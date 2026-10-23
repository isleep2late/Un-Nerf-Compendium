# Pokemon Ghost Grey Version (v1.03) [FireRed] - Union Room level cap, any ability (Un-Nerf)
**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**

Status: **untested**. Built from static analysis only: the FireRed [Un-Nerf] patch (the verified `Pokemon - FireRed Version (USA) [Un-Nerf].gba`, hack-unnerf recipe `gen3_frlg/firered-r0`) re-applied to Ghost Grey at the same addresses after checking that every original byte, and the 32 bytes around each site, are FireRed's. Nobody has booted the patched hack, fought a battle on it or checked an ability in game yet. Keep a backup of your save before you try it.

What it changes (in place: the RAM and save layout stay Ghost Grey's, so your save keeps working):

- **Union Room level-30 limit lifted** in both checks: inviting or accepting a battle (`HasAtLeastTwoMonsOfLevel30OrLower`) and the party menu (`GetBattleEntryEligibility`). Two non-egg Pokemon are still needed. It matters only with a Wireless Adapter. Ghost Grey has the Trainer Tower and the Union Room (FireRed's).
- **Any ability**: PK3 byte `0x1E` = **2..138** makes that ability the Pokemon's ability in battle (battle start, switch-in), on the summary screen and for the Soundproof checks of Heal Bell and the Poke Flute; `0` (and `1`, and anything above 138) keeps the normal ability from the PID bit. Same byte and meaning as the Emerald and FireRed Un-Nerf. Ghost Grey's ability list is repointed (names at `0x08950000`): ids 2..138 are accepted; 78 is an empty `-------` slot (no effect) and 139..155 are empty slots, so they are not accepted.
- **Parity edits, no effect in normal play** (as on FireRed): the Battle Tower code left over from Ruby, which no script reaches, gets its level-50 cap, ban list and Species/Item Clause opened; the Battle Tower gate on the Soul Dew boost is removed (the +50 % Sp. Atk / Sp. Def boost already applies in every battle you can play).

## Files

| File | What |
|---|---|
| `GhostGrey_UnNerf.ips` | everything above, IPS (346 bytes, 21 records) |
| `bps/GhostGrey_UnNerf.bps` | the same change as a BPS (336 bytes; checks the input CRC32) |

## Input (exact), how to apply, output

| | File | SHA-1 | CRC32 |
|---|---|---|---|
| input | `Pokemon Ghost Grey Version (v1.03) [FireRed].gba` (FireRed rev 0 patched with `GhostGrey03.bps` from `Pokemon Ghost Grey Version v103.zip`, byte-identical to the English ROM in the same zip) | `46e1cbb178df0bed76f703dd2148e703b409e60d` | `c2d95b0c` |
| output | `Pokemon Ghost Grey Version (v1.03) [FireRed] [Un-Nerf UNTESTED].gba` | `00ed8b28d4505a3c488c625350d9b78df1fdd3e5` | `a7ebb5d4` |

The patch is made against the plain hack above, not against clean FireRed, and only this exact version is supported: check your file's SHA-1 first. The IPS does not check its input; the BPS refuses a wrong input.

Apply with Floating IPS (`flips --apply GhostGrey_UnNerf.ips "Pokemon Ghost Grey Version (v1.03) [FireRed].gba" out.gba`), Rom Patcher JS or any IPS/BPS tool; the BPS works the same way. The result must have the output SHA-1 above. The ROM size (32 MiB) and the cartridge header are unchanged, so no checksum fix is needed (header complement `0x68`, valid).

## What each change does (ROM offsets)

| Feature | Offset | Before -> after | Where (pokefirered names) |
|---|---|---|---|
| Union Room | `0x127578` | `32 d9` -> `32 e0` | `GetBattleEntryEligibility`: `cmp r0,#30; bls` -> `b` (party menu) |
| Union Room | `0x11B112` | `08 d8` -> `c0 46` | `HasAtLeastTwoMonsOfLevel30OrLower`: every non-egg Pokemon counts (invite / accept) |
| Tower Lv 50 | `0x1275AC` | `e5 d8` -> `c0 46` | `GetBattleEntryEligibility`, Battle Tower case (unreachable) |
| Tower ban list | `0x4020C4` | `97 00` -> `ff ff` | `gBattleTowerBannedSpecies` ends at its first entry (unreachable) |
| Species Clause | `0x12769E` | `2b d0` -> `c0 46` | `CheckBattleEntriesAndGetMessage` (unreachable) |
| Item Clause | `0x1276C4` | `1a d0` -> `c0 46` | `CheckBattleEntriesAndGetMessage` (unreachable) |
| Soul Dew | `0x03EF6A`, `0x03EFA2` | `11 d1` -> `c0 46` | `CalculateBaseDamage`: attacker and defender `BATTLE_TYPE_BATTLE_TOWER` gate |
| Any ability | `0x3E4874` (176 bytes) | new routines | written over `sUnusedWater`, FireRed graphic data nothing uses: its bytes are FireRed's and no pointer or branch in Ghost Grey leads into it |
| Any ability | `0x040D3A` | BL | `GetAbilityBySpecies`: an ability number of 2..138 is returned as the ability |
| Any ability | `0x040D94` | BL | `GetMonAbility` reads byte `0x1E` |
| Any ability | `0x1366F8` | BL | summary screen (`BufferMonSkills`) shows the stored ability |
| Any ability | `0x013154`, `0x023FDE` | BL | battle start and switch-in: the battler's ability comes from its party Pokemon's byte `0x1E` |
| Any ability | `0x04131E` | BL | `CopyPlayerPartyMonToBattleData` (the same for that path) |
| Any ability | `0x026E10`, `0x026EAE`, `0x02A80E` | BL | Poke Flute and Heal Bell Soundproof checks use the stored ability |
| Any ability | `0x02CE7A`, `0x02CE7E` | BL + branch | `Cmd_pickup` reads the full ability |

The routines are the shared Gen 3 any-ability module (hack-unnerf `g3_anyability.py`) with FireRed's symbols; only the upper bound differs from FireRed's 77 (four `cmp #138` immediates).

### Setting an ability

Write the ability id into byte `0x1E` of the Pokemon's PK3 data (outside the checksum, so nothing has to be recomputed). The Compendium's PKHaX writes that byte for Gen 3 saves, but it does not know Ghost Grey's species and ability lists yet (the hack save editor for the binary FireRed hacks comes later), so for now use an editor that lets you set the raw byte. Use an id this hack names (see above).

## Notes

- Byte `0x1E` is unused in Ghost Grey as far as static reading shows: the box-data get/set routines keep FireRed's code for that field (the `MON_DATA_ENCRYPT_SEPARATOR` case is byte-identical); Ghost Grey changed only the IV setters, and the only code that writes that field through `SetMonData` is FireRed's own `CreateShedinja`, which clears it. A hack routine that touches it through a computed pointer cannot be ruled out without the in-game test.
- Abilities above 77 are Ghost Grey's own: whether one does something depends on the hack's battle code, as it does for the species that have it.
- Link battles and trades with a custom-ability Pokemon need both players on this patched file; an unpatched game uses the normal ability and a link battle can desync. The enemy AI still guesses abilities from the species.
- The Union Room attendant still explains the old Lv 30 rule; the text is unchanged.

## Not in this beta (skipped, and why)

- **Deoxys formes**: Ghost Grey changed the Deoxys stat row (6 of 12 bytes differ, DESIGN-8 3.7): not in this beta (the owner asked to skip Deoxys formes).
- **Editing the ability in PKHaX for this hack**: needs the legacy hack save codec (DESIGN-8 R8-11), not part of a ROM patch.
- **A separate "live" IPS** (only the two-byte edits, for applying while the game runs): not made for this beta; the IPS above contains those edits plus the any-ability code.
- **Trainer Tower and link-battle rules**: there is nothing to lift (FireRed's Trainer Tower has no ban list, clauses or cap; the Cable Club only refuses fainted Pokemon): `N/A-native`.
