# HeartGold / SoulSilver Un-Nerf
**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**

Platinum's Un-Nerf set carried over to HeartGold and SoulSilver as in-place edits on the retail game. Every
change was found in the pret `pokeheartgold` decompilation, whose build reproduces both dumps byte for byte,
and is written only where the retail bytes match what the decompilation says should be there. RAM layout,
save format and every address outside the edited instructions stay as retail, so cheats and save editing keep
working. Nobody has booted the result yet: treat it as a beta and report what misbehaves.

## Files

| file | what |
|---|---|
| `hgss_unnerf.py` | applies all 23 edits to your own clean HeartGold or SoulSilver dump; Python 3 and ndspy 4.2.0 |
| `README.md` | this file |

There is no `.xdelta`, `.bps` or `.ips` here; see the next section.

## Why there is no xdelta or BPS

On the HG/SS cartridge the ARM9 and nearly all overlays (127 of 129, including 1 and 12) are stored compressed.
Changing even one instruction inside them re-encodes and shifts most of the compressed stream, so a whole-ROM patch (xdelta, BPS or IPS) for these
edits has to carry about 320-430 KB of recompressed retail ARM9 and overlay 1/12 code.
That is the game's own code, and the Compendium does not distribute game code ("Nothing copyrighted is
distributed here"). The script does the same work on your machine instead, the way `../gen4_dp/dp_unnerf.py`
and `../hacks/gen4_platinum/frp-2.0/frp_unnerf.py` do: it holds only the addresses, a SHA-1 of the original
bytes at each address, and the new bytes (new Thumb code written for this patch), and it rebuilds the
compressed code from your dump. With ndspy 4.2.0 its output is byte for byte the Un-Nerf build (SHA-1s below).

## How to run

Requirements: Python 3.7 or newer and ndspy 4.2.0 (the library the `gen5_bw2` tools already use).

```
pip install ndspy==4.2.0
python3 hgss_unnerf.py "Pokemon - HeartGold Version (USA).nds"
python3 hgss_unnerf.py "Pokemon - SoulSilver Version (USA).nds"
```

On Windows use `py -3` instead of `python3`. Each run takes about 10 seconds on a desktop PC and writes
`<input name> [Un-Nerf UNTESTED].nds` next to the input (`--out <file>` picks another name). It never
overwrites its input, and it prints the output SHA-1 and whether it matches the table below.

What the script does, in order:

1. Reads the game code (`IPKE` HeartGold, `IPGE` SoulSilver) and the revision byte (0), and checks the SHA-1 of
   the whole dump against the clean one below. Any other dump is refused (`--allow-other-sha1` tries anyway; every
   site is still checked); a ROM that is already patched is refused.
2. Decompresses the ARM9, overlay 1 (`field`) and overlay 12 (battle) with ndspy, and a second time in place the
   way the game's own loader does it; both must agree.
3. Checks the original bytes at every edit site (by SHA-1). If any site differs, nothing is written.
4. Writes the edits into the decompressed code, and one word into the battle-subscript archive `a/0/0/1`
   (member 293, which is not compressed).
5. Recompresses with ndspy 4.2.0 and puts each module back where it was. The ARM9 and overlay 1 keep their exact
   retail size: the unused room is taken up by extra `0xFF` padding inside the compression footer, so the header,
   secure area, header CRC and the ARM9's own size fields are unchanged. Overlay 12 comes out 4 bytes longer in
   both games; it stays at its retail ROM offset and grows into the free `0xFF` padding of its own 0x200-byte
   slot, and its FAT end and overlay-table compressed size are updated. No file moves. Every new stream is
   decompressed again both ways and must equal the edited code before anything is written.

`--list` prints every site. `--features banlist,clauses,souldew,arceus,formes` (all five by default) keeps some
features and drops others; a partial set gives a different SHA-1 than the table below. All 31 feature sets were
tried on both games and each fits in place; if a set ever did not fit, the script says so and writes nothing.

Patch the ROM file before you boot it; this is not a live (mid-game) patch. Your save keeps working: nothing in
the save layout changes.

## Exact inputs and outputs

| | file name | size | SHA-1 | CRC32 |
|---|---|---|---|---|
| HeartGold input | `Pokemon - HeartGold Version (USA).nds` | 134217728 | `4fcded0e2713dc03929845de631d0932ea2b5a37` | `C180A0E9` |
| HeartGold output | `Pokemon - HeartGold Version (USA) [Un-Nerf UNTESTED].nds` | 134217728 | `93f950590eff7bb492c10853d87998b1d668399e` | `D46A71ED` |
| SoulSilver input | `Pokemon - SoulSilver Version (USA).nds` | 134217728 | `f8dc38ea20c17541a43b58c5e6d18c1732c7e582` | `D8EA6090` |
| SoulSilver output | `Pokemon - SoulSilver Version (USA) [Un-Nerf UNTESTED].nds` | 134217728 | `587ecbb100f4e92528d3aa6e3a4b2f4ac7ded8f0` | `37C7A6BF` |

The inputs are the USA retail dumps (the same images pret `pokeheartgold` builds). Check yours with `sha1sum`
before patching. The output SHA-1s hold for ndspy 4.2.0; another ndspy version may compress differently, which
gives a different but still checked file, and the script prints a note when the installed version is not 4.2.0.

## What changes

Addresses are RAM addresses in the decompressed ARM9 (`main`), overlay 1 (`field`) and overlay 12 (battle);
HeartGold and SoulSilver use the same addresses and the same bytes for every edit.

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
tail (0x02071C50-0x02071C9E) is zero-filled. Callers: the party/PC `TYPE_1`/`TYPE_2` getter
(`GetBoxMonDataInternal`, 0x0206EBF0, fallback = the form), `BoxMon_UpdateArceusForm` (0x02071C00, the form is
rewritten only when a Plate is held, so viewing the party or moving items no longer resets a PKHaX-set form), the
battle form check (`Battler_CheckWeatherFormChange`, 0x02256CE2, battle form re-derived only with a Plate) and
`Battler_GetType` (0x02258570, no Plate keeps the battler's own type instead of Normal). Taking a Plate away
leaves Arceus in the Plate's form, as in the Platinum set. The five Arceus sites belong together (the callers
use the new entry), so `--features` treats them as one feature.

**Forme persistence (Giratina-O, Shaymin-Sky, Rotom).** `BoxMon_UpdateGiratinaForm` sets Origin when the
Griseous Orb is held and otherwise leaves the form alone (0x02071CDC); Giratina-O no longer reverts in battle
without the Orb (0x02256D76; it still reverts if it Transformed, as in retail); `BoxMon_UpdateShayminForm`
ignores requests for Land Forme, which covers night, the PC, the Day Care and trades (0x02071D92; the Gracidea
still turns a Land Shaymin into Sky); the battle subscript that turns a frozen Shaymin-Sky back
(`a/0/0/1` member 293, `CheckShayminForm`) ends at once (one word, 0x20 -> 0xE0); the Day Care
(`ScrCmd_DaycareSanitizeMon`, 0x0204D040) and the pre-communication party check (`ScrCmd_CommSanitizeParty`,
overlay 1 0x022013D4) no longer reset Giratina, Rotom or Shaymin forms (the Griseous Orb is still moved to the
bag there). Once Origin or Sky, a Pokemon stays that way until you change it in PKHaX.

| site | feature | region | function (pokeheartgold) | address | bytes | new code |
|---|---|---|---|---|---|---|
| ban-1 | banlist | ARM9 | `PartyIsValidForFrontier` | `0x0204A62E` | 4 | `movs r0,#0` instead of the ban-list call |
| ban-2 | banlist | ARM9 | `BattleHall_DoesPartyContainEligibleMons` | `0x0204F0F6` | 4 | the same |
| ban-3 | banlist | ARM9 | `sub_0207B364` (Tower party menu) | `0x0207B3AC` | 4 | the same |
| ban-4 | banlist | ARM9 | `sub_0207B418` (Hall party menu) | `0x0207B438` | 4 | the same |
| ban-5 | banlist | ARM9 | `sub_0207B4A0` (Castle/Arcade party menu) | `0x0207B4C0` | 4 | the same |
| clause-1 | clauses | ARM9 | `PartyIsValidForFrontier` | `0x0204A63A` | 2 | never drops a Pokemon for a duplicate item |
| clause-2 | clauses | ARM9 | `PartyIsValidForFrontier` | `0x0204A684` | 4 | `SpeciesAndItem_ValidateAllUnique` -> TRUE |
| clause-3 | clauses | ARM9 | `FrontierFieldSystem_PartyHasDuplicateSpeciesOrItems` | `0x0204AB10` | 4 | returns 0 |
| clause-4 | clauses | ARM9 | `sub_0207BA78` (Tower confirm) | `0x0207BA78` | 4 | returns 0 |
| clause-5 | clauses | ARM9 | `sub_0207BB88` (Castle/Arcade confirm) | `0x0207BB88` | 4 | returns 0 |
| souldew-1 | souldew | overlay 12 | `CalcMoveDamage` (attacker) | `0x0225736C` | 2 | Frontier gate removed |
| souldew-2 | souldew | overlay 12 | `CalcMoveDamage` (defender) | `0x0225739A` | 2 | Frontier gate removed |
| arceus-1 | arceus | ARM9 | `GetArceusTypeByHeldItemEffect` | `0x02071C28` | 118 | table lookup with a fallback entry; tail zero-filled |
| arceus-2 | arceus | ARM9 | `BoxMon_UpdateArceusForm` | `0x02071C00` | 32 | form set only when a Plate is held |
| arceus-3 | arceus | ARM9 | `GetBoxMonDataInternal` | `0x0206EBF0` | 8 | type from the form when no Plate |
| arceus-4 | arceus | overlay 12 | `Battler_CheckWeatherFormChange` | `0x02256CE2` | 30 | battle form changed only with a Plate |
| arceus-5 | arceus | overlay 12 | `Battler_GetType` | `0x02258570` | 2 | no Plate keeps the battler's type |
| forme-1 | formes | ARM9 | `BoxMon_UpdateGiratinaForm` | `0x02071CDC` | 2 | no Orb: form kept |
| forme-2 | formes | ARM9 | `BoxMon_UpdateShayminForm` | `0x02071D92` | 10 | Land Forme requests ignored |
| forme-3 | formes | overlay 12 | `Battler_CheckWeatherFormChange` | `0x02256D76` | 2 | Giratina-O kept in battle without the Orb |
| forme-4 | formes | ARM9 | `ScrCmd_DaycareSanitizeMon` | `0x0204D040` | 2 | form reset skipped |
| forme-5 | formes | overlay 1 | `ScrCmd_CommSanitizeParty` | `0x022013D4` | 2 | form reset skipped |
| forme-6 | formes | `a/0/0/1` member 293 | `CheckShayminForm` battle subscript | word 0 | 4 | `0x20` -> `0xE0`, the subscript ends at once |

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
