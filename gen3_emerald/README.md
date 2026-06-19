# Gen 3 — Pokémon Emerald: Hackmons / No-Ban-List + Any-Ability

Brings Gen-3 Emerald in line with the rest of the Compendium: removes every Battle
Frontier entry restriction, un-nerfs Soul Dew inside the Frontier, and — new for Gen 3 —
lets **any Pokémon have any ability**, set in PKHeX.

All three patches are **IPS**, made against a clean **Pokémon Emerald (USA/Europe)** ROM
(base used: md5 `7b058a7aea5bfbb352026727ebd87e17`). Apply with Lunar IPS, Floating IPS (flips),
or any IPS tool. Saves are compatible across all three (code-only changes).

## Patches (`patches/`)

| File | What it does |
|---|---|
| `1_Emerald_FrontierUnlock_SoulDew.ips` | Removes ALL Battle Frontier entry restrictions + un-nerfs Soul Dew |
| `2_Emerald_AnyAbility.ips` | Any-ability engine (use with the patched PKHeX) |
| `3_Emerald_Full_Hackmons_v3.ips` | Everything: patch 1 + patch 2 + the flash save-reliability fix. **This is the full build.** |

Patch 3 is self-contained; patches 1 and 2 are the individual components if you only want one.

## Battle Frontier restrictions removed (patch 1 & 3)

Gen 3 enforces frontier entry in two places — both are neutralized:
- **Banned-species list** (`gFrontierBannedSpecies` @ ROM `0x08611C9A`) → terminated (empty). Mew, Mewtwo, the legendary trio, Lati@s, etc. all allowed.
- **`AppendIfValid`** (the registration validator, the one that actually rejects your team) — three checks NOP'd: **level cap** (`bhi` @ `0x081A3F5E`), **species clause** (`bne` @ `0x081A3F82`), **item clause** (`bne` @ `0x081A3FA8`).
- Party-menu selection UI (`IsMonAllowedInBattleFrontier` @ `0x081B85AC` forced to allow; clause messages in `CheckBattleEntriesAndGetMessage` NOP'd @ `0x081B8724`/`0x081B873C`).
- **Soul Dew** in `CalculateBaseDamage`: the `!(gBattleTypeFlags & BATTLE_TYPE_FRONTIER)` gate is NOP'd (`bne` @ `0x080697A0` attacker, `0x080697D6` defender) so Latias/Latios keep the +50% Sp.Atk/Sp.Def boost inside the Frontier.

Works across all modes (Singles / Doubles / Multi / Link) and all facilities — these are the shared validators.

## Any ability (patch 2 & 3) — how it works

Gen 3 does **not** store "which ability" — it stores a single **ability-slot bit** (slot 0 / slot 1) and looks the ability up in the species table. There is no per-mon ability ID, which is why the community believed abilities were "locked."

This patch adds one: the per-Pokémon **override byte** lives in the unused PK3 **Sanity word, byte `0x1E`** (low byte; outside the checksum, so nothing has to be recomputed). `0` = normal slot ability; `2..77` = force that exact ability ID.

The engine reads it through small injected routines (free space @ `0x0837F260`) hooked into **every** site that establishes a battler's ability, plus the out-of-battle getter:
- `GetBoxMonData` `MON_DATA_ABILITY_NUM` @ `0x0806AA2A` (out-of-battle / summary)
- `GetAbilityBySpecies` @ `0x0806B694` (returns the override when ≥2)
- battle **intro** @ `0x0804C99A`
- battle **switch-in data update** @ `0x0803AD68`  ← the one that runs every send-in
- player battle-load @ `0x0806BC62`

### Setting an ability (PKHeX in this folder)

Use **`PKHeX/PKHeX.exe`** (patched build). Open a Gen-3 mon → the **Ability dropdown now lists all 78 abilities** for Gen 3. Pick one and save. (Picking one of the species' two normal slot abilities clears the override; picking anything else writes the override byte.) It also shows correctly on the hover tooltip and summary. Requires the free **.NET 10 Desktop Runtime** (same as official PKHeX): https://dotnet.microsoft.com/download/dotnet/10.0

Reserved: ability IDs 0 (None) and 1 (Stench) act as the slot markers and can't be force-assigned; everything else (2–77) works.

## PKHeX source (`PKHeX/src/`)
Modified files from PKHeX master (net10): `PK3.cs`, `G3PKM.cs`, `EditPK3.cs`, `PKMEditor.cs`. See `PKHeX_CHANGES.md` for the exact edits.
