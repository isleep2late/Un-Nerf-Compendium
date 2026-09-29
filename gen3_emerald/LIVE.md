# Emerald — what can be applied to a running game

"Live-safe" here means: the patch only overwrites bytes inside the 16 MiB cartridge image, never moves
anything, never changes the image's length, and so can be written into the ROM copy of a game that is
already running (mGBA's `GBAPatch8` path copies the ROM on first write) without the running code losing its
footing. The rule is decided by inspecting each file, not by the name it carries.

## The three IPS files: all live-safe

Every record was dumped with a plain IPS parser (`PATCH`, u24 offset, u16 size, `EOF`); none of the three
has a truncation trailer after `EOF`, none uses RLE, every record ends below 16 MiB, and the largest single
record is 30 bytes. The code cave at `0x37F260`–`0x37F314` (0xB4 bytes of zero space in the retail image) is
written as seventeen short records, so no record is larger than 0xB4 bytes either.

| file | size | records | bytes written | largest record | highest byte | trailer |
|---|---|---|---|---|---|---|
| `1_Emerald_FrontierUnlock_SoulDew.ips` | 71 | 9 | 18 | 2 | `0x611C9C` | none |
| `2_Emerald_AnyAbility.ips` | 297 | 23 | 174 | 30 | `0x37F314` | none |
| `3_Emerald_Full_Hackmons_v3.ips` | 375 | 34 | 197 | 30 | `0x611C9C` | none |

Patch 3 is the union of 1 and 2 plus the two flash save-reliability records at `0x2E1D95` (3 bytes) and
`0x2E1D9A` (2 bytes). Patch 1 and patch 2 write disjoint offsets, and patch 3 repeats their bytes exactly.

### What each one changes at runtime (from `README.md`)

* **Patch 1 — Frontier unlock + Soul Dew.** Two-byte Thumb edits only: the `gFrontierBannedSpecies` list at
  `0x611C9A` is terminated at its first entry (empty ban list); in `AppendIfValid` the level-cap `bhi` at
  `0x1A3F5E`, the species-clause `bne` at `0x1A3F82` and the item-clause `bne` at `0x1A3FA8` are NOPed; the
  party-menu `IsMonAllowedInBattleFrontier` is forced to allow at `0x1B85B0` and the two clause messages in
  `CheckBattleEntriesAndGetMessage` are NOPed at `0x1B8724` / `0x1B873C`; in `CalculateBaseDamage` the
  `BATTLE_TYPE_FRONTIER` gates on the Soul Dew boost are NOPed at `0x0697A0` (attacker) and `0x0697D6`
  (defender). Applied mid-game these take effect the next time the code runs: the next Frontier
  registration screen, the next damage calculation. Nothing already on the stack is affected.
* **Patch 2 — any ability.** Four-byte hooks at `0x03AD68` (switch-in), `0x04C99A` (battle intro),
  `0x06AA2A` (`GetBoxMonData` ability slot), `0x06B696` (`GetAbilityBySpecies`), `0x06BA1E`, `0x06BC62`
  (player battle load), and the routines they branch to in the `0x37F260` cave. The override byte lives in
  each Pokémon's PK3 sanity word, so a save made before the patch is unaffected until PKHeX writes an
  override. Applied mid-battle the new ability is read at the next switch-in or intro, not retroactively.
* **Patch 3 — everything.** Patch 1 + patch 2 + the flash save fix at `0x2E1D95` / `0x2E1D9A` (five bytes of
  save-code timing). The save fix matters at the next save, which is the only time that code runs.

Order of a live write does not matter for 1 + 2 (disjoint offsets); writing 3 alone is the same result.
Reversing a live patch means writing the original bytes back (the same offsets, the retail values), which
the app can take from its copy of the untouched image; there is no truncation to undo.

## The whole-ROM BPS / xdelta: NOT live-safe

`bps/Emerald_UnNerf_Full.bps`, `bps/Emerald_UnNerf_Full.nocomp.xdelta` and `Emerald_UnNerf_Full.xdelta`
produce the Deoxys-forms build, which is a **rebuilt decomp image** (pret `pokeemerald` compiled with the
changes). The linker relaid the whole ROM: functions, tables and strings sit at different addresses from
retail, so the delta is 650 KB of moves and rewrites, not a handful of in-place edits. Writing that over a
running game's cartridge image would pull the code out from under every return address and pointer the
game is holding. Apply it only to a file before boot, and only to the dump named in `bps/SOURCES.md`.
