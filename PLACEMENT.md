# Where each patched file goes

Copy each file below over the same path inside a fresh PKHeX source tree
(`PKHeX-master/`). Paths are relative to the PKHeX repo root. Nine files are
modified; one (`G1Editor.cs`) is new.

| Patched file (this folder)                                   | Destination in PKHeX repo                                    | New? | Feature |
|--------------------------------------------------------------|--------------------------------------------------------------|------|---------|
| PKHeX.Core/PKM/PK1.cs                                         | PKHeX.Core/PKM/PK1.cs                                         | mod  | Gen-1 sprite desync + type retention |
| PKHeX.Core/PKM/Shared/PokeList1.cs                          | PKHeX.Core/PKM/Shared/PokeList1.cs                          | mod  | Gen-1 read/write of header (sprite) species |
| PKHeX.Core/PKM/PK3.cs                                         | PKHeX.Core/PKM/PK3.cs                                         | mod  | Gen-3 AbilityOverride byte (0x1E) |
| PKHeX.Core/PKM/Shared/G3PKM.cs                             | PKHeX.Core/PKM/Shared/G3PKM.cs                             | mod  | Gen-3 Ability getter returns override |
| PKHeX.WinForms/Controls/PKM Editor/G1Editor.cs            | PKHeX.WinForms/Controls/PKM Editor/G1Editor.cs            | NEW  | Gen-1 Sprite/Type1/Type2 editor control |
| PKHeX.WinForms/Controls/PKM Editor/PKMEditor.cs          | PKHeX.WinForms/Controls/PKM Editor/PKMEditor.cs          | mod  | Hosts G1Editor; Gen-3 ability dropdown |
| PKHeX.WinForms/Controls/PKM Editor/EditPK1.cs            | PKHeX.WinForms/Controls/PKM Editor/EditPK1.cs            | mod  | Load/save Gen-1 sprite + types |
| PKHeX.WinForms/Controls/PKM Editor/EditPK3.cs            | PKHeX.WinForms/Controls/PKM Editor/EditPK3.cs            | mod  | Load/save Gen-3 any-ability |
| PKHeX.WinForms/Controls/Slots/SummaryPreviewer.cs       | PKHeX.WinForms/Controls/Slots/SummaryPreviewer.cs       | mod  | Routes Gen-1 info into the hover preview box |
| PKHeX.WinForms/Controls/Slots/PokePreview.cs            | PKHeX.WinForms/Controls/Slots/PokePreview.cs            | mod  | Renders Gen-1 species/sprite/typing in the hover box |

Base revision: PKHeX master as of June 2026 (the `PKHeX-master` you supplied). The Core files were
re-derived against that exact revision; the WinForms files apply cleanly to it. Every change is
tagged with a `// PKHaX` comment (grep for `PKHaX`).

Build (Windows, .NET 10 SDK):
    dotnet publish PKHeX.WinForms -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
Then rename the output `PKHeX.exe` to `PKHaX.exe` to enable illegal/HaX mode.
