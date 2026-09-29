# Black 2 / White 2

* `POKESTAR.md` — the full patch (ban list, clauses, Arceus typing, Pokéstar validator fix, prop back-sprite fix) and how `bw2_nobanlist.py` + `tools/` rebuild it from a clean ROM.
* `Black2_UnNerf_Pokestar.xdelta`, `White2_UnNerf_Pokestar.xdelta` — whole-ROM deltas keyed to the owner's dumps.
* `bps/` — the same targets as BPS keyed to the No-Intro dumps (`Pokemon - Black Version 2 ... .nds` CRC32 `D4427FD1`, `Pokemon - White Version 2 ... .nds` CRC32 `777EB04F`), plus xdeltas without secondary compression. Expected dump and target hashes: `bps/SOURCES.md`.
* `live/` — the ban-list + clause removal alone as **in-place, length-preserving** patches (IPS32 + BPS per game) that can be applied to a running game; the Arceus and Pokéstar parts stay in the whole-ROM files. `live/README.md` has the record spans, base and result hashes.
