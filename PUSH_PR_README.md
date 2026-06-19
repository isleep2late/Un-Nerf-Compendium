# Push to GitHub as a PR — one-click button

Double-click **`push_pr.command`** in Finder. (If macOS blocks it the first time,
right-click → **Open**, or run `chmod +x push_pr.command` once in Terminal.)

`push_pr.command` is **self-contained** — all the logic is inside that one file, so
moving or renaming this folder will not break it. A standalone **`push_pr.sh`** with
the same logic is also included for terminal users (`bash push_pr.sh`); the button
does **not** depend on it.

## What it does

1. **Checks prerequisites** — confirms `git` is installed (and `gh`, optionally).
2. **Makes this a git repo** if it isn't one yet (`git init -b main`), and configures
   the `origin` remote. The **first run asks for your GitHub repo URL**; it's then
   saved in `.push_pr.conf` (gitignored) so you're never asked again.
3. **Fetches `origin/main`** and builds a fresh branch off it named
   `pkhax-level255-gen12-<YYYYMMDD-HHMM>`, carrying your current working-tree changes.
4. **Commits** the changed files (PKHaX source edits + README + `LEVEL255_CHANGES.md`)
   with the message:
   `PKHaX: Gen 1/2 level-255 support (UI unlock + ResetPartyStats guard); rebuild exe`
5. **Pushes** the branch to `origin` (never force, never touches `main`).
6. **Opens a Pull Request** into `main` via `gh pr create`. If `gh` isn't installed or
   authenticated, it prints the GitHub **compare URL** for you to click instead.
7. **Prints** the branch name, the PR URL, and the exact commands to pull + merge
   locally yourself.

## What it does NOT do

- It **never merges** — you merge locally or via the GitHub button.
- It **never force-pushes** and **never modifies the remote `main`**.
- It **does not commit `PKHeX.exe`** (gitignored — 40 MB binary). Ship the build as a
  **GitHub Release** if you want to distribute it.

## Merging the PR locally (printed at the end of every run)

```sh
git fetch origin
git checkout main
git merge --no-ff <branch-name>
```

## Re-running

Safe to run any time. Each run creates a new timestamped branch and a new PR; nothing
is overwritten. If your working tree already matches `origin/main` (nothing to push),
it says so and stops without creating an empty commit.
