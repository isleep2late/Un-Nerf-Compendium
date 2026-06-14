# Arceus / Silvally form-driven typing — code patch (ORAS + USUM)

This is the **confirmed-working** approach (supersedes the personal-rebuild in the parent folder).
A PKHeX'd Arceus (or Silvally) plays as its **form's type**:

- **Holds the matching Plate / Memory →** Multitype / RKS fires (Plate/Memory type) — vanilla behaviour.
- **No Plate / Memory →** the **form's type** (Ghost form = Ghost), not Normal.
- The form **persists** through save + reload.

## Why a code patch (not personal data)
The gen-6/7 in-battle type accessor *forces* Normal on a no-Plate Multitype Arceus (a `default`
case), so editing personal data alone doesn't help in battle. The fix routes both type-getters
through a small **code cave** in `.code` that returns the Plate/Memory type if one is held, else the
form's type (`GetFormNo`), and NOPs the persistence reset. Length-neutral; every site is validated
before writing and the build is re-hashed (ExeFS `.code` + superblock, TMD for `.cia`).

## Files
- `s2_lib.py` — `.code` extraction helpers.
- `s2_arceus_port.py` / `s2_arceus_final.py` — USUM Arceus+Silvally edits (config-driven; US & UM).
- `s2_oras_build.py` — ORAS (AS/OR) Arceus edits (reuses the game's `ItemToForm`).
- `s2_build_port.py` / `s2_build_final.py` — build a ROM from a NO-NERF base, with inline Unicorn
  verification, then `s2_cia_to_3ds.py` wraps the `.cia` NCCH into a bootable `.3ds`.

## Use
Point the build script's source at your **decrypted** dump (or your already-un-nerfed / Hoopa-fixed
build — the Arceus edits are in disjoint regions, so they stack cleanly on top). Example:
```
python s2_oras_build.py        # AS/OR  -> *_ArceusFormType.3ds   (edit the paths at the bottom)
python s2_build_port.py        # US     -> UltraSun_*.3ds
python s2_build_final.py       # UM     -> UltraMoon_*.3ds (+ Silvally + Battle.cro Protean)
```
Verified in Unicorn end-to-end (no-plate Ghost form → Ghost; Earth Plate → Ground; etc.); Ultra Moon
was additionally confirmed in-game.

**Other gens:** Platinum = `../../gen4_platinum/platinum_arceus_formtype.patch` (pret source patch);
BW2 = `../../gen45_nds_arceus_typefix/`.
