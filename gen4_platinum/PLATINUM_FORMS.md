# Platinum: removing Battle Frontier FORM restrictions

> **This is now automated** — run `platinum_forms.py` (or `apply_platinum_forms.bat`, or apply
> `platinum_forms.ups`) and you're done; you do **not** need DSPRE. The script reproduces exactly
> the edits below via a length-preserving byte change: it rewrites each check's `CompareVarValue
> 32780 255` constant to `0xFFFF`, so the `JumpIf EQUAL` to the revert handler can never fire —
> equivalent to deleting the run, but without shifting any script jump targets. It patches the 13
> checks in scripts 367/377/378/379 and leaves identical-looking checks elsewhere untouched. The
> manual DSPRE walkthrough below is preserved as reference.

---

## Manual walkthrough (original, via DSPRE)

The banlist patch (`platinum_nobanlist.py`) lets banned **species** enter the Frontier. A
*separate* restriction makes alternate **forms** (Origin Giratina, Rotom forms, Sky Shaymin)
revert to their base form. That check lives in the field **scripts**, not in a fixed banlist
byte, so it can't be safely auto-patched as a byte edit without an NDS emulator to test against
(editing compiled Gen-4 script bytecode shifts jump targets). It's a quick, reliable edit in
**DS Pokemon ROM Editor (DSPRE, Reloaded build)** — credit to SmolJoltik (PP topic 67882).

Open your Platinum ROM in DSPRE, go to each Script file below, find the listed Function, and
**delete** the listed command run(s). Then Save Scripts -> Save ROM. (Function numbers can shift
if other patches are applied, so match by the command pattern.)

**Battle Tower - Script 367**
- Function 90 (Singles/Doubles): delete the three repeats of
  `CMD_477 53 <0/1/2> 32780` / `CMD_798 32780 32780` / `CompareVarValue 32780 255` /
  `JumpIf EQUAL Function#111`
- Function 114 (Multi): delete the two repeats of the same run (slots 0 and 1)

**Battle Hall - Script 377, Function 79** - delete the two repeats:
  `CMD_798 16386 32780 / CompareVarValue 32780 255 / JumpIf EQUAL Function#86` and the `16389` one

**Battle Castle - Script 378, Function 29** - delete the three repeats:
  `CMD_798 16386/16389/16390 32780 / CompareVarValue 32780 255 / JumpIf EQUAL Function#33`

**Battle Arcade - Script 379, Function 29** - identical to Battle Castle; delete the same three.

(The Factory rents Pokemon, so it has no such check.) Known cosmetic side effect: Origin Giratina
keeps Origin stats/ability in battle but its held Griseous Orb is removed by the Castle/Arcade, so
it visually reverts to Altered until you leave - harmless.
