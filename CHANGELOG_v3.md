# Un-Nerf Compendium — v3 changelog (2026-06)

This release corrects one wrong claim, adds Arceus form-typing as a clean code patch across four
games, and documents what is and isn't solved. Everything below was reverse-engineered from
legally-dumped personal copies; nothing copyrighted is distributed.

## 1. Hoopa-Unbound persistence — corrected (this is the important one)

**Earlier versions and write-ups said Hoopa-Unbound persisted through save+reload. They were wrong.**

- Megas, Primals, Shaymin-Sky, Furfrou, Necrozma-Ultra and Zygarde-Complete revert via a *single*
  `CoreParam::ChangeFormNo(base)` call. NOP that call and they persist — those were always correct.
- **Hoopa-Unbound is different.** Its revert is a **destructive multi-call block**:
  `ChangeFormNo(base)` **plus** the adjacent recurring `CoreParam` extra-setters (`0x3B2CA8`,
  `0x3B2E24`), and that block appears in **multiple contexts** — party-load, PC/box via
  `UpdateFormForBox` (ORAS `0x46E82C`), and the field clock. ORAS has **four** such blocks; none
  were NOP'd by the old patch, so Hoopa reverted anyway (most visibly on Alpha Sapphire).

**Fix:** `gen67_formepersist/formepersist.py` v3 adds `find_forme_destructive_resets()`, which
auto-locates every block by signature and NOPs all of its calls. Verified: **AS/OR +11 NOPs (4
blocks), US/UM +6** (one block was already covered). Apply with `--full`.

If you published the older "Hoopa persists" claim anywhere, it should be corrected to: *the original
patch did not make Hoopa-Unbound persist; the destructive-reset-block fix (v3) does.*

## 2. Arceus form-driven typing — now a code patch (supersedes personal-rebuild)

Goal: a PKHeX'd Arceus keeps its form's type. **Hold the matching Plate → Multitype fires (Plate
type), exactly like vanilla. No Plate → the form's type (Ghost form = Ghost). Form persists.**
(Gen-4/6/7 Arceus form id == type id, so "the form's type" is literally the form number.)

| Game | How | Status |
|---|---|---|
| **ORAS** (AS/OR) | code.bin getter cave reusing `ItemToForm`; gate-removal so any-ability Arceus is handled; `ItemToForm` persist NOP | built + Unicorn-verified |
| **USUM** (US/UM) | code.bin getter cave (plate-or-form table) for **Arceus and Silvally**; persistence; + Battle.cro Protean species-list cleared | built + Unicorn-verified (UM in-game confirmed) |
| **Platinum** | 3-edit source patch vs pret `pokeplatinum` (`Battler_MonType` default→formNum; two plate-gated persistence guards) — see `gen4_platinum/platinum_arceus_formtype.patch` | source patch, builds to a verified ROM |
| **BW2** (B2/W2) | per-form personal entries (gen-5 has `FormStatsIndex`) | built |

Castform/Kecleon Protean in **ORAS** needs no patch — there is no species blocklist in the gen-6
battle module (unlike USUM's `Battle.cro` list, which the Arceus/Silvally fix clears).

## 3. Known-not-done (documented honestly)

- **Giratina-Origin without the Griseous Orb (Gen 6/7):** Origin is *item-derived*, not reset by a
  `ChangeFormNo` normalizer, so `formepersist` can't cover it. Needs a separate orb-gate patch
  (same shape as the Arceus Plate gate). In **Platinum**, the facility forme-revert *is* handled
  (SmolJoltik's command-798 fix + the battle-engine flip in the `gen4_platinum` xdelta).
- **6-Pokémon facility teams:** raising the party-size field past the legal limit crashes on
  team-confirm in the games tested (the team buffer is fixed-size); this is the same `>3` crash the
  Gen-5 write-up warns about. Not shipped. The ORAS "6 in Battle Maison" claim from the forum post
  is unverified and should be re-checked in-game.

## 4. Build provenance

Patches located via a save-state `|static|` symbol extractor, capstone ARM/Thumb disassembly, and
Unicorn emulation of each patched routine (end-to-end type/persistence checks). 3DS builds re-fix
the ExeFS `.code` hash, the NCCH ExeFS/RomFS superblock hashes, and (for `.cia`) the TMD content
hash; every build was re-read and hash-validated. NDS arm9 is uncompressed and round-trips
byte-exact via ndspy.
