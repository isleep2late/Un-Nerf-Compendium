# Gen 6/7 Forme Persistence

Makes battle-only / restricted **formes persist through a save + reload** in Pokémon
**Omega Ruby/Alpha Sapphire and Ultra Sun/Ultra Moon (NOT tested on X/Y/S/M**. Set a forme with
PKHeX and it no longer snaps back to base when you boot the game — on your party, and (with
`--full`) in the PC and other contexts.

## What reverts, and why this fixes it

In gen 6/7, the game runs "normalize formes" routines outside of battle that call
`CoreParam::ChangeFormNo(baseForm)` on your Pokémon. There are several of them:

| context | what it reset |
|---|---|
| **GameData load** (`ResetPartyBattleForms`) | **Mega** Evolutions, **Primal** Kyogre/Groudon |
| field real-time clock | **Furfrou** trims (5 days), **Hoopa** Unbound (3 days) |
| field clock / GTS | **Shaymin**-Sky (reverts at night) |
| GameData load | **Necrozma** Ultra-Burst, **Zygarde**-Complete *(gen 7 only)* |
| PC deposit, Pokémon Refresh, Day Care | Shaymin / Furfrou / Hoopa |

This patch **NOP**s those `ChangeFormNo` revert calls. It deliberately leaves the forme
**setters** alone — Mega Evolve in battle, the Prison Bottle (`SetHuupaForm`), terrain
forme changes (Burmy/Wormadam), Mystery Gift, Xerneas's in-battle activation — so you can
still change formes normally, and in-battle stance logic (Aegislash, Wishiwashi, Darmanitan-Zen,
etc., which lives in `Battle.cro`) is untouched. Fusions (Kyurem-Black/White, Necrozma
Dusk-Mane/Dawn-Wings) already persist and need nothing.

## Use

```
python formepersist.py  YourGame.cia            # Mega/Primal + auto-detected reverts (safe, universal)
python formepersist.py  YourGame.cia --full     # + the full verified forme table (US/UM, OR/AS)
python formepersist.py  YourGame.cia --verify
```

- Works on a **decrypted** `.cia` or `.3ds` dump (Citra/Azahar/Lime3DS don't check signatures).
- The **Mega/Primal** fix is fully **auto-located** by instruction signature, so in theory it should work on
  X/Y, OR/AS, S/M, US/UM, any region/version.
- `--full` adds the complete forme set; verified address tables ship for **US/UM** and **OR/AS**.
  X/Y and S/M should get the auto-located subset (Mega/Primal + the clean condition-reverts); their
  full tables can be generated the same way (find `ChangeFormNo`, NOP its out-of-battle
  revert callers).
- The script re-fixes the ExeFS `.code` hash, ExeFS superblock hash, and (for `.cia`) the TMD
  content hash, so the build still installs and boots.

## How the addresses were found (for porting to other versions)

1. Find `ChangeFormNo` by the `ResetPartyBattleForms` loop signature: a per-party-member loop
   whose tail is `bl X ; add rN,rN,#1 ; cmp rN,rM ; blt loop`, where `X` is called 3× in the
   body with two `cmp r0,#1` (Primal Groudon/Kyogre checks). `X` is `ChangeFormNo`; the loop is
   the Mega/Primal reset.
2. Enumerate all callers of `ChangeFormNo`. The **reverts** write the base forme (`mov r1,#0`
   or a computed base) from an out-of-battle normalizer; the **setters** write a special forme.
   NOP the reverts.
3. Re-hash (ExeFS `.code` + superblock, plus TMD for `.cia`).

This same approach produced the verified US/UM (19 sites) and OR/AS (16 sites) tables.
