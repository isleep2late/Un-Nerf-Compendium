# PKHaX Mobile

A cross-platform (iOS + Android) save editor for the Un-Nerf Compendium, built on **.NET MAUI** and the
fork's own **PKHeX.Core**. It is the mobile counterpart to the desktop `PKHaX.exe`.

## Why this architecture

The whole point of "PKHaX" (the `HaX` spelling) is **illegal-edit mode**, and every hackmons feature in
this compendium — Gen 3 any-ability, Gen 1 sprite/typing desync, Gen 3 Deoxys forms, the Gen 1/2 "No Move"
glitch move — is implemented **inside `PKHeX.Core`**. This app references that project directly:

```
PKHaX-Mobile/src/PKHaX.Mobile.csproj  ->  ../../PKHaX/PKHeX.Core/PKHeX.Core.csproj
```

so it inherits **all** of those features automatically, and **every upstream PKHeX sync** flows in at build
time with zero extra work — when the compendium's `PKHeX.Core` is updated (the recorded-base diff-apply in
`../PKHaX/PKHaX_README.md`), the next mobile build picks it up. There is no second copy of the save logic to
keep in sync, and nothing to re-implement in JavaScript.

`PKHeX.Drawing.*` (the desktop sprite pipeline) uses `System.Drawing` and is **not** mobile-safe, so it is
deliberately **not** referenced. Sprites are fetched over the network instead (`Services/Sprites.cs`).
Fork-only battle-sim species (Goku, etc.) never exist inside a real cartridge save, so the standard sprite
set covers everything a save file can hold.

## Building it (no Mac needed)

- **Android** builds locally on Linux (proven): `bash PKHaX-Mobile/build-android-local.sh`.
- **iOS** builds on a GitHub Actions macOS cloud runner via `.github/workflows/build-mobile.yml`; the one-time
  Mac-free signing setup is in `docs/ios-cloud-build.md`.

See `build-and-sideload.md` for the full flow.

## Updates instead of OTA

MAUI apps cannot do Expo/EAS-style push-OTA — iOS forbids downloading native code, and PKHeX.Core is native.
For this app "update" means: after a PKHeX.Core sync, **rebuild** (Android locally, iOS by re-running the
workflow). Because the app is a thin shell over PKHeX.Core, a rebuild is the update — it always tracks whatever
the fork's core is at.

## What it does today

- Open a save file **in place** from anywhere the OS file picker can reach (an emulator's save folder, the
  Files app, Documents) — Gen 1-9 main-series, via `SaveUtil.GetSaveFile`.
- Trainer summary (game, OT, IDs, box/party counts).
- Touch-friendly **box browser**: 3-wide grid, swipe left/right (or the arrow buttons) to change boxes, tap a
  Pokemon to edit.
- **Entity editor**: species, level, ability (full list — Gen 3 any-ability writes the fork's override byte
  exactly like desktop), shiny, and the four moves. Illegal edits are allowed; a legality read-out is shown
  but never blocks the write.
- **Save back to file**: serialises through `PKHeX.Core` (`SaveFile.Write()`) and overwrites the original
  file in place (Android SAF `rwt`; iOS security-scoped bookmark).
- **Gen 1 save corrupter (Any% 2-swap)**: a "Corrupt save (Gen 1 Any% 2-swap)" button appears for a
  Red/Blue/Yellow raw 32 KiB save file (never for one opened out of a save state). `SaveManager` keeps a
  private copy of the bytes exactly as the file was opened (or as last saved back) — `SaveFile` aliases the
  array it is given, so the copy is taken before PKHeX.Core sees the array and edits can never leak into it.
  The button first writes that copy to `<AppDataDirectory>/backups/<name>.bak-yyyyMMdd-HHmmss` and offers
  the backup through the system share sheet (put it in Files / Drive; if you dismiss the sheet the app-local
  copy still exists and its path is shown in the status line). Only then does it patch those same bytes —
  party count 255 + species-list terminator `0xFF` + recomputed checksum, every other byte (Trainer ID
  included) untouched — and write them back in place: byte-identical to the desktop build and to the
  standalone `corrupt-save-gen1.py` run on the same file. **Unsaved edits in the app are NOT included**; the
  confirmation says so — tap "Save changes to file" first if you want them. That warning is driven by the
  editor's own edit flag (`SaveFile.State.Edited`, set by every mobile edit path and cleared on load / save back),
  not by comparing `Write()` against the file: `SAV1.Write()` re-packs even an unedited save into different
  bytes, so a byte compare would warn about edits that were never made. PKHeX cannot reload a
  party-count-255 file, so the app keeps the pre-corruption save loaded; reopen the backup for further edits.

The illegal-edit toggle on the main screen is **on by default** — that is the PKHaX behaviour.

## Project layout

```
PKHaX-Mobile/
  PKHaX.Mobile.sln
  src/
    PKHaX.Mobile.csproj        references ../../PKHaX/PKHeX.Core
    MauiProgram.cs, App.*, AppShell.*
    Services/
      SaveManager.cs           owns the loaded SaveFile + all PKHeX.Core calls
      ISaveFileGateway.cs       platform file read/write abstraction + factory
      Sprites.cs                remote sprite URLs (no System.Drawing)
    ViewModels/SlotItem.cs
    Views/
      MainPage.*               open save, trainer summary, illegal toggle, save
      BoxPage.*                swipeable 3-wide box grid
      EntityEditorPage.*       per-Pokemon editor (fork features surfaced here)
    Platforms/
      Android/                 SAF gateway, MainActivity/Application, manifest
      iOS/                     bookmark gateway, AppDelegate, Info.plist, entitlements
    Resources/Styles, AppIcon, Splash
  README.md, build-and-sideload.md
```

## Extending it

The editor surfaces the headline fields; the full PKHeX.Core object model is one `pk.` away. To add, say, the
Gen 1 sprite/typing desync UI, bind new controls in `EntityEditorPage` to the `PK1` members the fork added
(`HeaderSpeciesInternal`, the type bytes) — the data layer already round-trips them because it is the same
`PKHeX.Core` the desktop app uses. No server round-trip, no data duplication.
