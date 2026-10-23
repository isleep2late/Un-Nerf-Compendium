**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**
# Following Renegade Platinum v2.0

The Platinum Un-Nerf set (`gen4_platinum/`) carried over to Pokemon Following Renegade Platinum v2.0. Nothing here
has been booted or played: each patch was only re-applied to the plain hack and compared byte for byte with the
built ROM.

Following Renegade Platinum is a binary hack of US Platinum (rev 0) that leaves every function the Platinum Un-Nerf
touches exactly as it is in the clean dump (checked site by site before anything was written). The Compendium's
Platinum files are a rebuilt decomp ROM (every overlay moved by 0x3C0 bytes), so they cannot be applied to the hack.
These patches are **in-place edits at the retail addresses** instead: each function that the Compendium's full
build (the `Platinum_UnNerf_Full.xdelta` output, SHA-1 `15f1441d...`) changes was compared with its clean rev 0
version, using the pret `pokeplatinum` symbols (its build is byte-identical to the clean dump), and the same change
was written as hand-assembled Thumb code at the retail address. 32 sites in the ARM9 and in overlays 5 (field
scripts), 16 (battle), 88 (trade) and 94 (GTS) change 399 bytes. No function, table or file moves, the header and
the secure area are untouched (so the header CRC stays valid), and the file stays 128 MiB.

## What changes

1. **Ban list removed.** `Pokemon_IsOnBattleFrontierBanlist` (ARM9 `0x02078804`) returns FALSE, so the Battle Tower,
   Hall, Castle and Arcade entry checks, the party menu and the battle-rule check accept Mewtwo, Mew, Lugia, Ho-Oh,
   Celebi, Kyogre, Groudon, Rayquaza, Jirachi, Deoxys, Dialga, Palkia, Giratina, Phione, Manaphy, Darkrai, Shaymin
   and Arceus. The 18-entry list itself stays; the reception message that names the banned species you have seen
   names none of them instead (one branch). The full build empties the list instead; an empty entry makes the
   Pokedex check behind that message raise the game's assertion, which resets the game while a link session is
   open, so the list is kept here. In single player both give the same message.
2. **Species Clause and Item Clause off.** The Battle Tower party menu's duplicate species/item check, the Battle
   Hall's "must use the same kind of Pokemon" rule and the Battle Castle's species clause (all three return "no
   problem", as in the full build), the Tower's re-check of the registered team, and the species-unique and item-unique rules of the
   battle-rule party validator.
3. **Level rules off** in the battle-rule validator: the total-level cap and the per-Pokemon maximum level, as in the
   full build. The Frontier's own level scaling is not changed.
4. **Other battle-rule restrictions off** (feature `rules`): the full build's validator keeps only the Egg check, so
   the evolution, height, weight and special-species rules of rule sets are skipped too. Leave it out with
   `--features` if you want link cups to keep those rules.
5. **Soul Dew un-nerfed.** The two `BATTLE_TYPE_FRONTIER` tests in the damage calculation (overlay 16 `0x0225A5F0`
   attacker, `0x0225A61E` defender, the sites the Compendium README names) are removed, so Latios/Latias holding the
   Soul Dew get x1.5 Sp. Atk and Sp. Def inside the Battle Frontier too.
6. **Arceus form typing** (Platinum's four edits):
   * the type the Pokemon data reports outside battle is a Multitype Arceus's stored form, not the held Plate;
   * the form is rewritten only while a real Plate is held, so a form set in a save editor survives opening the
     party or a box;
   * the battle-start Multitype form change runs only with a Plate held;
   * in battle, a Multitype Arceus with no Plate takes its form's type instead of Normal (the plate-to-type table is
     read through the game's own ARM9 function, which holds the same 16 entries as the battle copy it replaces).

   Following Renegade Platinum turns type 9 into Fairy, and form id equals type id, so an Arceus in form 9 is a
   Fairy-type Arceus here. The hack's own type handling is kept as it is.
7. **Forme persistence** (Giratina-Origin, Shaymin-Sky, Rotom appliance formes), as in the full build:
   * Giratina-Origin no longer reverts in battle without the Griseous Orb (it still reverts when it is a Transform
     copy), and taking the Orb away no longer turns it back to Altered (giving the Orb still makes it Origin);
   * the two field script commands that take the Griseous Orb and reset Giratina, Rotom and Shaymin (one Pokemon,
     or the whole party) only clear their result variable;
   * Shaymin-Sky no longer reverts at night or when the game is loaded, and keeps its form when deposited in the PC,
     traded, or deposited at the GTS.
8. **AbilityLock.** The Compendium has no source for it; in the full build it is a helper that tests whether a
   Pokemon's Ability is one of its current form's natural Abilities, called by the four form setters (Giratina by
   held item, Giratina-Origin, Shaymin, Rotom): the Ability is recomputed after a form change only if it was a
   natural one, so an Ability set in a save editor is kept. Here the same test and two small form-setting wrappers
   (148 bytes) sit in the body of the Tower party-menu duplicate check that the clauses feature switches off, so
   `abilitylock` needs `clauses`; each setter calls the wrapper instead of setting the form and recomputing the
   Ability itself.

| site | feature | region | function (pokeplatinum) | address | bytes |
|---|---|---|---|---|---|
| ban-1 | banlist | ARM9 | `Pokemon_IsOnBattleFrontierBanlist` | `0x02078804` | 4 |
| ban-2 | banlist | ARM9 | `BattleFrontier_MakeSeenBanlistSpeciesMsg` | `0x0204AF50` | 2 |
| clause-1 | clauses | ARM9 | `CheckDuplicateValues` (Tower party menu) | `0x020808CC` | 4 |
| clause-2 | clauses | ARM9 | `CheckUniqueValues` (Hall party menu) | `0x02080968` | 4 |
| clause-3 | clauses | ARM9 | `CheckEqualityInArray` (Castle party menu) | `0x020809DC` | 4 |
| clause-4 | clauses | ARM9 | `BattleTower_CheckDuplicateSpeciesAndHeldItems` | `0x0204A410` | 4 |
| clause-5 | clauses | ARM9 | `BattleRegulation_ValidatePartySelection` species rule | `0x0207A478` | 2 |
| clause-6 | clauses | ARM9 | `BattleRegulation_ValidatePartySelection` item rule | `0x0207A4BC` | 2 |
| level-1 | levelcap | ARM9 | `BattleRegulation_ValidatePartySelection` total level | `0x0207A460` | 2 |
| level-2 | levelcap | ARM9 | `BattleRegulation_ValidatePokemon` max level | `0x0207A2EE` | 2 |
| rules-1 | rules | ARM9 | `BattleRegulation_ValidatePokemon` other rules | `0x0207A31C` | 4 |
| souldew-1 | souldew | overlay 16 | `BattleSystem_CalcMoveDamage` | `0x0225A5F0` | 2 |
| souldew-2 | souldew | overlay 16 | `BattleSystem_CalcMoveDamage` | `0x0225A61E` | 2 |
| arceus-1 | arceus | ARM9 | `BoxPokemon_GetDataInternal` | `0x02074AF2` | 20 |
| arceus-2 | arceus | ARM9 | `BoxPokemon_SetArceusForm` | `0x0207796C` | 8 |
| arceus-3 | arceus | overlay 16 | `BattleSystem_TriggerFormChange` | `0x02259F6A` | 8 |
| arceus-4 | arceus | overlay 16 | `Battler_MonType` | `0x0225B786` | 116 |
| forme-1 | formes | overlay 16 | `BattleSystem_TriggerFormChange` (Giratina) | `0x02259FDE` | 2 |
| forme-2 | formes | overlay 5 | `ScrCmd_TryRevertPokemonForm` | `0x021F6DC2` | 2 |
| forme-3 | formes | overlay 5 | `ScrCmd_TryRevertPartyPokemonForms` | `0x021F6C9A` | 2 |
| forme-4 | formes | ARM9 | `BoxPokemon_SetGiratinaForm` | `0x02077A3C` | 2 |
| forme-5 | formes | ARM9 | `Party_SetShayminLandForm` | `0x02077BC0` | 2 |
| forme-6 | formes | ARM9 | `PCBoxes_TryStoreBoxMonInBox` | `0x020798B4` | 4 |
| forme-7 | formes | ARM9 | `PCBoxes_TryStoreBoxMonAt` | `0x02079928` | 4 |
| forme-8 | formes | overlay 88 | `ov88_0223B914` (trade) | `0x0223B9A8` | 4 |
| forme-9 | formes | overlay 94 | `ov94_022425A8` (GTS deposit, party) | `0x022425C4` | 4 |
| forme-10 | formes | overlay 94 | `ov94_022425A8` (GTS deposit, box) | `0x022425E4` | 4 |
| ability-0 | abilitylock | ARM9 | routine in the body of `CheckDuplicateValues` | `0x020808D0` | 148 |
| ability-1 | abilitylock | ARM9 | `BoxPokemon_SetGiratinaForm` | `0x02077A4C` | 10 |
| ability-2 | abilitylock | ARM9 | `Pokemon_SetGiratinaOriginForm` | `0x02077A82` | 10 |
| ability-3 | abilitylock | ARM9 | `BoxPokemon_SetShayminForm` | `0x02077B02` | 10 |
| ability-4 | abilitylock | ARM9 | `Pokemon_SetRotomForm` | `0x02077D02` | 10 |

## Files

| file | what | apply with |
|---|---|---|
| `FRP_UnNerf.xdelta` | VCDIFF (xdelta3 3.0.11, `-9 -S djw`), plain hack -> patched hack | `xdelta3 -d -s "<plain hack>.nds" FRP_UnNerf.xdelta out.nds` |
| `bps/FRP_UnNerf.bps` | BPS, no metadata, same source and target as the xdelta | `flips --apply bps/FRP_UnNerf.bps "<plain hack>.nds" out.nds` |
| `frp_unnerf.py` | all 32 sites on your own `.nds`, Python 3 standard library only | `python3 frp_unnerf.py "<plain hack>.nds"` |

`frp_unnerf.py` checks the game code (`CPUE`) and the SHA-1 of the input, finds each site through the ROM's own
header, overlay table and FAT, checks the original bytes of every site (by SHA-1) before writing anything, and writes
`<input name> [Un-Nerf UNTESTED].nds` next to the input (or `--out <file>`). It never overwrites its input and
refuses a ROM that is already patched. With the plain hack it gives exactly the patch target. To keep some features
and drop others, pass a subset: `--features banlist,clauses,levelcap,rules,souldew,arceus,formes,abilitylock` (all
eight by default; `abilitylock` needs `clauses`); `--list` prints the sites. `--allow-other-sha1` lets it try
another US Platinum-based ROM, still refusing unless every site holds the expected bytes.

## Input and output

| | file | size | SHA-1 | CRC32 |
|---|---|---|---|---|
| input | `Pokemon Following Renegade Platinum (v2.0) [Platinum].nds` (byte-identical to the release `Pokemon Following Renegade Platinum (v2.0).nds`) | 134217728 | `9fe37a855782265c8567d5d19aebe6a8ebae9bd6` | `3A23536C` |
| output | `Pokemon Following Renegade Platinum (v2.0) [Platinum] [Un-Nerf UNTESTED].nds` | 134217728 | `688f7b1c9e01fee19f2f719e4b2045afb23a28e7` | `49D4E4D3` |

The hack's base is `Pokemon - Platinum Version (USA).nds` (rev 0, SHA-1 `ce81046eda7d232513069519cb2085349896dec7`);
the patches are keyed to the plain hack, not to the clean game. The BPS refuses any other source CRC32 (clean
Platinum included).

## Not in this set

* **6-Pokemon Battle Tower and the flexible 1-6 team sizes:** they widen Battle Tower arrays on the heap and cannot be
  an in-place edit; they are a later step. Every facility keeps its retail team size, and the battle-rule team-size
  check is unchanged (the full build's "2 to 6 in doubles" rule belongs to that step).
* **"ArceusDoubles":** named in the Compendium's root README for Platinum, but there is no source for it and the full
  build holds no Arceus change beyond the four above, so nothing is mapped.
* **Arceus without Multitype:** the full build reports the form's type for any Arceus outside battle, whatever its
  Ability; here the published `platinum_arceus_formtype.patch` behaviour is kept (only a Multitype Arceus).
* **Shaymin in the Day Care and when frozen in battle:** these reverts are unchanged, as in the full build.
* **Trading caveat:** a Shaymin-Sky traded from this game keeps its Sky Forme, as in the full build; Diamond and
  Pearl have no Sky Forme.
