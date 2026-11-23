# Gen 1 - Pokemon Red / Blue: Pikachu Surf and Fly, the truck, New York moves
**UNTESTED BETA (built 2026-11-23; nobody has played a patched game yet).**

Status: **untested in play**. These patches were built from the pret disassembly (pret/pokered): the unmodified build
reproduces the clean dump byte for byte, the changes were made in the source, and the patch is the difference between
the two builds. What was checked: every patch re-applies to its stated input and gives the stated output byte for
byte; no existing code or data moved (new code sits in unused space at the end of a bank); the RAM and save layout are
identical to the clean game; each new routine (the New York move pick on wild, caught and hatched Pokemon, and in
Crystal the Battle Tower auto-level) was run in place in an emulator with test inputs and gave the expected
result. Everything else, including every map script and a full Battle Tower run, has not been run. Keep a backup of your save before you try them, and please report
what misbehaves.

- **Pikachu and Raichu can learn Surf and Fly** from HM03 and HM02 (two bits in each one's TM/HM compatibility data).
  In Red and Blue a surfing Pikachu uses the normal surfing sprite.
- **The truck**: after the S.S. Anne has left, the sailor in Vermilion City still says so but no longer pushes you
  back, so you can return to the dock. Surf to the walkway on the right, use STRENGTH from the party menu, then face
  the truck and press A: a **level 5 Mew** jumps out as a wild battle. If it faints or you run, it is there again
  the next time; once caught it is gone for good. Without STRENGTH the truck only shows a hint. The truck does not move.
- **Pokemon Center New York moves**: wild and gift Pokemon of the listed species have a 1 in 2 chance of knowing a
  move that was only ever given out at the Pokemon Center New York (table below).

## Files

| File | For | What |
|---|---|---|
| `Red_UnNerf.ips` | Red | everything above, IPS (694 bytes, 22 records) |
| `bps/Red_UnNerf.bps` | Red | the same change as a BPS (checks the input CRC32) |
| `Blue_UnNerf.ips` | Blue | everything above, IPS (694 bytes, 22 records) |
| `bps/Blue_UnNerf.bps` | Blue | the same change as a BPS (checks the input CRC32) |

## Input (exact), how to apply, output

| Game | Clean input (No-Intro) | Input SHA-1 | Input CRC32 | Output SHA-1 | Output CRC32 |
|---|---|---|---|---|---|
| Red | `Pokemon - Red Version (USA, Europe) (SGB Enhanced).gb` | `ea9bcae617fdf159b045185467ae58b2e4a48b9a` | `9f7fdd53` | `d96877896404cafc212b7721cfd4acf8221425b0` | `67ca9d63` |
| Blue | `Pokemon - Blue Version (USA, Europe) (SGB Enhanced).gb` | `d7037c83e1ae5b39bde3c30787637ba1d4c48ce2` | `d6da8a1a` | `dc1584082cae422bcda2e1b3be6a27f3b2533e2b` | `f9aff6fb` |

Check your dump's SHA-1 first; other regions and revisions are not supported. The IPS files do not check the input; the
BPS files refuse a wrong input. Apply with Floating IPS, Lunar IPS, Rom Patcher JS or any IPS/BPS tool. The result must
have the output SHA-1 above. The ROM size is unchanged; the header checksums are already correct in the output.

Saves are compatible both ways with the clean game: nothing in the save file or in RAM was added or moved. The truck Mew uses two event flags the clean game never reads.

## What each change does (ROM offsets, the same in Red and Blue)

| Feature | Offset | Size | Where (pokered) |
|---|---|---|---|
| Surf and Fly | `0x38698`, `0x386B4` | 1 byte each | TM/HM bits of Pikachu and Raichu in `BaseStats` |
| Sailor | `0x19804` | 2 bytes | `VermilionCityDefaultScript`: `jr nz, .ship_departed` becomes `ret nz` |
| Truck | `0x1DB4B` | 6 bytes | `VermilionDock_h`: the map now points at the new text table, script and object data |
| Truck | `0x1ECC2` (about 245 bytes) | new | free space at the end of bank 7: the map script wrapper, the truck text and its script, the dock's object data with two sign events on the truck at (20,0) and (21,0) |
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
