PKHaX for Linux
===============

PKHaX is the Un-Nerf Compendium's save editor (PKHeX 26.10.18 with the PKHaX
changes). This build is a small Linux window around the same hack support the
Windows and Android PKHaX use.

Run
---
    tar -xzf PKHaX-linux-x64.tar.gz
    ./PKHaX-linux-x64/PKHaX

It is one self-contained file: no .NET runtime or other package is needed.
You can also give it a file to start with: ./PKHaX <save, ROM or patch>

Add it to the application menu (optional)
-----------------------------------------
    mkdir -p ~/.local/bin ~/.local/share/applications ~/.local/share/icons/hicolor/64x64/apps
    cp PKHaX-linux-x64/PKHaX ~/.local/bin/PKHaX
    cp PKHaX-linux-x64/pkhax.desktop ~/.local/share/applications/pkhax.desktop
    cp PKHaX-linux-x64/icon.png ~/.local/share/icons/hicolor/64x64/apps/pkhax.png

The menu entry runs "PKHaX", so ~/.local/bin must be on your PATH (or edit
the Exec= line of pkhax.desktop to the full path).

ROM hacks
---------
Drop a ROM hack or a patch on the left side of the window (or use Browse).
PKHaX reads the hack's species, moves, items and abilities and its save
format from your own file and keeps only those tables, in
~/.local/share/pkhax/hacks/<SHA-1 of the ROM>/. The ROM is not copied.

  * A ROM: GB, GBC, GBA and DS images are read here. 3DS images are not; in
    isleep2late Files use Export for PKHaX on that game and drop the exported
    profile here.
  * A patch (IPS, UPS, BPS, xdelta): PKHaX asks for the clean game it was
    made for. UPS, BPS and xdelta name that game by CRC32 and refuse a wrong
    one. IPS cannot check its input, so choose the right game yourself.
  * An exported profile (.pkhaxprofile.json or .is2lprofile).

Then open the hack's save (drop it, or "Open a save for this hack...").

Saves
-----
  * Every write first copies the file as it was when PKHaX opened it to
    <save>.<date-time>.bak beside it. A backup is never overwritten or
    deleted by PKHaX. If the backup cannot be written, the save is not
    written either.
  * The save is written to a temporary file, read back, and only then
    renamed over the save. The save keeps its file mode. If the save is a
    symbolic link, the file the link points at is written and the backup
    goes beside that file; the link stays as it is.
  * After a hack's own save format is written, open the save again before
    the next edit; the window says so and disables the save button.
  * Some saves open read-only, and the window says why: a hack's own save
    format whose writes have not been shown to work in the game, a hack with
    species, moves or items the stock editor cannot hold, a save PKHeX
    cannot write back unchanged, or a Gen 3 save until you confirm it is from
    the unmodified game.

What it does not do
-------------------
This is not the full PKHeX window. It edits the party, the boxes (species,
nickname, level, shiny, nature, ability, held item, moves, IVs, EVs and
Showdown text) and money. It has no event flags, mystery gifts, batch
editor, box layout tools or legality panel. For those:

  * isleep2late Files: isleep2late-Files --editor <save>
  * the Windows PKHaX.exe under Wine (needs the Windows .NET 10 Desktop
    Runtime inside the Wine prefix; not tested by the PKHaX authors)

Licence
-------
PKHaX is GPL-3.0 (LICENSE). Other parts and their licences are listed in
THIRD_PARTY_NOTICES.md.
