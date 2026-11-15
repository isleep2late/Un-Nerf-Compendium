# Diamond / Pearl
**UNTESTED BETA (built 2026-10-23 with no in-game test; the owner asked for an untested beta first).**

The Platinum Un-Nerf set (`../gen4_platinum/`) carried over to Pokemon Diamond and Pokemon Pearl (USA, Rev 5).
Nothing here has been booted or played: each patch was only re-applied to its clean dump and compared byte for
byte with the built ROM.

Unlike the Platinum files, which are a rebuilt decomp ROM, these are **in-place edits of the retail image**: 15
small sites in the ARM9, overlay 6 (the Battle Tower field code) and overlay 11 (the battle engine), written as
hand-assembled Thumb code at the retail addresses. The addresses and original bytes come from the pret
`pokediamond` decompilation, whose build is byte-identical to both dumps (SHA-1 `a46233d8...` and `99083bf1...`).
No function, table or file moves, the header and the secure area are untouched (so the header CRC stays valid),
and the file stays 64 MiB. Diamond and Pearl share every address except the three overlay 6 sites, which sit
0xC bytes later in Pearl.

## What changes

1. **Ban list removed.** `IsPokemonLegendaryOrMythical` (ARM9 `0x0206A964`) now returns FALSE. It is the
   only reader of the 18-entry list (Mewtwo, Mew, Ho-Oh, Lugia, Celebi, Kyogre, Groudon, Rayquaza, Jirachi,
   Deoxys, Dialga, Palkia, Giratina, Phione, Manaphy, Darkrai, Shaymin, Arceus), so the Battle Tower entry count
   (overlay 6), the Tower party menu and the battle-rule check all accept those Pokemon. Platinum's set empties
   the same list; here the list stays and the overlay 6 message builder that names the banned species you have
   seen names none of them instead (one branch), so no message calls an allowed species banned.
2. **Species Clause and Item Clause off.** Five sites: the Tower party menu's duplicate check (ARM9
   `0x0207174C`, returns "no duplicate"), the Tower's re-check of the registered team (overlay 6, returns "no
   duplicate"), the pairwise species/item test inside the Tower's entry pre-check (overlay 6, returns "all
   distinct", the same edit the Platinum full build makes to its copy), and the species-unique and item-unique
   rules of the battle-rule party validator (ARM9 `0x0206BC60`, both skipped, as in Platinum).
3. **Level rules off** in the battle-rule validator, as in the Platinum full build: the total-level cap
   (`0x0206BD20`) and the per-Pokemon maximum level (`0x0206BBB8`) are skipped. The Battle Tower's own level
   handling is not changed (Platinum's set does not change its Tower's either).
4. **Soul Dew un-nerfed.** The two `BATTLE_TYPE_TOWER` tests in the damage calculation (overlay 11
   `0x0224B684` attacker, `0x0224B6B2` defender) are removed, so Latios/Latias holding the Soul Dew get the
   Gen 3-6 x1.5 Sp. Atk and Sp. Def inside the Battle Tower too.
5. **Arceus form typing** (Platinum's four edits, including the Gen 4 `???` form, type 9):
   * the type the Pokemon data reports (summary and party screens and every other reader outside battle) is
     Arceus's stored form, not the held Plate (ARM9 `0x020677E6`, inside `GetBoxMonDataInternal`);
   * the form is rewritten only while a real Plate is held, so a form set in a save editor is no longer reset
     to Normal when the party or box is opened (ARM9 `0x02069F08`, `BoxMon_UpdateArceusForm`);
   * the battle-start Multitype form change also runs only with a Plate held (overlay 11 `0x0224B1F0`);
   * in battle, a Multitype Arceus with no Plate takes its form's type instead of Normal (overlay 11
     `0x0224C792`; the plate-to-type table is now read through the game's own ARM9 function, which holds the
     same 16 entries).

| site | region | function (pokediamond) | Diamond address | Pearl address | bytes |
|---|---|---|---|---|---|
| ban-1 | ARM9 | `IsPokemonLegendaryOrMythical` | `0x0206A964` | `0x0206A964` | 4 |
| ban-2 | overlay 6 | `ov06_022441F4` (banned-species message) | `0x02244276` | `0x02244282` | 2 |
| clause-1 | ARM9 | `sub_0207174C` (party menu, Tower mode) | `0x0207174C` | `0x0207174C` | 4 |
| clause-2 | overlay 6 | `ov06_02243780` (registered-team re-check) | `0x02243780` | `0x0224378C` | 4 |
| clause-3 | ARM9 | `sub_0206BC60` species-unique rule | `0x0206BD36` | `0x0206BD36` | 2 |
| clause-4 | ARM9 | `sub_0206BC60` item-unique rule | `0x0206BD7A` | `0x0206BD7A` | 2 |
| clause-5 | overlay 6 | `ov06_02243184` (entry pre-check, pairwise test) | `0x02243184` | `0x02243190` | 4 |
| level-1 | ARM9 | `sub_0206BC60` total-level rule | `0x0206BD20` | `0x0206BD20` | 2 |
| level-2 | ARM9 | `sub_0206BB84` max-level rule | `0x0206BBB8` | `0x0206BBB8` | 2 |
| souldew-1 | overlay 11 | `ov11_0224B310` (damage calculation) | `0x0224B684` | `0x0224B684` | 2 |
| souldew-2 | overlay 11 | `ov11_0224B310` | `0x0224B6B2` | `0x0224B6B2` | 2 |
| arceus-1 | ARM9 | `GetBoxMonDataInternal` | `0x020677E6` | `0x020677E6` | 20 |
| arceus-2 | ARM9 | `BoxMon_UpdateArceusForm` | `0x02069F08` | `0x02069F08` | 8 |
| arceus-3 | overlay 11 | `ov11_0224AE78` (battle form change) | `0x0224B1F0` | `0x0224B1F0` | 8 |
| arceus-4 | overlay 11 | `ov11_0224C740` (battler type) | `0x0224C792` | `0x0224C792` | 114 |

## Files

| file | what | apply with |
|---|---|---|
| `Diamond_UnNerf.xdelta` | VCDIFF (xdelta3 3.0.11, `-9 -S djw`), clean Diamond -> patched Diamond | `xdelta3 -d -s "<clean Diamond>.nds" Diamond_UnNerf.xdelta out.nds` |
| `Pearl_UnNerf.xdelta` | the same for Pearl | `xdelta3 -d -s "<clean Pearl>.nds" Pearl_UnNerf.xdelta out.nds` |
| `bps/Diamond_UnNerf.bps` | BPS, no metadata, same source and target as the xdelta | `flips --apply bps/Diamond_UnNerf.bps "<clean Diamond>.nds" out.nds` |
| `bps/Pearl_UnNerf.bps` | the same for Pearl | `flips --apply bps/Pearl_UnNerf.bps "<clean Pearl>.nds" out.nds` |
| `dp_unnerf.py` | all 15 sites on your own `.nds`, Python 3 standard library only | `python3 dp_unnerf.py game.nds` |

`dp_unnerf.py` reads the game code (`ADAE` Diamond, `APAE` Pearl) and the revision byte (Rev 5 only), finds
each site through the ROM's own header, FAT and overlay table, checks the original bytes of every site (by
SHA-1) before writing anything, and writes `<input name> [Un-Nerf UNTESTED].nds` next to the input (or
`--out <file>`). It never overwrites its input and refuses a ROM that is already patched. With the clean dump
it gives exactly the patch target. To keep some features and drop others, pass a subset:
`--features banlist,clauses,levelcap,souldew,arceus` (all five by default); `--list` prints the sites.

## Input and output

| | file | size | SHA-1 | CRC32 |
|---|---|---|---|---|
| Diamond input | `Pokemon - Diamond Version (USA) (Rev 5).nds` | 67108864 | `a46233d8b79a69ea87aa295a0efad5237d02841e` | `84427823` |
| Diamond output | `Pokemon - Diamond Version (USA) (Rev 5) [Un-Nerf UNTESTED].nds` | 67108864 | `382215d7a3a4af40ffdddafc9d226d48711cc97c` | `862DFC69` |
| Pearl input | `Pokemon - Pearl Version (USA) (Rev 5).nds` | 67108864 | `99083bf15ec7c6b81b4ba241ee10abd9e80999ac` | `E2D87EBF` |
| Pearl output | `Pokemon - Pearl Version (USA) (Rev 5) [Un-Nerf UNTESTED].nds` | 67108864 | `d625d95f69e43f278f6141c37694c6a3ce0d5a02` | `D24CD1EA` |

The BPS files refuse any other source CRC32. Diamond and Pearl patches are not interchangeable, and earlier
revisions of either game are not supported.

## Not in this set

* **6-Pokemon Tower:** Platinum's 1-6 party widens Battle Tower arrays and cannot be an in-place edit; it is a
  later step. The Tower keeps its retail team sizes, and the battle-rule team-size check is unchanged.
* **Forme persistence** (Giratina-Origin, Shaymin-Sky, Rotom appliance formes): not applicable, those formes do
  not exist in Diamond / Pearl.
* **AbilityLock:** the Platinum build carries it, but its source is not in the Compendium and where it sits in the
  Platinum build has not been established, so there is nothing to carry over yet.
* **"ArceusDoubles":** named in the root README for Platinum, but no source and no located change exists for it,
  so it is not mapped.
* **Other battle-rule restrictions:** the Platinum full build also drops the evolution, height and weight rules of
  its rule validator; those are not part of the Diamond / Pearl feature list and stay as they are.
* **Tower entry count's same-species-and-same-item filter:** unchanged, as in the Platinum full build; it only
  matters for a party whose Pokemon are all the same species holding the same item.
