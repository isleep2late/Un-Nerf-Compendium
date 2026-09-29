# Emerald_UnNerf_Full as a BPS keyed to the No-Intro dump

`../Emerald_UnNerf_Full.xdelta` was built against the owner's `Emerald.gba` (SHA-1
`4c743011d7f9af0fbc1ef1de7bff157dde718f56`). It happens to decode against the No-Intro dump too, because
the two dumps differ only in bytes the delta never copies from. The BPS here is keyed to the No-Intro
dump outright, so a patcher can tell a wrong ROM apart by CRC32 before it writes anything.

| | name | size | SHA-1 | CRC32 |
|---|---|---|---|---|
| source | `Pokemon - Emerald Version (USA, Europe).gba` (No-Intro) | 16777216 | `f3ae088181bf583e55daf962a92bb46f4f1d07b7` | `1F1C08FB` |
| target | `Emerald_UNBANNED_NOCLAUSE_SOULDEW_ANYABILITY_DEOXYS.gba` | 16777216 | `3c3efe151cd2d7935505038704ec47ddfc54a231` | `54B752E3` |

The target is byte-for-byte the file the shipped xdelta produces (and the copy in the owner's finished-ROM
folder). The features it carries are listed in `../README.md`; this folder only changes the container.

Files:

* `Emerald_UnNerf_Full.bps` — BPS, no metadata block. Refuses any ROM whose CRC32 is not `1F1C08FB`.
* `Emerald_UnNerf_Full.nocomp.xdelta` — the same delta as VCDIFF **without** secondary compression
  (the shipped `.xdelta` uses DJW static Huffman, which many xdelta ports do not implement). Keyed to
  the same No-Intro source; xdelta only checks the Adler-32 of the output window, so it does not refuse a
  wrong source as reliably as BPS does.

Commands (flips = Floating IPS CLI, xdelta3 3.0.x):

```
flips --create --bps "Pokemon - Emerald Version (USA, Europe).gba" Emerald_UNBANNED_NOCLAUSE_SOULDEW_ANYABILITY_DEOXYS.gba Emerald_UnNerf_Full.bps
flips --apply Emerald_UnNerf_Full.bps "Pokemon - Emerald Version (USA, Europe).gba" out.gba      # sha1 3c3efe15...
xdelta3 -e -S none -s "Pokemon - Emerald Version (USA, Europe).gba" <target> Emerald_UnNerf_Full.nocomp.xdelta
xdelta3 -d -s "Pokemon - Emerald Version (USA, Europe).gba" Emerald_UnNerf_Full.nocomp.xdelta out.gba
```
