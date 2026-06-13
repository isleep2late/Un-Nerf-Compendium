# Features and how to disable each one

This is the "I want everything EXCEPT feature X" guide. Each game ships as a small set of patches; for
every feature below you get the exact thing to remove. Two patch kinds:

- **Source patches** (`.patch` for the decomp games, `.py` scripts): editable as TEXT. Every feature is
  wrapped in a tagged block - search for the tag and delete that block, then re-apply / rebuild. Tags
  used: `// UN-NERF [name]`, `// PKHaX [name]`, `# UNNERF: [name]`.
- **Binary patches** (`.ips`, `.xdelta`, exefs `.ips`/`.pchtxt`): you cannot comment a binary diff. To
  drop a feature, use the per-feature **single-purpose patch** instead of the all-in-one, or skip the
  listed byte edit. The per-feature breakdown is given below so you can rebuild a custom binary.

---

## Gen 3 - Emerald  (`gen3_emerald/`)
The all-in-one is `3_Emerald_Full_Hackmons_v3.ips` = patch 1 + patch 2 + flash-save fix. To keep only
some features, apply the **single-purpose** IPS files instead:
- **Frontier unban + clauses + Soul Dew un-nerf** -> apply only `1_Emerald_FrontierUnlock_SoulDew.ips`.
  To DISABLE just this: don't apply patch 1 (or, in the decomp `.patch`, delete the `// UN-NERF Frontier`
  and `// UN-NERF SoulDew` blocks).
- **Any-ability (PK3 0x1E)** -> apply only `2_Emerald_AnyAbility.ips`. DISABLE: don't apply patch 2
  (decomp tag `// PKHaX AnyAbility`).
- **Deoxys forms** -> in the decomp source `.patch`, delete the blocks tagged `// UN-NERF Deoxys*`
  (stats / sprites / icons / summary / default-form / trade) and rebuild. Each sub-feature is its own tag.
- **6-Pokemon party** -> `emerald_6pokemon_full.patch`; don't apply it (or delete `// UN-NERF PartySize`)
  to keep legal party sizes.

## Gen 4 - Platinum  (`gen4_platinum/`)
- All-in-one binary: `Platinum_unbanned_species_item_clause_formefix.xdelta`. Source: the `.patch`
  files, each feature tagged `// UN-NERF` / `// PKHaX`.
- **Ban list** -> the arm9 banned-species zero. Source tag `// UN-NERF BanList`.
- **Species + Item Clause** -> two arm9 gates (team-select + Tower re-check). Tag `// UN-NERF Clauses`.
- **Forme persistence (Giratina-O/Rotom/Shaymin-Sky)** -> overlay 5 `0x021F6DC2: C0 46 -> 01 D1` to
  REVERT to stock (restore the check). Tag `// UN-NERF FormePersist`.
- **Giratina-O no-orb battle revert kill** -> overlay 16 `0x02259FBD: E0 -> D1` to restore stock.
  Tag `// UN-NERF GiratinaBattleRevert`.
- **Soul Dew un-nerf** -> overlay `0x0225A5F0` + `0x0225A61E` (restore stock bytes to re-nerf).
  Tag `// UN-NERF SoulDew`.
- **Arceus doubles typing** -> the doubles eligibility/distinctness clause neutralization. Tag
  `// UN-NERF ArceusDoubles`.
- **6-Pokemon** -> `platinum_6pokemon_singles.patch`; omit to keep legal sizes. Tag `// UN-NERF PartySize`.
- **AbilityLock (forme keeps hacked ability)** -> `Platinum_AbilityLock_pokemon_c.patch`; omit to let
  formes recompute the default ability. Tag `// PKHaX AbilityLock`.
- **Arceus form-typing** -> `platinum_arceus_formtype.patch`; omit to keep stock plate-only typing.
  Tag `// UN-NERF ArceusFormType`.

## Gen 5 - Black 2 / White 2  (`gen5_bw2/`)
Binary (regulation NARC a/1/0/6). Use the Python `bw2_nobanlist.py` and edit its `MODE`/flags to pick
features, or apply the prebuilt `*_NoRestrictions.xdelta`.
- **Ban list / Species Clause / Item Clause** -> `bw2_nobanlist.py`; comment the block marked
  `# UNNERF: banlist/clauses` to keep stock.
- **Arceus type-by-form** -> `gen45_nds_arceus_typefix/bw2_arceus_typefix.py`; don't run it to keep
  stock Multitype-only typing.
- (6-mon and forme-persist are NOT included for BW2 - see PROJECT_STATE.)

## Gen 6 - ORAS  (`gen6_oras/`, `gen67_arceus_typefix/`)
Python patchers - each feature is a function/flag you can comment out:
- **Battle Maison unban / clauses / team-size / EV cap** -> `oras_nobanlist.py` + `oras_no_restrictions.py`
  + `oras_evcap.py`. Run only the ones you want. In `oras_no_restrictions.py` the team-size and EV-cap
  edits are separate, tagged blocks.
- **Forme persistence + Hoopa** -> `gen6_oras/formepersist.py` (run with `--full` for Hoopa). Skip to
  keep stock reverts.
- **Arceus form-typing (getter cave)** -> `gen67_arceus_typefix/`; skip to keep stock plate-only typing.

## Gen 7 - USUM  (`gen7_usum/`, `gen67_arceus_typefix/`)
- **Battle Tree unban / clauses** -> `gen7_usum/unnerf.py --mode nbl`.
- **Prankster / Gale Wings / Parental Bond / Soul Dew un-nerfs** -> `unnerf.py --mode prankster` /
  `galewings` / `parentalbond` / `souldew` (or `all`). Each is an independent mode; run only what you
  want. The matching in-game text edits are applied by `gametext.py` and gated to the modes you choose.
- **Forme persistence + Hoopa** -> `gen7_usum/formepersist.py`.
- **Arceus + Silvally form-typing** -> `gen67_arceus_typefix/` (USUM path).
- **Protean on Arceus/Silvally** -> the Battle.cro species-list clear (`{0x1ED,0x305} -> 0xFFFF` at
  +0x102670). To DISABLE (keep their type locked), skip that one 4-byte edit (it lives in the USUM
  arceus-typefix build step).

## Gen 8 - SwSh  (`gen8_swsh/`)
LayeredFS - pick folders. Per title-ID you get `NoTowerClause/`, `FormePersist/`, `DynamaxCandyAll/`.
Include only the subfolders for the features you want; each is a self-contained exefs overlay.

## BDSP  (`bdsp/`)
LayeredFS - the exefs `noclause` overlay is the only feature (Battle Tower banlist + clauses). Omit it
to keep stock.

---

### Why not "one patch per game with inline comments" for the binary games
A binary diff (`.ips`/`.xdelta`) is just "write these bytes here" - there is no text to comment. So for
the binary games the feature granularity is provided as **separate single-purpose patches** (above), and
the decomp games (Emerald, Platinum) - which DO have editable source - carry the inline `// UN-NERF` /
`// PKHaX` tags you can delete to drop a feature before rebuilding.
