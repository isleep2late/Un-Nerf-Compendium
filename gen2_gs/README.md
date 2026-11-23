# Gen 2 - Pokemon Gold / Silver: Pikachu Surf and Fly, New York moves
**UNTESTED BETA (build 4, 2026-11-23; no person has played a patched game yet).**

Status: **not yet played by a person**. These patches were built from the pret disassembly (pret/pokegold): the unmodified build
reproduces the clean dump byte for byte, the changes were made in the source, and the patch is the difference between
the two builds. What was checked: every patch re-applies to its stated input and gives the stated output byte for
byte; no existing code or data moved (new code sits in unused space at the end of a bank); the RAM and save layout are
identical to the clean game. An automated tester then played Gold and Crystal 1.0 in an emulator by key presses (Silver and Crystal 1.1 were only booted): saves both ways, Surf and Fly, the New York moves on wild Pokemon and hatched eggs, and in Crystal Battle Tower runs with legendaries, levelled to the room and restored after a win, a loss and quitting. Not played: link play. Keep a backup of your save before you try them, and please report what misbehaves.

- **Pikachu and Raichu can learn Surf and Fly** from HM03 and HM02 (two bits in each one's TM/HM compatibility data).
  The clean game already shows the surfing Pikachu sprite when a Pikachu uses Surf.
- **Pokemon Center New York moves**: wild Pokemon and hatching eggs of the listed species have a 1 in 2 chance of
  knowing one of the moves that were only given out at the Pokemon Center New York (table below).

Gold and Silver have no Battle Tower, so there is no ban list to remove here.

## Files

| File | For | What |
|---|---|---|
| `Gold_UnNerf.ips` | Gold | everything above, IPS (458 bytes, 14 records) |
| `bps/Gold_UnNerf.bps` | Gold | the same change as a BPS (checks the input CRC32) |
| `Silver_UnNerf.ips` | Silver | everything above, IPS (459 bytes, 14 records) |
| `bps/Silver_UnNerf.bps` | Silver | the same change as a BPS (checks the input CRC32) |

## Input (exact), how to apply, output

| Game | Clean input (No-Intro) | Input SHA-1 | Input CRC32 | Output SHA-1 | Output CRC32 |
|---|---|---|---|---|---|
| Gold | `Pokemon - Gold Version (USA, Europe) (SGB Enhanced) (GB Compatible).gbc` | `d8b8a3600a465308c9953dfa04f0081c05bdcb94` | `6bde3c3e` | `6adc662f080968d65ff758cae22289a77b2439e3` | `8a79edb1` |
| Silver | `Pokemon - Silver Version (USA, Europe) (SGB Enhanced) (GB Compatible).gbc` | `49b163f7e57702bc939d642a18f591de55d92dae` | `8ad48636` | `405623d0fa3e61227d809e13e24210df99137075` | `e8281f4b` |

Check your dump's SHA-1 first; other regions and revisions are not supported. The IPS files do not check the input; the
BPS files refuse a wrong input. Apply with Floating IPS, Lunar IPS, Rom Patcher JS or any IPS/BPS tool. The result must
have the output SHA-1 above. The ROM size is unchanged; the header checksums are already correct in the output.

Saves are compatible both ways with the clean game: nothing in the save file or in RAM was added or moved. 

## What each change does (ROM offsets, the same in Gold and Silver)

| Feature | Offset | Size | Where (pokegold) |
|---|---|---|---|
| Surf and Fly | `0x51E29`, `0x51E49` | 1 byte each | TM/HM bits of Pikachu and Raichu in `BaseData` |
| New York moves | `0x3E93B` | 5 bytes | `LoadEnemyMon.WildMoves`: the wild move fill goes through the new routine |
| New York moves | `0x3FE9B` (about 320 bytes) | new | free space at the end of bank `$0F`: routine and table |
| New York moves | `0x173AB` | 5 bytes | `HatchEggs`: the stat calculation goes through the new routine |
| New York moves | `0x1783E` (about 43 bytes) | new | free space at the end of bank 5: the hatch routine |
| Checksums | `0x14E`, `0x1FFDFE` to `0x1FFFFF` (8 bytes) | | header checksum and the Pokemon Stadium 2 checksums the build tool writes |

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
