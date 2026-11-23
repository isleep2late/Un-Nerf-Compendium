# Gen 1 - Pokemon Red / Blue: Pikachu Surf and Fly, the truck, New York moves
**UNTESTED BETA (build 4, 2026-11-23; no person has played a patched game yet).**

Status: **not yet played by a person**. These patches were built from the pret disassembly (pret/pokered): the unmodified build
reproduces the clean dump byte for byte, the changes were made in the source, and the patch is the difference between
the two builds. What was checked: every patch re-applies to its stated input and gives the stated output byte for
byte; no existing code or data moved (new code sits in unused space at the end of a bank); the RAM and save layout are
identical to the clean game. An automated tester then played Red, Blue and Yellow in an emulator by key presses: Surf and Fly on Pikachu and Raichu, the starter Raichu and its follower, the sailor, the dock without the ship, the truck Mew (running away, fainting it and catching it), the Pikachu's Beach minigame, the New York moves in wild battles and on gift Pokemon, and saves moved between the patched and the clean game, including a save made inside the dock (a save made there after the ship left shows the ship again on the clean game, but it cannot be boarded). Not played: link play and trades. Keep a backup of your save before you try them, and please report what misbehaves.

- **Pikachu and Raichu can learn Surf and Fly** from HM03 and HM02 (two bits in each one's TM/HM compatibility data).
  In Red and Blue a surfing Pikachu uses the normal surfing sprite.
- **The truck**: after the S.S. Anne has left, the sailor in Vermilion City still says so but lets you through, so
  you can return to the dock. The ship is gone: there is open water where it was, and the way onto it is closed.
  Surf to the walkway on the right, use STRENGTH from the party menu, then face the truck and press A: a
  **level 5 Mew** jumps out as a wild battle. If it faints or you run, it is there again the next time; once caught it
  is gone for good. Without STRENGTH the truck only shows a hint. The truck does not move.
- **Pokemon Center New York moves**: wild and gift Pokemon of the listed species have a 1 in 2 chance of knowing a
  move that was only ever given out at the Pokemon Center New York (table below).

## Files

| File | For | What |
|---|---|---|
| `Red_UnNerf.ips` | Red | everything above, IPS (826 bytes, 25 records) |
| `bps/Red_UnNerf.bps` | Red | the same change as a BPS (checks the input CRC32) |
| `Blue_UnNerf.ips` | Blue | everything above, IPS (826 bytes, 25 records) |
| `bps/Blue_UnNerf.bps` | Blue | the same change as a BPS (checks the input CRC32) |

## Input (exact), how to apply, output

| Game | Clean input (No-Intro) | Input SHA-1 | Input CRC32 | Output SHA-1 | Output CRC32 |
|---|---|---|---|---|---|
| Red | `Pokemon - Red Version (USA, Europe) (SGB Enhanced).gb` | `ea9bcae617fdf159b045185467ae58b2e4a48b9a` | `9f7fdd53` | `4719f60ec6fe1f0785dce0477d378ac14a41a182` | `052cb98d` |
| Blue | `Pokemon - Blue Version (USA, Europe) (SGB Enhanced).gb` | `d7037c83e1ae5b39bde3c30787637ba1d4c48ce2` | `d6da8a1a` | `3b1f69cc4ecf8452d03a11acec69620c5e39119f` | `28510095` |

Check your dump's SHA-1 first; other regions and revisions are not supported. The IPS files do not check the input; the
BPS files refuse a wrong input. Apply with Floating IPS, Lunar IPS, Rom Patcher JS or any IPS/BPS tool. The result must
have the output SHA-1 above. The ROM size is unchanged; the header checksums are already correct in the output.

Saves are compatible both ways with the clean game: nothing in the save file or in RAM was added or moved. The truck Mew uses three event flags the clean game never reads. A save made inside the dock loads in the clean game too (which then shows the ship again).

## What each change does (ROM offsets, the same in Red and Blue)

| Feature | Offset | Size | Where (pokered) |
|---|---|---|---|
| Surf and Fly | `0x38698`, `0x386B4` | 1 byte each | TM/HM bits of Pikachu and Raichu in `BaseStats` |
| Sailor | `0x19804` | 2 bytes | `VermilionCityDefaultScript`: `jr nz, .ship_departed` becomes `ret nz` |
| Sailor | `0x198B8`, `0x198EC` | 1 and 16 bytes | `VermilionCitySailor1Text`: the "ship set sail" branch also lets you pass, like the ticket branch |
| Dock | `0x1DB50` | 5 bytes | `VermilionDock_h` points at the new object data; the first instruction of `VermilionDock_Script` becomes a jump to the new script (the script and text addresses the game stores in the save stay the clean ones) |
| Dock | `0x1DCBF` | 2 bytes | the dock's one text entry (an unused text) now points at the truck text |
| Dock | `0x1ECC2` (about 346 bytes) | new | free space at the end of bank 7: the map script (removes the ship and its entrance once it has left, checks whether Mew was caught), the truck text and its script, the dock's object data with two sign events on the truck at (20,0) and (21,0) |
| New York moves | `0xF417` | 5 bytes | `_AddPartyMon`: the move fill goes through the new routine |
| New York moves | `0xFC4A` (about 155 bytes) | new | free space at the end of bank 3: routine and table |
| New York moves | `0x3EBC5` | 5 bytes | `LoadEnemyMonData`: the wild move fill goes through the new routine |
| New York moves | `0x3FBC8` (about 161 bytes) | new | free space at the end of bank `$0F`: routine and table |
| Header | `0x14E` | 2 bytes | global checksum |

## Pokemon Center New York moves in this patch

The New York machines (2001 to 2003) only served Gold, Silver and Crystal. These are the 33 species and move pairs from
that list where both the species and the move exist in Generation I, so the real ones could be traded back through the
Time Capsule. None of them can be obtained in the clean games.

| Species | Special move |
|---|---|
| Spearow | SonicBoom |
| Nidoran (female) | Lovely Kiss |
| Nidoran (male) | Lovely Kiss |
| Oddish | Leech Seed |
| Psyduck | Petal Dance, Tri Attack |
| Poliwag | Growth, Lovely Kiss |
| Machop | Thrash |
| Bellsprout | Lovely Kiss |
| Tentacool | Confuse Ray |
| Ponyta | Low Kick |
| Doduo | Low Kick |
| Magnemite | Agility |
| Voltorb | Agility |
| Onix | Sharpen |
| Drowzee | Amnesia |
| Cubone | Fury Attack |
| Lickitung | DoubleSlap |
| Horsea | Haze |
| Goldeen | Swords Dance |
| Scyther | SonicBoom |
| Pinsir | Rock Throw |
| Tauros | Quick Attack |
| Magikarp | Bubble |
| Lapras | Bite |
| Eevee | Growth |
| Omanyte | Rock Throw |
| Kabuto | Rock Throw |
| Aerodactyl | Rock Throw |
| Snorlax | Lovely Kiss, Splash |
| Dratini | Hydro Pump |

When it is decided: when the Pokemon itself is created. A wild Pokemon gets its hidden stats (DVs) at the moment the
battle starts, and the special move follows from them: an odd Special DV means it has the move (1 in 2), and for
Psyduck, Poliwag and Snorlax the Speed DV picks which of the two. The move goes into the first empty move slot, or over
the fourth move when all four are taken. A caught Pokemon keeps the same DVs, so it has the same move the wild one
showed in battle. Gift Pokemon (Eevee, Lapras, the fossils, the Magikarp) are decided when they are handed over. Nothing
is decided when the save file is created: save in front of Snorlax and each attempt is a fresh roll. Trainers'
Pokemon are never affected.

Source: Bulbapedia, "List of PCNY event Pokemon distributions in Generation II".

## Notes

- A legality checker will flag a Pikachu or Raichu that knows Fly, a truck Mew with your own trainer name, and most Pokemon carrying a New York move. Surf on
  Pikachu is accepted only where the checker allows Pokemon Stadium moves.
- Link trades with a clean game work; the clean game treats these Pokemon as ordinary data.
- Not checked: real cartridges, link cable play, Pokemon Stadium.
