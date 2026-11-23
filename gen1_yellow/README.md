# Gen 1 - Pokemon Yellow: Pikachu Surf and Fly, the starter evolves and still follows, the truck, New York moves
**UNTESTED BETA (build 4, 2026-11-23; no person has played a patched game yet).**

Status: **not yet played by a person**. These patches were built from the pret disassembly (pret/pokeyellow): the unmodified build
reproduces the clean dump byte for byte, the changes were made in the source, and the patch is the difference between
the two builds. What was checked: every patch re-applies to its stated input and gives the stated output byte for
byte; no existing code or data moved (new code sits in unused space at the end of a bank); the RAM and save layout are
identical to the clean game. An automated tester then played Red and Yellow in an emulator by key presses (Blue was only booted): Surf and Fly on Pikachu and Raichu, the starter Raichu and its follower, the sailor, the dock without the ship, the truck Mew (running away, fainting it and catching it), about 130 wild battles for the New York moves, and saves moved between the patched and the clean game, including a save made inside the dock. Not played: the Pikachu's Beach minigame, the gift Pokemon path of the New York moves, link play. Keep a backup of your save before you try them, and please report what misbehaves.

- **Pikachu and Raichu can learn Surf and Fly** from HM03 and HM02 (two bits in each one's TM/HM compatibility data).
  The clean game already shows the surfing Pikachu sprite for any Pikachu that knows Surf and already opens the
  Pikachu's Beach minigame for it; with HM03 this no longer needs a Pokemon Stadium Pikachu.
- **The starter Pikachu can evolve**: the Thunder Stone is no longer refused. A Raichu with your trainer ID and name
  counts as the starter: it keeps walking behind you (drawn as Pikachu, as the game has no other follower picture), keeps
  its friendship, mood, voice and face screens, walks up to Nurse Joy, and still cannot be released. The surfing sprite
  and the beach minigame stay Pikachu-only.
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
| `Yellow_UnNerf.ips` | Yellow | everything above, IPS (880 bytes, 30 records) |
| `bps/Yellow_UnNerf.bps` | Yellow | the same change as a BPS (checks the input CRC32) |

## Input (exact), how to apply, output

| Game | Clean input (No-Intro) | Input SHA-1 | Input CRC32 | Output SHA-1 | Output CRC32 |
|---|---|---|---|---|---|
| Yellow | `Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe) (CGB+SGB Enhanced).gb` | `cc7d03262ebfaf2f06772c1a480c7d9d5f4a38e1` | `7d527d62` | `e504570d117c492f95b57493856efc3bd219cf6b` | `624988ee` |

Check your dump's SHA-1 first; other regions and revisions are not supported. The IPS files do not check the input; the
BPS files refuse a wrong input. Apply with Floating IPS, Lunar IPS, Rom Patcher JS or any IPS/BPS tool. The result must
have the output SHA-1 above. The ROM size is unchanged; the header checksums are already correct in the output.

Saves are compatible both ways with the clean game: nothing in the save file or in RAM was added or moved. The truck Mew uses three event flags the clean game never reads. A save made inside the dock loads in the clean game too (which then shows the ship again). In the clean game a starter Raichu is an ordinary Raichu and does not follow.

## What each change does (ROM offsets)

| Feature | Offset | Size | Where (pokeyellow) |
|---|---|---|---|
| Surf and Fly | `0x38698`, `0x386B4` | 1 byte each | TM/HM bits of Pikachu and Raichu in `BaseStats` |
| Starter evolves | `0xD809` | 1 byte | `ItemUseEvoStone`: the refusal branch is never taken |
| Starter Raichu | `0xFCDC5`, `0xFCE27`, `0xFD0DF` | 16 bytes | `IsStarterPikachuAliveInOurParty`, `IsThisMonStarterPikachu`, `IsPlayerPikachuAsleepInParty`: the species test accepts Pikachu or Raichu |
| Starter Raichu | `0xFF26F` | 12 bytes, new | free space at the end of bank `$3F`: the shared species test |
| Sailor | `0x198BF` | 2 bytes | `VermilionCityDefaultScript`: `jr nz, .ship_departed` becomes `ret nz` |
| Sailor | `0x19976`, `0x199AA` | 1 and 16 bytes | `VermilionCitySailor1Text`: the "ship set sail" branch also lets you pass, like the ticket branch |
| Dock | `0x1D458` | 5 bytes | `VermilionDock_h` points at the new object data; the first instruction of `VermilionDock_Script` becomes a jump to the new script (the script and text addresses the game stores in the save stay the clean ones) |
| Dock | `0x1D5C7` | 2 bytes | the dock's one text entry (an unused text) now points at the truck text |
| Dock | `0x1E698` (about 346 bytes) | new | free space at the end of bank 7: the map script (removes the ship and its entrance once it has left, checks whether Mew was caught), the truck text and its script, the dock's object data with two sign events on the truck |
| New York moves | `0xF29D`, `0x3ED4B` | 5 bytes each | `_AddPartyMon` and `LoadEnemyMonData` go through the new routines |
| New York moves | `0xFAD8`, `0x3FBAB` (about 155 and 161 bytes) | new | free space at the end of banks 3 and `$0F` |
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
