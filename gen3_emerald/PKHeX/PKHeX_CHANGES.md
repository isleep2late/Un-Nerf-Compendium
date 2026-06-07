# PKHeX changes for Gen-3 arbitrary abilities

Built against PKHeX master (TargetFramework net10.0-windows). Four files changed. Rebuild with
`dotnet publish PKHeX.WinForms -c Release -r win-x64 -p:PublishSingleFile=true` (or just use the
included `PKHeX.exe`).

The override is stored in the **PK3 Sanity word, byte 0x1E** (low byte): 0 = no override, ≥2 = force that ability ID.

## 1. `PKHeX.Core/PKM/PK3.cs` — add the override property
After the `AbilityBit` property:
```csharp
public int AbilityOverride { get => Data[0x1E]; set => Data[0x1E] = (byte)value; }
```

## 2. `PKHeX.Core/PKM/Shared/G3PKM.cs` — make Ability return the override
So tooltip / summary / Showdown export all show the real ability. Change the `Ability` getter:
```csharp
public sealed override int Ability { get => (this is PK3 p3o && p3o.AbilityOverride >= 2) ? p3o.AbilityOverride : PersonalInfo.GetAbility(AbilityBit); set { } }
```
(Gated to PK3 so Colosseum/XD CK3/XK3 are unaffected.)

## 3. `PKHeX.WinForms/Controls/PKM Editor/EditPK3.cs` — GUI load/save
- `PopulateFieldsPK3`: for PK3, set CB_Ability's DataSource to the full ability list and select
  `AbilityOverride` (if ≥2) else the slot ability.
- `PreparePK3`: read the chosen ability; if it equals the species' slot-0/slot-1 ability, write the
  slot bit and clear the override; otherwise write `AbilityOverride = chosen id`.

## 4. `PKHeX.WinForms/Controls/PKM Editor/PKMEditor.cs` — populate full list + gate handlers
- `SetAbilityList`: for PK3, use `GameInfo.FilteredSources.Abilities` (all abilities ≤ MaxAbilityID = all
  78 for Gen 3) instead of the 2-slot list; restore selection by value (using binding-safe
  `SelectedItem`/`SelectedIndex`, never `SelectedValue`, to avoid an InvalidCastException during
  `InitializeBinding`).
- Gate the two CB_Ability change handlers with `Entity is not PK3` so they don't reroll the PID or call
  `SetAbilityIndex` for Gen 3 (the override is handled in Prepare/Populate instead).

## Notes
- Byte 0x1E is outside the PK3 checksum, verified to survive encrypt→decrypt round-trip with the checksum
  still valid (tested on a real Emerald save via PKHeX.Core).
- This pairs with the Emerald `AnyAbility` / `Full` IPS patch — PKHeX writes 0x1E, the ROM reads it.
