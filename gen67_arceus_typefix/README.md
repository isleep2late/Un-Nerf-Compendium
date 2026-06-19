# Arceus / Silvally form-driven typing (Gen 6/7)

Gives **Arceus (#493)** and **Silvally (#773)** real per-form types so a form set in PKHeX
behaves as that type **without needing to hold the Plate/Memory** — fixing the classic
"looks Ghost, plays Normal" problem.

## Why it's needed

Both species ship with **`FormStatsIndex = 0`** in the personal table (`a/0/1/7`), i.e. **no
per-form entries** — every form falls back to the base **Normal/Normal** entry. So the form is
purely cosmetic; the battle type is supplied only by **Multitype / RKS System** reading the held
**Plate / Memory**. Set the form in PKHeX but don't hold the item and the mon is Normal-typed.

(Verified against the retail USUM table: Deoxys/Rotom/Giratina/Hoopa/Furfrou all have non-zero
FormStatsIndex; only Arceus and Silvally are 0.)

## What the tool does

Arceus and Silvally form indices equal the **gen-6/7 internal type id** (0 Normal, 1 Fighting …
7 Ghost … 17 Fairy), so the fix is an **identity map**: each new form entry is the base entry with
`Type1 = Type2 = formIndex`. The tool appends 17 leaf entries per species (every original index
preserved, including the trailer block), and repoints each base entry's `FormStatsIndex`. The engine
already loads personal data **by form** (`pml::personal::LoadPersonalData(MonsNo, form)`), so the
new types are read automatically. Holding the matching Plate/Memory still works (Multitype/RKS
override as normal).

## Use

```
# 1) extract a/0/1/7 from your decrypted dump (pk3DS/pkNX "extract RomFS", or 3dstool)
python arceus_silvally_typefix.py  a/0/1/7  -o a_0_1_7_typed
# 2) put a_0_1_7_typed back as a/0/1/7 and REBUILD the RomFS (pk3DS/pkNX, or 3dstool)
#    then recompute the NCCH RomFS-superblock hash (+ CIA TMD). This is a normal RomFS rebuild.
```

`--species 493 773` (default) — pass your own list if wanted. `--verify` reports current state.
Works for **OR/AS and US/UM** (identical personal layout).

## Important notes

- This is a **length-changing** RomFS edit (+17×84 B per species), so it needs a real **RomFS
  rebuild**, not an in-place byte patch. pk3DS/pkNX do this for you; a pure byte-patcher cannot.
- **Behavioural check:** after building, a Ghost-form Arceus with **no Plate** should now read as
  Ghost in battle. If it still shows Normal, the ability is force-setting the type from the held
  item every turn (active, not passive) — in that case the type must instead be driven by a small
  Battle.cro edit; open an issue. (In testing the personal route is expected to work because
  Multitype/RKS only act when the item is held.)
- Pairs with `gen67_formepersist/` — the form already persists across save+reload after that patch,
  so the form now carries both the look **and** the type.

## Gen 4 / Gen 5 ports

Platinum/BW2 use an NDS `personal` NARC with a different entry layout; the same idea applies
(add per-form types for Arceus, repoint its form index) but the NARC must be rebuilt with ndspy.
Confirm Arceus's form/personal layout in the pokeplatinum / pokeblack2 decomp first.
