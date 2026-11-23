# Gen 1 - Pokemon Yellow: Pikachu Surf and Fly, the starter evolves and still follows, the truck, New York moves
**UNTESTED BETA (built 2026-11-23; nobody has played a patched game yet).**

Status: **untested in play**. These patches were built from the pret disassembly (pret/pokeyellow): the unmodified build
reproduces the clean dump byte for byte, the changes were made in the source, and the patch is the difference between
the two builds. What was checked: every patch re-applies to its stated input and gives the stated output byte for
byte; no existing code or data moved (new code sits in unused space at the end of a bank); the RAM and save layout are
identical to the clean game; each new routine (the New York move pick on wild, caught and hatched Pokemon, and in
Crystal the Battle Tower auto-level) was run in place in an emulator with test inputs and gave the expected
result. Everything else, including every map script and a full Battle Tower run, has not been run. Keep a backup of your save before you try them, and please report
what misbehaves.

- **Pikachu and Raichu can learn Surf and Fly** from HM03 and HM02 (two bits in each one's TM/HM compatibility data).
  The clean game already shows the surfing Pikachu sprite for any Pikachu that knows Surf and already opens the
  Pikachu's Beach minigame for it; with HM03 this no longer needs a Pokemon Stadium Pikachu.
- **The starter Pikachu can evolve**: the Thunder Stone is no longer refused. A Raichu with your trainer ID and name
  counts as the starter: it keeps walking behind you (drawn as Pikachu, as the game has no other follower picture), keeps
  its friendship, mood, voice and face screens, walks up to Nurse Joy, and still cannot be released. The surfing sprite
  and the beach minigame stay Pikachu-only.
- **The truck**: after the S.S. Anne has left, the sailor in Vermilion City still says so but no longer pushes you
  back, so you can return to the dock. Surf to the walkway on the right, use STRENGTH from the party menu, then face
  the truck and press A: a **level 5 Mew** jumps out as a wild battle. If it faints or you run, it is there again
  the next time; once caught it is gone for good. Without STRENGTH the truck only shows a hint. The truck does not move.
- **Pokemon Center New York moves**: wild and gift Pokemon of the listed species have a 1 in 2 chance of knowing a
  move that was only ever given out at the Pokemon Center New York (table below).

## Files

| File | For | What |
|---|---|---|
| `Yellow_UnNerf.ips` | Yellow | everything above, IPS (748 bytes, 27 records) |
| `bps/Yellow_UnNerf.bps` | Yellow | the same change as a BPS (checks the input CRC32) |

## Input (exact), how to apply, output

| Game | Clean input (No-Intro) | Input SHA-1 | Input CRC32 | Output SHA-1 | Output CRC32 |
|---|---|---|---|---|---|
| Yellow | `Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe) (CGB+SGB Enhanced).gb` | `cc7d03262ebfaf2f06772c1a480c7d9d5f4a38e1` | `7d527d62` | `168bd6d1977de94bedae128132d8a33104e49ef7` | `462a3252` |

Check your dump's SHA-1 first; other regions and revisions are not supported. The IPS files do not check the input; the
BPS files refuse a wrong input. Apply with Floating IPS, Lunar IPS, Rom Patcher JS or any IPS/BPS tool. The result must
have the output SHA-1 above. The ROM size is unchanged; the header checksums are already correct in the output.

Saves are compatible both ways with the clean game: nothing in the save file or in RAM was added or moved. The truck Mew uses two event flags the clean game never reads. In the clean game a starter Raichu is an ordinary Raichu and does not follow.

## What each change does (ROM offsets)

| Feature | Offset | Size | Where (pokeyellow) |
|---|---|---|---|
| Surf and Fly | `0x38698`, `0x386B4` | 1 byte each | TM/HM bits of Pikachu and Raichu in `BaseStats` |
| Starter evolves | `0xD809` | 1 byte | `ItemUseEvoStone`: the refusal branch is never taken |
| Starter Raichu | `0xFCDC5`, `0xFCE27`, `0xFD0DF` | 16 bytes | `IsStarterPikachuAliveInOurParty`, `IsThisMonStarterPikachu`, `IsPlayerPikachuAsleepInParty`: the species test accepts Pikachu or Raichu |
| Starter Raichu | `0xFF26F` | 12 bytes, new | free space at the end of bank `$3F`: the shared species test |
| Sailor | `0x198BF` | 2 bytes | `VermilionCityDefaultScript`: `jr nz, .ship_departed` becomes `ret nz` |
| Truck | `0x1D453` | 6 bytes | `VermilionDock_h` points at the new text table, script and object data |
| Truck | `0x1E698` (about 245 bytes) | new | free space at the end of bank 7 |
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
