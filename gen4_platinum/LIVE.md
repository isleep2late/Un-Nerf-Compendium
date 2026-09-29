# Platinum — nothing here is live-safe

Every Platinum artefact in this folder is a **rebuilt decomp ROM**: `Platinum_UnNerf_Full.xdelta`,
`bps/Platinum_UnNerf_Full.bps` and `bps/Platinum_UnNerf_Full.nocomp.xdelta` all produce the
`Platinum_UNBANNED_NOCLAUSE_SOULDEW_GIRATINAO_SHAYMINSKY_ARCEUS_6MON_ABILITYLOCK.nds` image, which is pret
`pokeplatinum` compiled with the three source patches (`platinum_fullhackmons_allmodes.patch`,
`platinum_6pokemon_singles.patch`, `platinum_arceus_formtype.patch`). The source patches resize runtime
arrays and add code, so the linker relaid the ARM9 binary and the overlays: functions, tables and data
moved. The delta is 540 KB of moves and rewrites across the image, not a set of in-place byte edits, even
though the output happens to be the same 128 MiB.

Writing that image over a running retail game's cartridge data would replace code and data under the
addresses the running game holds (return addresses, overlay tables, pointers into the FAT and NARCs). That
is unsafe; the game would crash or corrupt its save. These files are for a ROM file **before boot**, applied
to the dump named in `bps/SOURCES.md` (rev 0 only).

## What a live-safe Platinum patch would need

The behaviour changes themselves are mostly small logic edits (`Pokemon_IsOnBattleFrontierBanlist`
returning `FALSE`, the clause checks and the `MAX_TOTAL_LEVEL` rule in `BattleRegulation_ValidatePartySelection`,
the two `BATTLE_TYPE_FRONTIER` guards on the Soul Dew branches in `battle_lib.c`, the Arceus
`Battler_MonType` / `BoxPokemon_SetArceusForm` gates). To turn them into in-place records against the
**retail** ARM9 and overlays, the same way the Emerald IPS files were made, two things are needed:

1. the retail addresses of those functions, which the decomp gives symbolically (pret `pokeplatinum`
   builds byte-identical to retail rev 1, so its map is authoritative), and
2. the exact instruction bytes each edit becomes at those addresses, which means assembling the changed
   functions with the decomp's toolchain (`mwccarm` + NitroSDK via `get_metroskrew.sh`) and diffing
   against the clean build. That toolchain is not present here, and hand-assembling the edits without it
   would mean shipping bytes nobody has compiled or run.

The 1–6 party-size change is the exception: it widens `BattleTower` arrays, so it is not expressible as
same-size edits at all and would stay a rebuilt-ROM feature even with the toolchain. A future in-place set
would carry the ban list, clauses, level cap, Soul Dew and Arceus typing, and leave the flexible party
count to the whole-ROM build.
