# Paste-ready prompt for Claude Code (run LOCALLY on Windows, where it can build + test)

Use this on your own machine with Claude Code, in a terminal that has `git`, the `gh` CLI (logged in),
and the **.NET 10 SDK** installed. It re-bases our PKHaX changes onto the latest upstream PKHeX,
rebuilds, tests, and reports. Do NOT run it on a machine that cannot build .NET WinForms.

------------------------------------------------------------------------------------------------
PROMPT TO PASTE:

You are updating a vendored, modified copy of PKHeX called "PKHaX". The current folder is a full
PKHeX source tree where every one of OUR changes is tagged with the comment `// PKHaX`. Our changes
add three features, in these files (search `// PKHaX` to find every edit):

  - Gen 1 RBY sprite/type desync: PKHeX.Core/PKM/PK1.cs, PKHeX.Core/PKM/Shared/PokeList1.cs,
    PKHeX.WinForms/Controls/PKM Editor/G1Editor.cs, .../EditPK1.cs,
    PKHeX.WinForms/Controls/Slots/PokePreview.cs, .../SummaryPreviewer.cs
  - Gen 3 any-ability (PK3 0x1E): PKHeX.Core/PKM/PK3.cs, PKHeX.Core/PKM/Shared/G3PKM.cs,
    PKHeX.WinForms/Controls/PKM Editor/EditPK3.cs
  - Gen 3 Deoxys forms: PKHeX.Core/PersonalInfo/PersonalTable.cs, .../Table/PersonalTable3.cs,
    .../Info/PersonalInfo3.cs, PKHeX.Core/Legality/Tables/FormInfo.cs,
    PKHeX.Drawing.PokeSprite/Builder/SpriteBuilder.cs, PKHeX.WinForms/MainWindow/Main.cs,
    PKHeX.WinForms/Controls/PKM Editor/PKMEditor.cs

Do this:
1. `git init` (if needed) and commit the current tree as "PKHaX base (our changes)".
2. Add upstream and fetch:  `git remote add upstream https://github.com/kwsch/PKHeX`  then  `git fetch upstream`.
3. Identify the upstream commit our tree is based on (check the assembly/version in
   Directory.Build.props or PKHeX.Core's version; pick the matching upstream tag). Create a branch
   from THAT base, drop in our current files as one commit, so you have "base + our diff".
4. `git merge upstream/master` (or the default branch). Resolve conflicts, KEEPING both upstream's
   changes and our `// PKHaX` blocks. If an upstream API our code calls was renamed/removed, fix our
   call sites minimally (our edits are small and additive: a property, a control, a dropdown).
5. Rebuild as PKHaX:
     `dotnet publish PKHeX.WinForms/PKHeX.WinForms.csproj -c Release -r win-x64 --self-contained false`
   then rename the produced PKHeX.exe to **PKHaX.exe** (the name ending in HaX enables illegal-edit
   mode; do not rename the DLLs).
6. TEST before trusting it:
   - Gen 1: open a Yellow save with a desynced mon (e.g. Mewtwo showing the Gyarados sprite, typed
     Water/Ghost); confirm the Sprite + Type1/Type2 dropdowns show it and that save+reload keeps it.
   - Gen 3: open an Emerald save; confirm the Ability dropdown lists all abilities and writes 0x1E,
     and that a Deoxys shows all 4 forms with the right per-form stats/sprite.
7. Update README.md to note the new upstream base version. Report exactly which files had conflicts
   and what you changed at each call site.

Only commit the result if step 6 passes. If the build fails, report the errors instead of committing.
------------------------------------------------------------------------------------------------

## Why this is run locally and not by the cloud assistant
PKHeX.WinForms is Windows-only and needs the .NET 10 Desktop build; a Linux cloud sandbox cannot
compile or launch it, so any merge it produced would be untested - and an untested re-base of a
fast-moving upstream is how you end up with a tree that does not build. Running it here, where you
can rebuild and open a real save, keeps your working PKHaX safe.
