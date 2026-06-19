# Gen 4/5 (NDS) Arceus form-driven typing

Same idea as `gen67_arceus_typefix/`, for the NDS personal NARC. Arceus has no per-form personal
entries, so every form is Normal/Normal and the type comes only from Multitype + Plate.

## Gen 5 — Black 2 / White 2  (data-only, like gen 6/7)
BW2 personal has `FormStatsIndex` (Arceus = 0, FormCount 17). `bw2_arceus_typefix.py` appends 16
typed form entries (Fighting…Dark — gen 5 has no Fairy) and repoints FormStatsIndex, rebuilding the
NARC + NDS with ndspy. Run on a clean/NO-NERF Black2 or White2 `.nds`. `pip install ndspy`.

## Gen 4 — Platinum  (data + a small arm9 code patch)
Gen 4 has NO `FormStatsIndex`; the form→personal mapping is hardcoded in `Pokemon_GetFormNarcIndex`
(pokeplatinum `src/pokemon.c`), a `switch(species)` with no Arceus case. Fix =
1. append 16 Arceus form entries to `poketool/personal/pl_personal.narc` (indices 508–523), and
2. inject an Arceus case into the compiled `Pokemon_GetFormNarcIndex` in arm9:
   `case SPECIES_ARCEUS: if (form && form<=16) species = (508-1)+form; break;`
The arm9 hook (code cave) is small but must be built + tested against the exact ROM; do it with the
decomp open. (The NO-NERF Platinum's other arm9/overlay edits are documented in RE_KNOWLEDGE_BASE.)
