# Gen 2 - Pokemon Crystal: Battle Tower rules removed and auto-level, Pikachu Surf and Fly, New York moves
**UNTESTED BETA (build 4, 2026-11-23; no person has played a patched game yet).**

Status: **not yet played by a person**. These patches were built from the pret disassembly (pret/pokecrystal): the unmodified build
reproduces the clean dump byte for byte, the changes were made in the source, and the patch is the difference between
the two builds. What was checked: every patch re-applies to its stated input and gives the stated output byte for
byte; no existing code or data moved (new code sits in unused space at the end of a bank); the RAM and save layout are
identical to the clean game. An automated tester then played Gold and Crystal 1.0 in an emulator by key presses (Silver and Crystal 1.1 were only booted): saves both ways, Surf and Fly, the New York moves on wild Pokemon and hatched eggs, and in Crystal Battle Tower runs with legendaries, levelled to the room and restored after a win, a loss and quitting. Not played: link play. Keep a backup of your save before you try them, and please report what misbehaves.

- **Battle Tower, ban list removed**: Mewtwo, Mew, Lugia, Ho-Oh and Celebi may enter every room. (The clean game
  refuses the five in the level 10 to 60 rooms.)
- **Battle Tower, Species Clause and Item Clause removed**: two or three of the same Pokemon, or the same held item
  twice, are allowed.
- **Battle Tower, auto-level**: the level limit is gone, and for each battle your three Pokemon are set to the level
  of the room you chose (10 to 100) with their stats recalculated, up or down. The clean game already reloads your
  party from the save file after every Tower battle, so your Pokemon are back at their real levels afterwards and
  the changed levels are never saved. You no longer need Pokemon at exactly the room's level.
- Still as in the clean game: exactly three Pokemon, no eggs, and the rooms above level 40 appear after the Hall of
  Fame.
- **Pikachu and Raichu can learn Surf and Fly** from HM03 and HM02 (two bits in each one's TM/HM compatibility data).
  The clean game already shows the surfing Pikachu sprite when a Pikachu uses Surf.
- **Pokemon Center New York moves**: wild Pokemon and hatching eggs of the listed species have a 1 in 2 chance of
  knowing one of the moves that were only given out at the Pokemon Center New York (table below).

## Files

| File | For | What |
|---|---|---|
| `Crystal_UnNerf.ips` | Crystal 1.0 | everything above, IPS (615 bytes, 25 records) |
| `bps/Crystal_UnNerf.bps` | Crystal 1.0 | the same change as a BPS (checks the input CRC32) |
| `Crystal11_UnNerf.ips` | Crystal 1.1 | everything above, IPS (616 bytes, 25 records) |
| `bps/Crystal11_UnNerf.bps` | Crystal 1.1 | the same change as a BPS (checks the input CRC32) |

## Input (exact), how to apply, output

| Game | Clean input (No-Intro) | Input SHA-1 | Input CRC32 | Output SHA-1 | Output CRC32 |
|---|---|---|---|---|---|
| Crystal 1.0 | `Pokemon - Crystal Version (USA, Europe).gbc` | `f4cd194bdee0d04ca4eac29e09b8e4e9d818c133` | `ee6f5188` | `a978a288b70476645d687ed4cfb13eeac0fd8c65` | `e870d884` |
| Crystal 1.1 | `Pokemon - Crystal Version (USA, Europe) (Rev 1).gbc` | `f2f52230b536214ef7c9924f483392993e226cfb` | `3358e30a` | `2349027aacaeb846eb7e8695c49cb472fb53d15a` | `8b90fcde` |

Check your dump's SHA-1 first; other regions and revisions are not supported. The IPS files do not check the input; the
BPS files refuse a wrong input. Apply with Floating IPS, Lunar IPS, Rom Patcher JS or any IPS/BPS tool. The result must
have the output SHA-1 above. The ROM size is unchanged; the header checksums are already correct in the output.

Saves are compatible both ways with the clean game: nothing in the save file or in RAM was added or moved. 

## What each change does (ROM offsets; 1.0 = 1.1 unless both are given)

| Feature | Offset | Size | Where (pokecrystal) |
|---|---|---|---|
| Ban list | `0x119DD9` | 1 byte, `30` -> `18` | `BattleTower_UbersCheck`: the check is skipped for every room |
| Species Clause | `0x8B2E2` | 3 bytes | `CheckBTRule_PartySpeciesAreUnique` returns "fine" at once |
| Item Clause | `0x8B32A` | 3 bytes | `CheckBTRule_PartyItemsAreUnique` returns "fine" at once |
| Level limit | `0x118A28` | 3 bytes | the level picker no longer calls `BattleTower_LevelCheck` |
| Auto-level | `0x170263` | 6 bytes | `RunBattleTowerTrainer`: a call to an empty mobile stub becomes a call to the new routine |
| Auto-level | 1.0 `0x17367F` / 1.1 `0x17368C` (about 80 bytes) | new | free space at the end of bank `$5C`: sets the level and recalculates the stats of each party Pokemon |
| Surf and Fly | `0x51742`, `0x51762` | 1 byte each | TM/HM bits of Pikachu and Raichu in `BaseData` |
| New York moves | `0x3EAC0` | 5 bytes | `LoadEnemyMon.WildMoves`: the wild move fill goes through the new routine |
| New York moves | `0x3FE86` (about 320 bytes) | new | free space at the end of bank `$0F`: routine and table |
| New York moves | `0x17003` | 5 bytes | `HatchEggs`: the stat calculation goes through the new routine |
| New York moves | `0x174BA` (about 43 bytes) | new | free space at the end of bank 5: the hatch routine |
| Checksums | `0x14E`, `0x1FFDFE` to `0x1FFFFF` (about 14 bytes) | | header checksum and the Pokemon Stadium 2 checksums the build tool writes |

## Pokemon Center New York moves in this patch

All 118 species and move pairs of the egg distributions at the Pokemon Center New York (2001 to 2003), as listed by
Bulbapedia ("List of PCNY event Pokemon distributions in Generation II"). The real ones were level 5 eggs with one
special move added to the normal starting moves.

| Species | Special move(s) |
|---|---|
| Bulbasaur | AncientPower |
| Charmander | Crunch |
| Squirtle | Zap Cannon |
| Spearow | SonicBoom |
| Nidoran (female) | Lovely Kiss, Moonlight, Sweet Kiss |
| Nidoran (male) | Lovely Kiss, Morning Sun, Sweet Kiss |
| Zubat | Flail |
| Oddish | Leech Seed |
| Paras | Synthesis |
| Psyduck | Petal Dance, Tri Attack |
| Poliwag | Growth, Lovely Kiss, Sweet Kiss |
| Abra | Foresight |
| Machop | False Swipe, Thrash |
| Bellsprout | Lovely Kiss, Sweet Kiss |
| Tentacool | Confuse Ray |
| Geodude | Rapid Spin |
| Ponyta | Low Kick |
| Magnemite | Agility |
| Farfetch'd | Fury Cutter |
| Doduo | Low Kick |
| Seel | Flail |
| Onix | Sharpen |
| Drowzee | Amnesia |
| Krabby | Metal Claw |
| Voltorb | Agility |
| Exeggcute | Sweet Scent |
| Cubone | Fury Attack |
| Lickitung | DoubleSlap |
| Chansey | Sweet Scent |
| Tangela | Synthesis |
| Kangaskhan | Faint Attack |
| Horsea | Haze |
| Goldeen | Swords Dance |
| Staryu | Twister |
| Mr. Mime | Mind Reader |
| Scyther | SonicBoom |
| Pinsir | Rock Throw |
| Tauros | Quick Attack |
| Magikarp | Bubble, Reversal |
| Lapras | Bite, Future Sight |
| Eevee | Growth |
| Porygon | Barrier |
| Omanyte | Rock Throw |
| Kabuto | Rock Throw |
| Aerodactyl | Rock Throw |
| Snorlax | Lovely Kiss, Splash, Sweet Kiss |
| Dratini | Hydro Pump |
| Chikorita | Petal Dance |
| Cyndaquil | Double-Edge |
| Totodile | Submission |
| Sentret | Dizzy Punch |
| Hoothoot | Night Shade |
| Ledyba | Barrier |
| Spinarak | Growth |
| Chinchou | Light Screen |
| Pichu | Dizzy Punch, Petal Dance, Scary Face, Sing |
| Cleffa | Petal Dance, Scary Face, Swift |
| Igglybuff | Mimic, Petal Dance, Scary Face |
| Natu | Safeguard |
| Marill | Dizzy Punch, Hydro Pump, Scary Face |
| Sudowoodo | Substitute |
| Hoppip | Agility |
| Aipom | Mimic |
| Sunkern | Splash |
| Yanma | Steel Wing, Sweet Kiss |
| Wooper | Belly Drum, Scary Face |
| Murkrow | Beat Up |
| Misdreavus | Hypnosis |
| Wobbuffet | Mimic |
| Pineco | Substitute |
| Dunsparce | Fury Attack, Horn Drill |
| Snubbull | Lovely Kiss |
| Qwilfish | Double-Edge |
| Heracross | Seismic Toss |
| Sneasel | Moonlight |
| Teddiursa | Sweet Scent |
| Swinub | Whirlwind |
| Remoraid | Amnesia, Mist |
| Delibird | Pay Day, Spikes |
| Mantine | Gust |
| Skarmory | Fury Cutter |
| Phanpy | Absorb |
| Stantler | Safeguard |
| Tyrogue | Rage |
| Smoochum | Metronome, Petal Dance |
| Elekid | Dizzy Punch, Pursuit |
| Magby | Faint Attack |
| Miltank | Mega Kick |
| Larvitar | Rage |

When it is decided:

- **Wild and fixed encounters**: when the battle starts. With a 1 in 2 chance one of the species' special moves (picked
  at random when there are several) goes into the first empty move slot, or over the fourth move when all four are
  taken. Fixed encounters such as Snorlax, Lapras and Sudowoodo use the same routine, so saving in front of them and
  trying again is a fresh roll. A caught Pokemon keeps the moves it had in battle.
- **Eggs**: at the moment the egg hatches, with the same 1 in 2 chance. This is how the babies, the starters and Eevee
  can get theirs. It applies to every hatching egg of a listed species, including gift eggs.
- Nothing is decided when the save file is created. Trainers' Pokemon are never affected.

Moves introduced in Generation II stop a Pokemon from going through the Time Capsule, exactly as for the real event
Pokemon.

## Notes

- A legality checker will flag a Pikachu or Raichu that knows Fly, and most Pokemon carrying a New York move. Surf on
  Pikachu is accepted only where the checker allows Pokemon Stadium moves.
- Link trades with a clean game work; the clean game treats these Pokemon as ordinary data.
- Not checked: real cartridges, link cable play, Pokemon Stadium.
- The Battle Tower was never meant to see three of the same Pokemon or a level 5 Pokemon raised to level 100; a full
  seven-battle run with such a team has not been played yet.
