# PKHaX change: Gen 1/2 level up to 255

Lets HaX mode set any Gen-1 (PK1) or Gen-2 (PK2) Pokemon's stored level to 255. RBY/GSC read the level
byte directly and never clamp a pre-existing value to 100, so it persists in-game. Three changes, all
gated on `GBPKM` (base of PK1/PK2) so no other generation is affected. `Stat_Level` (PK1 Data[0x21] +
mirror Data[0x03]; PK2 Data[0x1F]) already accepts a full byte — only the clamps below were lifted.

## 1. UI unlock + stamp the stored byte
`PKHeX.WinForms/Controls/PKM Editor/PKMEditor.cs` — `UpdateEXPLevel`, the `else` ("Change the XP") branch.

BEFORE:
```csharp
        else
        {
            // Change the XP
            var input = Util.ToInt32(TB_Level.Text);
            var level = (byte)Math.Clamp(input, Experience.MinLevel, Experience.MaxLevel);
            if (input != level && !string.IsNullOrWhiteSpace(TB_Level.Text))
                TB_Level.Text = level.ToString();
            TB_EXP.Text = Experience.GetEXP(level, gr).ToString();
        }
```
AFTER:
```csharp
        else
        {
            // Change the XP
            var input = Util.ToInt32(TB_Level.Text);
            // PKHaX: Gen 1/2 store level as a raw byte the game reads directly (RBY/GSC never clamp to 100).
            // In HaX mode on a GB mon, allow up to 255 and stamp the stored level byte (Stat_Level) directly.
            var maxLvl = (HaX && Entity is GBPKM) ? byte.MaxValue : Experience.MaxLevel;
            var level = (byte)Math.Clamp(input, Experience.MinLevel, maxLvl);
            if (input != level && !string.IsNullOrWhiteSpace(TB_Level.Text))
                TB_Level.Text = level.ToString();
            if (level > Experience.MaxLevel)
            {
                // EXP can only encode up to L100; peg EXP at the L100 minimum and write the byte the game uses.
                TB_EXP.Text = Experience.GetEXP(Experience.MaxLevel, gr).ToString();
                Entity.Stat_Level = level;
            }
            else
            {
                TB_EXP.Text = Experience.GetEXP(level, gr).ToString();
            }
        }
```

## 2. Stop the party-stat refresh from resetting the level
`PKHeX.Core/PKM/PKM.cs` — `ResetPartyStats`.

BEFORE:
```csharp
        SetStats(stats);
        Stat_Level = CurrentLevel;
        Status_Condition = 0;
```
AFTER:
```csharp
        SetStats(stats);
        // PKHaX: preserve an intentionally over-leveled Gen 1/2 stored level (RBY/GSC read this byte
        // directly and never clamp it to 100). Normal mons still get the EXP-derived level.
        if (this is not GBPKM gb || gb.Stat_Level <= CurrentLevel)
            Stat_Level = CurrentLevel;
        Status_Condition = 0;
```

## 3. (NOT applied here — optional) stats at the real level
Left out on purpose: computing stats at level 255 overflows the 16-bit stat fields (garbage stats). The
level persists without it; the game recomputes party stats at the stored level on a box withdraw anyway.
If you want it, in `PKHeX.Core/PKM/Shared/GBPKM.cs` `LoadStats`, change
`var lv = CurrentLevel;` to `var lv = Stat_Level > CurrentLevel ? Stat_Level : CurrentLevel;`.

## Build
`dotnet publish PKHeX.WinForms/PKHeX.WinForms.csproj -c Release -r win-x64` (the csproj already sets
`EnableWindowsTargeting`, single-file, framework-dependent). Needs the .NET 10 Desktop Runtime on the
target Windows machine.
