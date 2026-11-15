# Pokemon Unbound (v2.1.1.1) [FireRed] - Union Room level cap (Un-Nerf)
**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**

Status: **untested**. Built from static analysis only (a disassembly of the plain hack compared with FireRed rev 0 and the pokefirered symbols). Nobody has booted the patched hack yet. Keep a backup of your save before you try it.

Unbound is built on the Complete Fire Red Upgrade engine (CFRU), which replaced most of what the FireRed Un-Nerf patches. Of the FireRed [Un-Nerf] set only **one change** can be placed with confidence here, so this beta is small:

- **Union Room level-30 limit lifted** for inviting or accepting a battle (`HasAtLeastTwoMonsOfLevel30OrLower`, still FireRed's routine, called from FireRed's two Union Room tasks): every non-egg Pokemon counts, two are still needed. It matters only if Unbound can reach the Union Room (it needs a Wireless Adapter).

Saves are unchanged (code-only, two bytes).

## Files

| File | What |
|---|---|
| `Unbound_UnNerf.ips` | the change above, IPS (15 bytes, 1 record) |
| `bps/Unbound_UnNerf.bps` | the same change as a BPS (36 bytes; checks the input CRC32) |

## Input (exact), how to apply, output

| | File | SHA-1 | CRC32 |
|---|---|---|---|
| input | `Pokemon Unbound (v2.1.1.1) [FireRed].gba` (the Unbound 2.1.1.1 ROM as released, `Pokemon Unbound (v2.1.1.1).gba`, byte-identical) | `b4776b82a4c7915d0fadeaa27e013523f99dfd94` | `4b3d4957` |
| output | `Pokemon Unbound (v2.1.1.1) [FireRed] [Un-Nerf UNTESTED].gba` | `a60983f478b02875fe6bca99cab4c4c33ad57561` | `3f40eddd` |

The patch is made against the plain hack above, not against clean FireRed, and only this exact version is supported: check your file's SHA-1 first. The IPS does not check its input; the BPS refuses a wrong input.

Apply with Floating IPS (`flips --apply Unbound_UnNerf.ips "Pokemon Unbound (v2.1.1.1) [FireRed].gba" out.gba`), Rom Patcher JS or any IPS/BPS tool; the BPS works the same way. The result must have the output SHA-1 above. The ROM size (32 MiB) and the cartridge header are unchanged, so no checksum fix is needed (header complement `0x68`, valid).

## What the change does

| Feature | Offset | Before -> after | Where |
|---|---|---|---|
| Union Room | `0x11B112` | `08 d8` -> `c0 46` | `HasAtLeastTwoMonsOfLevel30OrLower` (`0x0811B0F0`, byte-identical to FireRed, called at `0x08118CFA` and `0x0811925A`): `cmp r0,#30; bhi` -> `nop` |

## Already in Unbound (nothing to change)

- **Union Room party menu**: CFRU replaced FireRed's `GetBattleEntryEligibility` (a jump at `0x08127550` to `0x089C3978`); outside a facility challenge it refuses only eggs and fainted Pokemon, with no level test, so FireRed's second Union Room edit has nothing to change (`N/A-native`).
- **Battle Tower rules**: Unbound's Battle Tower already has a **No Restrictions** tier, **6v6** and Level 50 / Level 100 modes (pick Level 100 mode to fight at full level); there is no level cap to lift (`N/A-native`, DESIGN-8 3.4/3.5).
- **Hidden abilities, Ability Capsule, Dream Mist**: native.
- **Deoxys formes**: separate species in Unbound (`N/A-native`).
- **Ring Challenge** (Antisis Gym): a story gym puzzle, left as the hack made it (OQ-80 default).

## Not in this beta (skipped, and why)

- **FireRed's Battle Tower parity edits** (level-50 cap, ban list, Species/Item Clause at `0x1275AC`, `0x4020C4`, `0x12769E`, `0x1276C4`): the bytes are still FireRed's, but CFRU jumps away at the start of both functions that hold them (`0x08127550`, `0x081275F8`), and the FireRed ban table is read only by that bypassed code. Writing them would change nothing, so they are left out.
- **Tier bans in Battle Sands, Battle Mine and Battle Circus, and Species/Item Clause off in the facilities** (DESIGN-8 3.5): these checks are in Unbound's own CFRU code (the eligibility routine at `0x089C3978` and the selection check at `0x08A0310C`, reached through jumps at FireRed's `0x08127550` / `0x081275F8`). DESIGN-8 left them as `[M8-4]` (to be located from a CFRU reference build or a runtime trace). Reading the disassembly shows the duplicate-species and duplicate-item loops, but not which facility or tier reaches them, and the same routine may also serve the Ring Challenge, which stays as it is; patching them blind could change rules that should stay. Not patched. The Battle Tower's No Restrictions tier already lifts the tier bans and Species Clause there.
- **Soul Dew (restore the Gen 3-6 +50 % Sp. Atk / Sp. Def, DESIGN-8 R8-3)**: Unbound ships CFRU's Gen 7 effect (1.2x Psychic/Dragon moves). CFRU sends the damage calculation to its own routine (battle command table `0x0899FFFC`, damage entry `0x089D3311`), so FireRed's Soul Dew gate in `CalculateBaseDamage` (whose bytes still match) is not on Unbound's damage path. The restore needs new code inside CFRU's damage routine (`[M8-8]`, not located to the byte), so it is left out.
- **Any ability**: CFRU stores PC Pokemon in a 58-byte compact record that drops PK3 bytes `0x1C-0x1F`, so the FireRed method (byte `0x1E`) would be lost on the first PC deposit. DESIGN-8's CFRU method (R8-6: an index in header byte `0x13` bits 3-7 into a per-save table of 31 abilities, with a hook at the entry of CFRU's ability getter `0x089C51B8`, reached through the veneer at `0x08040D38`) needs save space proven unused by a long run with a write watch (`[M8-5]`) and a census of every ability read (`[M8-22]`); both are runtime measurements, so it is left out.
- **Deoxys formes**: already separate species (and the owner asked to skip Deoxys formes).
