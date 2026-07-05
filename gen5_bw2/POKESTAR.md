# Pokéstar Studios props in Black 2 / White 2 — usable Pokémon, no Bad Egg

Pokéstar Studios movie opponents live in BW2 at internal species indices **652–684** (above the
National-Dex max of 649). They have real personal data and battle sprites, but the game never lets you
own one, and injecting one made it display as a **Bad Egg**. This documents what the Bad Egg actually
is, why the common "species > 649" theory is wrong, and the fix that ships in the BW2 patch + PKHaX.

## The 17 user-facing props

| Species | Showdown name | Species | Showdown name |
|--------:|---------------|--------:|---------------|
| 652 | Pokestar UFO         | 663 | Pokestar Black Door |
| 653 | Pokestar Brycen-Man  | 665 | Pokestar UFO-PropU2 |
| 654 | Pokestar MT          | 680 | Pokestar UFO-2 |
| 655 | Pokestar MT2         | 682 | Pokestar F-002 |
| 656 | Pokestar Transport   | 683 | Pokestar Black Belt |
| 657 | Pokestar Giant       | 684 | Pokestar Smeargle |
| 658 | Pokestar Humanoid    |     | |
| 659 | Pokestar Monster     |     | |
| 660 | Pokestar F-00        |     | |
| 661 | Pokestar Spirit      |     | |
| 662 | Pokestar White Door  |     | |

Every prop except 684 gets **BST 100** (100/100/100/100/100/100). **Pokestar Smeargle (684)** keeps
real Smeargle stats. Names match Pokémon Showdown's export exactly.

## The Bad Egg is NOT a species check

A "Bad Egg" in Gen 4/5 is shown for a Pokémon **only when its checksum fails** (`checksumFailed == TRUE`).
There is no species-number gate anywhere in that path:

- The per-mon validator at arm9 `0x201DDC8` decrypts the 4 data blocks, computes a plain u16 sum, and
  sets `checksumFailed` (bit 2 of `mon+4`) **only** when that sum ≠ the stored checksum
  (`0x201DDEE: cmp r0,r1; bne → strh [r5,#4]`). No species comparison exists in it.
- `GetBoxMonData` (`0x201DEC8`) renders a mon as Egg/Bad-Egg when `isEgg || checksumFailed` — again no
  species term.
- The Gen-4 `pokeplatinum` decomp shows the identical design, and its checksum routine is byte-for-byte
  what PKHeX/PKHaX computes.

So a prop written with a valid checksum is a **fully valid Pokémon**, and no "raise the 649 bound" arm9
gate-flip can fix a Bad Egg — there is no gate to flip.

## What it actually is: a checksum-desync timing race

Save-state forensics settled it. Two melonDS states of the *same* save — one where the prop displays
correctly, one where it's a Bad Egg — have:

- **Identical, clean arm9** (no patches, gates = 649).
- A **byte-identical, valid prop** (species 660, `ChecksumValid == true` in both).
- The **only** difference: the working state has `mon+4 == 0x00`; the Bad-Egg state has
  `mon+4 == 0x04` (`checksumFailed` set). The flag is **not stored in the save** (the `.sav` prop is
  valid) — it's recomputed every load.

During party-load the game briefly modifies a prop's data block (observed at decrypted offset 0x5E–0x5F)
without the checksum being in sync at that instant. If the validator runs inside that window it sees a
transient mismatch and sets the **sticky** `checksumFailed` bit → Bad Egg for the rest of the session.
Whether the window is hit is emulation-timing dependent: **melonDS usually avoids it, DeSmuME hits it
consistently** — which is exactly the "works in one emulator, Bad Egg in the other, same ROM+save"
behavior people reported.

## The fix (arm9)

A minimal code-cave hooked at the validator's flag-set site. Before setting `checksumFailed`, the cave
reads the mon's species from the freshly-decrypted blocks — computing the Gen-5 block shuffle from the
PID (`sv = ((PID >> 13) & 0x1F) % 24`, block-A physical position from the standard permutation table) —
and if the species is a prop (652–684) it takes the validator's **valid** path and never sets the flag.
Non-props are byte-for-byte unchanged, so genuinely corrupt saves still show Bad Eggs.

- Applied by `tools/bw2_pokestar_validator_fix.py` (validator auto-located by prologue signature; works
  for both Black 2 and White 2). Only arm9 is touched; it is recompressed on the normal boot path.
- Verified by disassembly **and** by Unicorn-emulating the patched validator across every PID shuffle
  value: props → valid (flag clear); non-props (incl. edge cases 651 and 685) → flagged as before.

## The fix (PKHaX)

BW2 prop indices 652–684 collide with Gen-6 National-Dex numbers (652 = Chesnaught, 660 = Diggersby,
661 = Fletchinder …), so **every prop lookup is gated to Gen-5 context** and Gen-6+ editing is untouched.
Props: carry their exact Showdown names for import/export, appear in the B2W2 species dropdown/search,
read as registered dex mons with BST 100 (Smeargle real), show their Pokéstar name on hover instead of
the wrong-gen dex name, and preview an extracted sprite in place of the Gen-6 Pokémon.

## Build / apply

```
# simplest -- apply the one prebuilt patch (does everything):
xdelta3 -d -s "Black 2.nds" Black2_UnNerf_Pokestar.xdelta Black2_UnNerf_Pokestar.nds

# or build it yourself from a clean ROM (identical result; needs: pip install ndspy):
python3 bw2_nobanlist.py "Black 2.nds"                      # -> Black2_UnNerf_Pokestar.nds
python3 tools/bw2_pokestar_build.py "Black 2.nds" --out Black2_UnNerf_Pokestar.nds   # same thing, explicit
```

The full patch = ban-list removal + clause removal (`a/1/0/6`) + Arceus form-typing (`a/0/1/6`) + this
Pokéstar validator fix (arm9) + the prop back-sprite fix (`a/0/0/4`). `bw2_nobanlist.py` produces a
byte-identical ROM to the shipped `*_UnNerf_Pokestar.xdelta`. Test from a **clean boot**, not an old save
state.
