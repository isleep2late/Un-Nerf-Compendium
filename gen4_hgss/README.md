# HeartGold / SoulSilver Un-Nerf
**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**

Platinum's Un-Nerf set carried over to HeartGold and SoulSilver as in-place edits on the retail game. Every
change was found in the pret `pokeheartgold` decompilation, whose build reproduces both dumps byte for byte,
and was written only where the retail bytes matched what the decompilation says should be there. RAM layout,
save format and every address outside the edited instructions stay as retail, so cheats and save editing keep
working. Nobody has booted these files yet: treat them as a beta and report what misbehaves.

## Files

| file | input | output |
|---|---|---|
| `HeartGold_UnNerf.xdelta` | HeartGold (clean) | HeartGold Un-Nerf |
| `SoulSilver_UnNerf.xdelta` | SoulSilver (clean) | SoulSilver Un-Nerf |
| `bps/HeartGold_UnNerf.bps`, `bps/HeartGold_UnNerf.nocomp.xdelta` | HeartGold (clean) | HeartGold Un-Nerf |
| `bps/SoulSilver_UnNerf.bps`, `bps/SoulSilver_UnNerf.nocomp.xdelta` | SoulSilver (clean) | SoulSilver Un-Nerf |

The `.xdelta` files use xdelta3's default secondary compression; `bps/` has the same targets as BPS (keyed to
the clean dump's CRC32, so a wrong ROM is refused) and as VCDIFF without secondary compression.

## Exact inputs and outputs

| | file name | size | SHA-1 | CRC32 |
|---|---|---|---|---|
| HeartGold input | `Pokemon - HeartGold Version (USA).nds` | 134217728 | `4fcded0e2713dc03929845de631d0932ea2b5a37` | `C180A0E9` |
| HeartGold output | `Pokemon - HeartGold Version (USA) [Un-Nerf UNTESTED].nds` | 134217728 | `93f950590eff7bb492c10853d87998b1d668399e` | `D46A71ED` |
| SoulSilver input | `Pokemon - SoulSilver Version (USA).nds` | 134217728 | `f8dc38ea20c17541a43b58c5e6d18c1732c7e582` | `D8EA6090` |
| SoulSilver output | `Pokemon - SoulSilver Version (USA) [Un-Nerf UNTESTED].nds` | 134217728 | `587ecbb100f4e92528d3aa6e3a4b2f4ac7ded8f0` | `37C7A6BF` |

The inputs are the USA retail dumps (the same images pret `pokeheartgold` builds). Check yours with
`sha1sum` before patching; any other revision or region is refused.

## How to apply

```
xdelta3 -d -s "Pokemon - HeartGold Version (USA).nds" HeartGold_UnNerf.xdelta "Pokemon - HeartGold Version (USA) [Un-Nerf UNTESTED].nds"
xdelta3 -d -s "Pokemon - SoulSilver Version (USA).nds" SoulSilver_UnNerf.xdelta "Pokemon - SoulSilver Version (USA) [Un-Nerf UNTESTED].nds"
flips --apply bps/HeartGold_UnNerf.bps "Pokemon - HeartGold Version (USA).nds" "Pokemon - HeartGold Version (USA) [Un-Nerf UNTESTED].nds"
flips --apply bps/SoulSilver_UnNerf.bps "Pokemon - SoulSilver Version (USA).nds" "Pokemon - SoulSilver Version (USA) [Un-Nerf UNTESTED].nds"
```

Delta Patcher, xdelta UI, Floating IPS or Rom Patcher JS do the same. Patch a ROM file before you boot it;
these are not live (mid-game) patches. Your save keeps working: nothing in the save layout changes.

## What changes

Addresses are RAM addresses in the decompressed ARM9 (`main`), overlay 1 (`field`) and overlay 12 (battle);
HeartGold and SoulSilver use the same addresses for every edit.

**Battle Frontier ban list (all five facilities).** The five Frontier callers of
`IsPokemonBannedFromBattleFrontier` get "not banned": `PartyIsValidForFrontier` (Tower, Castle, Arcade
registration, 0x0204A62E), `BattleHall_DoesPartyContainEligibleMons` (Hall, 0x0204F0F6) and the party-menu
selection checks for the Tower (0x0207B3AC), Hall (0x0207B438) and Castle/Arcade (0x0207B4C0). Mewtwo, Mew,
Lugia, Ho-Oh, Celebi, Kyogre, Groudon, Rayquaza, Jirachi, Deoxys, Dialga, Palkia, Giratina, Phione, Manaphy,
Darkrai, Shaymin, Arceus and Spiky-eared Pichu can all enter. The function itself is untouched, so the link and
Wi-Fi rulesets that call it (Ubers clause, two-legendary limit) work as in retail.

**Species Clause and Item Clause.** `PartyIsValidForFrontier` no longer drops a Pokemon for a duplicate item
(0x0204A63A) and no longer requires a unique species/item team (0x0204A684); the Tower's registered-team check
`FrontierFieldSystem_PartyHasDuplicateSpeciesOrItems` returns 0 (0x0204AB10); the party-menu confirm checks for
the Tower (`sub_0207BA78`, species and items) and Castle/Arcade (`sub_0207BB88`, species) return 0.

**Soul Dew.** The two `BATTLE_TYPE_FRONTIER` gates in `CalcMoveDamage` are gone (0x0225736C attacker,
0x0225739A defender), so Latias/Latios holding the Soul Dew get +50 % Sp. Atk and Sp. Def in the Frontier too.
The link-battle Soul Dew rule is a Wi-Fi ruleset entry and stays.

**Arceus form-typing.** An Arceus with Multitype and no Plate takes the type of its form (form id = type id,
including the ??? form 9) instead of turning Normal; with a Plate it behaves as in retail.
`GetArceusTypeByHeldItemEffect` (0x02071C28) is rewritten as a 16-entry table lookup that keeps its old entry
and adds a second entry (0x02071C2A) that returns a fallback instead of Normal when no Plate is held; its unused
tail is zero-filled. Callers: the party/PC `TYPE_1`/`TYPE_2` getter (`GetBoxMonDataInternal`, 0x0206EBF0,
fallback = the form), `BoxMon_UpdateArceusForm` (0x02071C00, the form is rewritten only when a Plate is held, so
viewing the party or moving items no longer resets a PKHaX-set form), the battle form check
(`Battler_CheckWeatherFormChange`, 0x02256CE2, battle form re-derived only with a Plate) and `Battler_GetType`
(0x02258570, no Plate keeps the battler's own type instead of Normal). Taking a Plate away leaves Arceus in the
Plate's form, as in the Platinum set.

**Forme persistence (Giratina-O, Shaymin-Sky, Rotom).** `BoxMon_UpdateGiratinaForm` sets Origin when the
Griseous Orb is held and otherwise leaves the form alone (0x02071CDC); Giratina-O no longer reverts in battle
without the Orb (0x02256D76; it still reverts if it Transformed, as in retail); `BoxMon_UpdateShayminForm`
ignores requests for Land Forme, which covers night, the PC, the Day Care and trades (0x02071D92; the Gracidea
still turns a Land Shaymin into Sky); the battle subscript that turns a frozen Shaymin-Sky back
(`a/0/0/1` member 293, `CheckShayminForm`) ends at once (one word, 0x20 -> 0xE0); the Day Care
(`ScrCmd_DaycareSanitizeMon`, 0x0204D040) and the pre-communication party check (`ScrCmd_CommSanitizeParty`,
overlay 1 0x022013D4) no longer reset Giratina, Rotom or Shaymin forms (the Griseous Orb is still moved to the
bag there). Once Origin or Sky, a Pokemon stays that way until you change it in PKHaX.

**How the files are stored.** The ARM9 and overlays 1 and 12 are compressed on the cartridge. They were
decompressed, edited and recompressed with ndspy 4.2.0, and each result was checked twice: decompressed by
ndspy, and decompressed in place the way the game's own loader does it, both equal to the edited code. The
ARM9 and overlay 1 keep their exact retail size (unused footer padding fills the difference), so the header,
secure area and header CRC are unchanged; overlay 12 is 4 bytes longer in both games and stays at its retail
ROM offset inside its own 0x200-byte slot, with its FAT end and overlay-table size updated. No file moved.

## Skipped, and why

| feature | why |
|---|---|
| 6-Pokemon Battle Tower singles | out of scope for this beta (stream S8-22b); needs the struct-tail widening that is not built yet |
| AbilityLock | Platinum's AbilityLock source patch is not in the Compendium and its location was never found, so there is nothing to carry over |
| Platinum's "Arceus doubles" change | no source and no HG/SS analogue identified; two Arceus in one team already work because the Species Clause is gone |
| Level cap | nothing to lift: HG/SS have no Frontier entry cap (the Tower, Castle and Arcade party menus allow Lv 100). The Lv 50 rule is a scaling rule (Pokemon above 50 fight at 50), kept as the Black 2/White 2 set keeps it |
| Battle Hall Lv 30 minimum and same-species pair | not clauses: the Hall scales opponents from your Pokemon's level and keeps its records per species, so these stay |
| Link/Wi-Fi rulesets | the Soul Dew rule, Ubers clause and two-legendary limit of the link rulesets are not Frontier rules and stay as retail |
| Receptionist's list of banned Pokemon | text only; it can still name legendaries when you have too few eligible Pokemon for other reasons |
