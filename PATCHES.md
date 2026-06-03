# Patch reference (Gen 7 · Ultra Sun / Ultra Moon)

Internal technical notes for the patches embedded in `unnerf.py`. Offsets and bytes were
found by reverse-engineering and verified against the live game (Citra/Azahar) and the
on-disk files. Title IDs: **Ultra Sun `00040000001B5000`**, **Ultra Moon `00040000001B5100`**.

The two ability patches live inside **`Battle.cro`** (the battle-engine module in RomFS) and
are **identical in both games** — only Battle.cro's location inside the `.cia` differs
(US `0x679D20`, UM `0x67DD20`), which the tool finds automatically by parsing the RomFS.

After any edit, the tool recomputes the affected IVFC master-hash slot(s) and the NCCH
RomFS superblock hash; TMD/content signatures are not enforced by the emulators and are
left untouched.

---

## 1. Prankster — remove the Dark-type immunity (Gen 7 nerf)

- **Where:** `Battle.cro` file offset **`0x24B14`**.
- **Change:** `D1 FF FF 0A` → `D1 FF FF EA`  (a single-byte `0A`→`EA`: `BEQ`→`B`).
- **Meaning:** in the unique `hasType(target, Dark)` enforcement branch, the conditional
  "skip if Dark" jump is turned into an unconditional fall-through, so a Prankster-boosted
  status move is no longer dropped against Dark-types.

## 2. Gale Wings — remove the full-HP requirement (Gen 7 nerf)

- **Where:** `Battle.cro` file offset **`0xDA514`**.
- **Change:** `09 00 00 0A` → `00 F0 20 E3`  (`BEQ`→`NOP`).
- **Meaning:** the `onModifyPriority` handler (event 0x11, function @vaddr `0x007B74E4`)
  reads `IsFullHP`; the "skip the +1 if not at full HP" branch is NOP'd, so the +1 Flying
  priority applies at any HP. The Flying-type check is preserved, so non-Flying moves are
  unaffected. Mirrors Showdown's pre-Gen-7 `if (move.type==='Flying' && hp===maxhp) priority+1`.

## 3. Parental Bond — restore the 0.5× second hit (Gen 7 nerf)

- **Where:** `Battle.cro` file offset **`0x24EAC`**.
- **Change:** `01 0B A0 13` → `02 0B A0 13`  (`movne r0,#0x400`→`movne r0,#0x800`).
- **Meaning:** in the per-hit damage-modifier setup (move-execution fn @vaddr `0x00701D68`,
  confirmed on the live second-hit call chain), the 2nd+ strike of a reduced-multistrike move
  loads modifier `0x400` (= 0.25× in Q12, where `0x1000` = 1.0×) and passes it to the damage
  builder (`0x75BCE4`); the first hit uses `0x1000`. Changing `0x400`→`0x800` makes the second
  hit 0.5× (Gen 6). Regular multi-hit moves (Bullet Seed, etc.) take the 1.0× path and are
  unaffected. Live-confirmed: 2nd hit ~50% of the first (was ~25%).

## 4. No Ban List — lift the Battle Tree restrictions

- **Where:** a cluster of **11 ban records** in the Battle Tree data inside RomFS.
- **Change:** each record's two key fields are zeroed (the ban entry becomes inert).
- **Offsets:** title-specific (different RomFS layout per game) — the exact absolute `.cia`
  offsets and expected original bytes are embedded in `unnerf.py` (`NBL_TABLE`). Span:
  UM `0x92129C32–0x9212F9A5`, US `0x921260A2–0x9212BE15`. All edits fall in IVFC master
  slot 36 for both titles.
- **Provenance:** derived by byte-diffing a stock `.cia` against a hand-made
  no-ban-list `.cia`; the data-region differences are exactly these 11 records (everything
  else in the diff was recomputed hashes, which the tool regenerates).

## 5. Species Clause + Item Clause — lift the duplicate restrictions

- **Where:** the Battle Tree rule table **`a/1/4/1`** (RomFS GARC; 25 rule records, 1192 /
  `0x4A8` bytes each). Located dynamically by name `"1"` + size `30372` + `CRAG` magic.
- **Change:** in each of the **14** facility rule records that enforce the clauses, set two
  flag bytes from `01`→`00`: **Species Clause @ record+`0x0E`**, **Item Clause @ record+`0x0F`**.
- **File-relative offsets (identical in US and UM):** `0x6F2/0x6F3, 0x14EA/0x14EB,
  0x22E2/0x22E3, 0x30DA/0x30DB, 0x3A2A/0x3A2B, 0x3ED2/0x3ED3, 0x437A/0x437B, 0x4822/0x4823,
  0x4CCA/0x4CCB, 0x5172/0x5173, 0x561A/0x561B, 0x5AC2/0x5AC3, 0x5F6A/0x5F6B, 0x6412/0x6413`.
  `unnerf.py` adds `a/1/4/1`'s absolute base to these (`CLAUSE_REL_OFFSETS`). Same IVFC
  master **slot 36** as the No-Ban-List records (same file), so it's free to bundle.
- **Why it works:** the rules screen and the team validator both read these per-record flags;
  the NBL edit only zeroes the embedded banned-Pokémon records, which is why the clauses
  survived it. Folded into the `nbl` and `all` modes ("No Restrictions"). Live-confirmed:
  rules screen shows Species/Item permitted and duplicate species/items are accepted.

## 6. Soul Dew — restore Gen-6 +50% Sp. Atk / Sp. Def (code injection)

- **Not a byte flip.** Gen 7 deleted the +50% stat logic and replaced it with a +20%
  Psychic/Dragon move-power effect, so the patch *adds* a new handler rather than editing one.
- **Where:** `Battle.cro` (byte-identical in US and UM, so the same offsets/bytes work for both).
  - A **184-byte handler** is written into the inter-segment code cave at file offset
    **`0xFC980`** (vaddr `0x7D9980`) — the page-padding between `.text` and `.rodata`, which is
    genuinely free at runtime *and* inside `.text`'s executable page. (Interior "zero gaps" in
    `.text` are relocated pointer tables — zero on disk, overwritten at load — so they cannot be
    used; this gap is the only safe one.)
  - Soul Dew's existing handler entry at file **`0xBBA10`** is repointed:
    `70 40 2D E9` (`push {r4,r5,r6,lr}`) → `DA 03 01 EA` (`b 0x7D9980`).
- **What the handler does:** on the damage-modify event (`0x47`), arg `0x35` (a Q12 multiplier,
  `0x1000` = 1.0×): if the holder is the **attacker** and the move is **special** and the species
  is Latios/Latias (`0x17C/0x17D`, covers Megas) → set `0x1800` (1.5× outgoing = +50% Sp. Atk);
  if the holder is the **defender** under the same conditions → set `0xAAB` (0.667× incoming =
  +50% Sp. Def). Calls the engine's own GetEventArg/GetParam/SetEventArg helpers.
- **IVFC:** both edits fall inside `Battle.cro`; the tool recomputes the affected master
  slot(s) across the full byte range. **Live-confirmed** by damage: a resisted Psychic dealt
  12 to a Lv50 Latios — below the 15-damage floor any unboosted Slowbro could deal — proving the
  Sp. Def half; Oblivion Wing (non-STAB) confirmed the Sp. Atk half. In `souldew` and `all` modes.

---

## IVFC notes

- RomFS layout: `[IVFC header 0x60][master-hash region][L3 = file data]`.
- The emulator recomputes L2/L1 from L3 at load and checks against the stored master
  region, then checks the master region against the NCCH `romfs_superblock_hash`
  (NCCH header `+0x1E0`). Updating those two is sufficient; intermediate levels and
  TMD/signatures are not enforced.
- Master slot for an edit at CIA offset `O`:
  `(((O - L3_start) / 0x1000) / 128) / 128`. Recomputing one slot reads ~64 MB of L3.
