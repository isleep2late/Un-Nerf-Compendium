# PKHaX for Linux

A small Avalonia window for Linux around PKHeX.Core 26.10.18 and PKHaX.Hacks.
It is the third PKHaX head beside the Windows (WinForms) and Android (MAUI)
apps, and uses the same intake and open path as they do (`PKHaX.Hacks.Intake`).

## Where the code lives

- This folder is exported from isleep2late Files
  (`PKHaX/Un-Nerf-Compendium/PKHaX-Linux/` there) by
  `tools/compendium/export-pkhax.sh`, with a sha256 list in
  `PKHaX/PKHAX_SOURCE.txt`. Do not edit it in the Compendium: change it in
  isleep2late Files and export again; `check-pkhax.sh` refuses a changed file.
- `Shared/HackEditorView.cs` is the hack-native editor view. isleep2late
  Files' desktop app compiles the same file.
- `Session/LinuxSaveSession.cs` opens a save through `HackOpen.Open`, applies
  edits and writes. It has no UI, so it can be tested without a display.
- `Views/` holds the window: `MainWindow` (the save on the right),
  `DropPanel` (the ROM hacks area and the stored hacks on the left),
  `StockSaveView` (trainer, money, party and boxes) and `SlotEditorView`.
- The patch decoders are compiled from `../PKHaX/PKHaX.Patching/*.cs` in the
  Compendium, or from `isleep2late-Files/src/Services/Patch` in isleep2late
  Files; `PKHaX.Linux.csproj` picks whichever exists.

## Build

In isleep2late Files: `./build.sh pkhax-linux` (publishes, then runs
`packaging/package.sh` and `tools/compendium/check-release.sh --tarball`).

In the Compendium:

```
dotnet publish PKHaX-Linux/PKHaX.Linux.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o <publish folder>
bash PKHaX-Linux/packaging/package.sh <publish folder> PKHaX-linux-x64.tar.gz
```

`package.sh` takes the publish folder and the output tarball, and puts
`PKHaX`, `pkhax.desktop`, `icon.png`, `LICENSE` (from `PKHaX/LICENSE`, or
`$PKHAX_LICENSE`), `THIRD_PARTY_NOTICES.md` and `README.txt` under
`PKHaX-linux-x64/`.

## Run

`./PKHaX [file]`. Drop a ROM hack, a patch or an exported profile on the
left, then open the hack's save. `packaging/README.txt` is the user's guide
(installing the menu entry, what is read and kept, how saves are written).

## Writes

`LinuxSaveSession.Write` refuses while the open result's `WriteRefusal` is
set, and otherwise: reads the file again and refuses if it changed since it
was opened; copies the bytes read at open to `<save>.<yyyyMMdd-HHmmss>.bak`
(a new name each time, never overwritten, never pruned); writes nothing if
that copy fails; writes the new bytes to a temporary file in the same folder,
reads them back, gives it the save's file mode, and renames it over the save.
A save opened through a symbolic link is resolved to the file it points at
first, so the backup, the temporary file and the rename act on that file. A
hard link is still split by the rename. After a hack-native write, or a write
that does not read back as written, the session refuses further edits and
writes until the save is opened again.

## What it does not do

No event flags, mystery gifts, batch editor, box layout tools or legality
panel; no 3DS images (export a profile from isleep2late Files instead). For
the full editor: `isleep2late-Files --editor <save>`, or the Windows
`PKHaX.exe` under Wine, which is a fallback nobody has tested.
