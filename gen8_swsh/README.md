# Gen 8 — Pokémon Sword / Shield (Switch)

LayeredFS code mods for Sword/Shield, delivered as IPSwitch `.pchtxt` files (no patcher needed —
the emulator/CFW applies them at load). Built and **confirmed working on Eden v0.2.0, game v1.3.1**.

> You supply your own legally-dumped game. Nothing copyrighted is distributed — only tiny patch text.

Title IDs: **Sword `0100ABF008968000`**, **Shield `01008DB008C2C000`**.

## Available

### 1. Battle-only forme PERSISTENCE  (`FormePersist`)
Makes **Crowned Zacian**, **Crowned Zamazenta**, and **Eternamax Eternatus** (set via PKHeX)
**persist through save + reload, with no held item required**, and display correctly on the team menu.
The Gen-8 analogue of the Gen-6/7 Mega/Primal persistence patches.

**How it works:** the game's `IsBattleOnlyForme()` detector is forced to return 0, so none of the three
"reset battle forme to base" code paths ever fire on the stored forme.

### 2. Battle Tower clause removal  (`NoTowerClause`)  ✅ confirmed working
Removes the Battle Tower **Species Clause** (no two of the same Pokémon) **and Item Clause**
(no two of the same held item). SwSh's Tower has **no banned-species list** — these two duplicate
checks are the entirety of the restriction — so this lifts it completely. Every other legality check
(eggs, etc.) is left intact.

**How it works:** the per-frame team-legality routine builds a duplicate-species flag and a
duplicate-item flag for each party slot by looping `species_get`/`item_get` and comparing against the
rest of the team. The patch forces both duplicate flags to always store 0 (and short-circuits the
compare chains), so no slot is ever marked a duplicate.

## Install (Eden / Yuzu LayeredFS)

1. Open your load folder: `%APPDATA%\eden\load\` (Eden: right-click game → *Open Mod Data Location*).
2. Copy the title-id folder(s) here so you end up with, e.g. (Sword):
   - `...\eden\load\0100ABF008968000\FormePersist\exefs\formepersist.pchtxt`
   - `...\eden\load\0100ABF008968000\NoTowerClause\exefs\noclause.pchtxt`
   - (and the `01008DB008C2C000` set for Shield)
3. Right-click the game → *Properties* → *Add-Ons* → tick the mod(s) → OK.
4. Make sure **Enable GDB Stub is OFF** (Configure → Debug) or the game hangs on "Launching".
5. Boot.

## Patch contents (v1.3.1)

### FormePersist — Sword (`@nsobid-4628A512…`) / Shield (`@nsobid-DBDDD138…`)

| offset (Sword / Shield) | bytes | meaning |
|---|---|---|
| `013AE910` / `013AE940` | `00008052` | `IsBattleOnlyForme`: `mov w0,#0` |
| `013AE914` / `013AE944` | `C0035FD6` | `ret` |
| `013AEC68` / `013AEC98` | `1F2003D5` | reset-call NOP (redundant safety) |

### NoTowerClause — Sword (`@nsobid-4628A512…`) / Shield (`@nsobid-DBDDD138…`)

| offset (Sword) | offset (Shield) | bytes | meaning |
|---|---|---|---|
| `014F9654` | `014F96C4` | `08008052` | species-dup `cset w8,eq` → `mov w8,#0` |
| `014F965C` | `014F96CC` | `19000014` | species-dup `b.eq` → unconditional `b` (skip remaining species compares) |
| `014F96DC` | `014F974C` | `09008052` | item-dup `cset w9,eq` → `mov w9,#0` |
| `014F96E4` | `014F9754` | `1B000014` | item-dup `b.eq` → unconditional `b` (skip remaining item compares) |

Both clause functions are the same code; Shield's copy is shifted +0x70 in that region. The two
short branch targets differ between versions and are baked into the `19000014` / `1B000014` words
above (each is a `b` to that game's item-block-start / loop-tail respectively).

## Format notes (the two things that make Eden `.pchtxt` mods silently do nothing)

- **`@enabled` is mandatory.** Without it Eden logs "Applying IPSwitch patch" but writes **zero bytes**.
- **`@flag offset_shift 0x100`** is the correct convention for Eden (module offsets from a text-base-0
  dump need +0x100). With `0x0` the patch lands 256 bytes early.

Offsets are version-specific (v1.3.1 build ids above). A different update needs re-derived offsets.

## `swsh_dynamax_unlock.py` — let Zacian / Zamazenta / Eternatus Dynamax

The "this species can't Dynamax" restriction in Sword/Shield is **not code** — it's a
per-species flag in the personal table (`CanNotDynamax`, byte `0x5A` bit 2 of each `0xB0`
entry). So it's fixed as **RomFS data**, the same edit pkNX makes, and shipped as a LayeredFS
RomFS mod rather than a `.pchtxt`.

This script does it from the command line (no pkNX needed) and, unlike a naive edit, also
clears the flag on the **alternate-form** entries (Crowned Zacian/Zamazenta are separate
appended entries, reached via `FormStatsIndex`/`FormCount`).

### Use

1. Dump your RomFS (Eden: right-click game → **Dump RomFS**) and grab
   `bin/pml/personal/personal_data_total.perbin`. Use your own **v1.3.1** dump so DLC
   entries aren't disturbed.
2. Run:
   ```
   python swsh_dynamax_unlock.py personal_data_total.perbin              # Zacian + Zamazenta
   python swsh_dynamax_unlock.py personal_data_total.perbin --eternatus  # + Eternatus
   python swsh_dynamax_unlock.py personal_data_total.perbin --all        # everything
   ```
3. Put the output at
   `load/<TitleID>/DynamaxUnlock/romfs/bin/pml/personal/personal_data_total.perbin`
   (Sword `0100ABF008968000`, Shield `01008DB008C2C000`), and enable the **DynamaxUnlock**
   add-on in Eden.

`--verify` reports what it would change without writing. Field offsets/flag verified against
pkNX `PersonalInfo8SWSH` and PKHeX.

> Why RomFS and not a code patch: the in-battle Dynamax check reads this personal flag; there's
> no single species-special-case in `main` to NOP (the only dog-range special-case in code is the
> Dynamax-Candy *item* gate, handled separately by `DynamaxCandyAll`).

