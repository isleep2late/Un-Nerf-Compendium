# Black2 / White2 UnNerf_Pokestar as BPS keyed to the No-Intro dumps

The shipped xdeltas were built against the owner's `Black 2.nds` / `White 2.nds`
(SHA-1 `5da09a39424f1a76c52a3eebad9b5e8dcacb71ba` / `332c5758e8bef5be2c167e9fa69a14bbdbf3c435`). The
White 2 delta also decodes against the No-Intro dump; the Black 2 delta does not (xdelta checksum
mismatch), so a No-Intro Black 2 owner could not use the shipped file at all. Both BPS files here are keyed
to the No-Intro dumps. The targets are the trimmed ROMs `bw2_nobanlist.py` writes (the padding past the
end of the NDS image is dropped), so they are smaller than the 512 MiB source; flips warns about that when
creating the patch and it is expected.

| | name | size | SHA-1 | CRC32 |
|---|---|---|---|---|
| B2 source | `Pokemon - Black Version 2 (USA, Europe) (NDSi Enhanced).nds` (No-Intro) | 536870912 | `e51e6dfb8678a3d19dcd2a10691b96a569ca0abb` | `D4427FD1` |
| B2 target | `Black2_UNNERF_NOCLAUSE_ARCEUSTYPE_POKESTAR.nds` | 288045768 | `7cbb90519eadf4fda92b7c0951cb0389c7018295` | `C3448615` |
| W2 source | `Pokemon - White Version 2 (USA, Europe) (NDSi Enhanced).nds` (No-Intro) | 536870912 | `b5d7490be7b415b8f1e672a53e978a9cc667e56a` | `777EB04F` |
| W2 target | `White2_UNNERF_NOCLAUSE_ARCEUSTYPE_POKESTAR.nds` | 287706312 | `cb108cc01f6898ad02b1d18b2e9a6e26bb2b24f9` | `7E18E009` |

Both targets are byte-for-byte what the shipped xdeltas produce from the owner's dumps, and the copies in
the owner's finished-ROM folder. Feature list: `../POKESTAR.md`.

Files:

* `Black2_UnNerf_Pokestar.bps`, `White2_UnNerf_Pokestar.bps` — BPS, no metadata block; refuse a ROM whose
  CRC32 is not the source CRC32 above (the owner's own dumps are refused: `73EEC470` / `B72BBA42`).
* `*.nocomp.xdelta` — VCDIFF without secondary compression, same No-Intro sources.

```
flips --create --bps "<No-Intro .nds>" <target> <name>.bps
flips --apply <name>.bps "<No-Intro .nds>" out.nds
xdelta3 -e -S none -s "<No-Intro .nds>" <target> <name>.nocomp.xdelta
xdelta3 -d -s "<No-Intro .nds>" <name>.nocomp.xdelta out.nds
```
