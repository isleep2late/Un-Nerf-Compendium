# Platinum_UnNerf_Full as a BPS keyed to the No-Intro dump

`../Platinum_UnNerf_Full.xdelta` was built against the owner's `Platinum.nds` (SHA-1
`c5072bf51a5ecc51ade99d3506c023f5bac3eeb3`). It also decodes against the pret `pokeplatinum_rev0.nds`
baseline, which is the No-Intro `Pokemon - Platinum Version (USA).nds` dump (rev 0). It does **not** decode
against rev 1 (`0862ec35b24de5c7e2dcb88c9eea0873110d755c`): xdelta reports a target window checksum
mismatch. So the delta is a rev-0 delta, and this BPS is keyed to the rev-0 No-Intro dump. No rev-1 BPS is
provided; nothing on hand decodes from rev 1.

| | name | size | SHA-1 | CRC32 |
|---|---|---|---|---|
| source | `Pokemon - Platinum Version (USA).nds` (No-Intro, rev 0) | 134217728 | `ce81046eda7d232513069519cb2085349896dec7` | `9253921D` |
| target | `Platinum_UNBANNED_NOCLAUSE_SOULDEW_GIRATINAO_SHAYMINSKY_ARCEUS_6MON_ABILITYLOCK.nds` | 134217728 | `15f1441de1a3bcffaf35a63e2f4e98a0a13b7416` | `C2BC6CA2` |

The target is byte-for-byte what the shipped xdelta produces from `Platinum.nds`, and the copy in the
owner's finished-ROM folder. Feature list: `../README_fullhackmons_allmodes.md`, `../README_6pokemon.md`,
`../README_arceus_formtype.md`.

Files:

* `Platinum_UnNerf_Full.bps` — BPS, no metadata block. Refuses any ROM whose CRC32 is not `9253921D`
  (rev 1's CRC32 is `69D628E8`, so it is refused).
* `Platinum_UnNerf_Full.nocomp.xdelta` — VCDIFF without secondary compression, same rev-0 source.

```
flips --create --bps "Pokemon - Platinum Version (USA).nds" <target> Platinum_UnNerf_Full.bps
flips --apply Platinum_UnNerf_Full.bps "Pokemon - Platinum Version (USA).nds" out.nds        # sha1 15f1441d...
xdelta3 -e -S none -s "Pokemon - Platinum Version (USA).nds" <target> Platinum_UnNerf_Full.nocomp.xdelta
xdelta3 -d -s "Pokemon - Platinum Version (USA).nds" Platinum_UnNerf_Full.nocomp.xdelta out.nds
```
