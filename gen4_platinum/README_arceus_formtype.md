# Platinum — permanent Arceus form-typing (source patch)

`platinum_arceus_formtype.patch` makes a PKHeX'd Arceus keep its form's type, against the pret
[`pokeplatinum`](https://github.com/pret/pokeplatinum) decomp (US, rev 1).

**Behaviour:** Hold the matching Plate → Multitype fires (plate type), exactly like vanilla. No plate
→ the Pokémon's *form* decides the type (gen-4 Arceus form id == type id, e.g. Ghost form = Ghost),
and the form is no longer reset to Normal on view/battle/restart.

## What it changes (3 edits)
- `src/battle/battle_lib.c` — `Battler_MonType`: the Multitype `default:` case now returns the
  battler's `formNum` instead of `TYPE_NORMAL` (no-plate → form's type in battle/damage calc).
- `src/battle/battle_lib.c` — the battle-start Multitype form-change now only re-derives the form
  when an actual Plate is held (no-plate → form left intact).
- `src/pokemon.c` — `BoxPokemon_SetArceusForm` now only rewrites the saved form when an actual Plate
  is held (stops PKHeX forms being overwritten with Normal when you view the party/box).

All three are length-neutral logic changes gated on the held item being an Arceus Plate
(`HOLD_EFFECT_ARCEUS_FIRE … HOLD_EFFECT_ARCEUS_STEEL`).

## Apply + build
```sh
git clone https://github.com/pret/pokeplatinum && cd pokeplatinum
git checkout <the rev these line numbers match>      # US/rev1
git apply ../platinum_arceus_formtype.patch          # or: patch -p1 < ../platinum_arceus_formtype.patch
# follow pret/pokeplatinum INSTALL.md (devkitARM + mwccarm) to build pokeplatinum.us.nds
```

## Combining with the un-nerf patch
The un-nerf (`Platinum_unbanned_species_item_clause_formefix.xdelta`) edits *data* (banlist / item
clause); this fix edits *code* (arm9 + battle overlay) — disjoint regions. To ship one ROM with both,
build the fixed clean ROM above, then diff it against an unmodified clean build to get the exact
byte-level code delta, and apply that same delta onto your already-un-nerfed `Platinum_UNNERFED.nds`
(the code bytes the un-nerf touches don't overlap these functions). That keeps the un-nerf intact and
layers the Arceus fix on top.
