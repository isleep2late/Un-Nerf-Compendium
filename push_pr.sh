#!/usr/bin/env bash
#
# push_pr.sh — terminal-runnable version of the PR pusher (no "press return" pause).
#
# This is a standalone copy of the same logic that lives inside push_pr.command.
# Double-clickers should use push_pr.command (it pauses so you can read output);
# terminal users can run:  bash push_pr.sh
#
# Safe by design: never force-pushes, never touches remote 'main', makes one
# fresh timestamped branch per run and opens a PR into main. Idempotent.
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$REPO_ROOT"

CONF_FILE="$REPO_ROOT/.push_pr.conf"
DEFAULT_REMOTE_URL="https://github.com/isleep2late/Un-Nerf-Compendium.git"  # used automatically if nothing else is set
COMMIT_MSG="PKHaX: Gen 1/2 level-255 support (UI unlock + ResetPartyStats guard); rebuild exe"

step()  { printf '\n\033[1;36m==>\033[0m \033[1m%s\033[0m\n' "$1"; }
info()  { printf '    %s\n' "$1"; }
ok()    { printf '    \033[1;32m✓\033[0m %s\n' "$1"; }
warn()  { printf '    \033[1;33m!\033[0m %s\n' "$1"; }
die()   { printf '\n\033[1;31m✗ %s\033[0m\n' "$1" >&2; exit 1; }

step "Checking prerequisites"
command -v git >/dev/null 2>&1 || die "git is not installed. Install Xcode Command Line Tools:  xcode-select --install"
ok "git found ($(git --version | awk '{print $3}'))"
HAVE_GH=0
if command -v gh >/dev/null 2>&1; then
    HAVE_GH=1; ok "gh CLI found ($(gh --version | head -n1 | awk '{print $3}'))"
else
    warn "gh CLI not found — will print a compare URL instead of opening the PR automatically."
    warn "Install later with:  brew install gh"
fi

step "Ensuring this folder is a git repository"
if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    info "Not a git repo yet — running 'git init'."; git init -b main >/dev/null
    ok "Initialized empty repo (default branch: main)"
else
    ok "Already a git repository"
fi

step "Resolving the 'origin' remote"
REMOTE_URL=""
if git remote get-url origin >/dev/null 2>&1; then
    REMOTE_URL="$(git remote get-url origin)"; ok "origin already set: $REMOTE_URL"
else
    if [[ -f "$CONF_FILE" ]]; then source "$CONF_FILE"; REMOTE_URL="${REMOTE_URL:-}"; fi
    if [[ -z "$REMOTE_URL" && -n "$DEFAULT_REMOTE_URL" ]]; then
        REMOTE_URL="$DEFAULT_REMOTE_URL"; info "Using built-in default remote: $REMOTE_URL"
    fi
    if [[ -z "$REMOTE_URL" ]]; then
        printf '\n    No GitHub remote is configured for this folder.\n'
        printf '    Enter your Un-Nerf Compendium repo URL\n'
        printf '    (e.g. https://github.com/<you>/Un-Nerf-Compendium.git or git@github.com:<you>/Un-Nerf-Compendium.git)\n'
        printf '    > '; read -r REMOTE_URL
        [[ -n "$REMOTE_URL" ]] || die "No remote URL entered. Aborting."
    fi
    git remote add origin "$REMOTE_URL"; ok "Added origin: $REMOTE_URL"
fi
printf 'REMOTE_URL=%q\n' "$REMOTE_URL" > "$CONF_FILE"

step "Verifying .gitignore covers build artifacts"
ensure_ignore() { local p="$1"; grep -qxF "$p" .gitignore 2>/dev/null || { printf '%s\n' "$p" >> .gitignore; info "added to .gitignore: $p"; }; }
touch .gitignore
ensure_ignore "**/bin/"; ensure_ignore "**/obj/"; ensure_ignore ".vs/"; ensure_ignore "*.pdb"
ensure_ignore ".push_pr.conf"; ensure_ignore "PKHeX.exe"; ensure_ignore "PKHaX/PKHeX.exe"
ok ".gitignore is good (bin/, obj/, .vs/, *.pdb, PKHeX.exe excluded)"

step "Fetching origin/main"
if ! git fetch origin main 2>/dev/null; then
    git fetch origin || die "Could not reach origin ($REMOTE_URL). Check the URL and your network/auth."
fi
git rev-parse --verify --quiet origin/main >/dev/null || die "Remote has no 'main' branch yet. Create/push 'main' on GitHub first, then re-run."
ok "Got origin/main"

step "Creating the PR branch"
TS="$(date +%Y%m%d-%H%M)"; BRANCH="pkhax-level255-gen12-${TS}"
git update-ref "refs/heads/${BRANCH}" origin/main
git symbolic-ref HEAD "refs/heads/${BRANCH}"
git reset --mixed >/dev/null
git update-ref refs/heads/main origin/main
ok "Branch '${BRANCH}' created off origin/main"

step "Staging and committing changes"
git add -A
if git diff --cached --quiet; then
    warn "Your working tree is identical to origin/main — nothing to commit."
    warn "Leaving branch '${BRANCH}' empty and stopping."; exit 0
fi
git status --short
git -c user.useConfigOnly=false commit -m "$COMMIT_MSG" >/dev/null \
    || die "Commit failed. Set your identity:
     git config --global user.name  \"Your Name\"
     git config --global user.email \"you@example.com\""
ok "Committed: $COMMIT_MSG"

step "Pushing '${BRANCH}' to origin"
git push -u origin "$BRANCH"; ok "Pushed origin/${BRANCH}"

step "Opening a Pull Request into main"
web_url() {
    local u="$1"; u="${u%.git}"
    if [[ "$u" == git@*:* ]]; then local h="${u#git@}"; h="${h%%:*}"; local p="${u#*:}"; printf 'https://%s/%s' "$h" "$p"
    elif [[ "$u" == ssh://git@* ]]; then u="${u#ssh://git@}"; u="${u/:/\/}"; printf 'https://%s' "$u"
    else printf '%s' "$u"; fi
}
REPO_WEB="$(web_url "$REMOTE_URL")"
COMPARE_URL="${REPO_WEB}/compare/main...${BRANCH}?expand=1"
PR_BODY="Adds Gen 1/2 level-255 support to the PKHaX save editor:
- PKMEditor.UpdateEXPLevel: in HaX mode on a GB mon, allow level up to 255 and stamp Stat_Level directly (EXP pegged at L100).
- PKM.ResetPartyStats: preserve an intentionally over-leveled Gen 1/2 stored level instead of clamping back to the EXP-derived level.

Gated entirely on GBPKM; no other generation is affected. See PKHaX/LEVEL255_CHANGES.md.
The rebuilt PKHeX.exe is shipped via Releases (kept out of git)."
PR_URL=""
if [[ "$HAVE_GH" -eq 1 ]] && gh auth status >/dev/null 2>&1; then
    if PR_URL="$(gh pr create --base main --head "$BRANCH" --title "$COMMIT_MSG" --body "$PR_BODY" 2>/dev/null)"; then
        ok "Pull request created: $PR_URL"
    else warn "gh pr create did not return a URL (a PR may already exist for this branch)."; PR_URL=""; fi
else
    [[ "$HAVE_GH" -eq 1 ]] && warn "gh is installed but not authenticated (run: gh auth login)."
fi
if [[ -z "$PR_URL" ]]; then
    info "Open this URL to create the PR in your browser:"; printf '\n      %s\n' "$COMPARE_URL"; PR_URL="$COMPARE_URL"
fi

step "Done — summary"
cat <<EOF

   Branch pushed : ${BRANCH}
   Pull request  : ${PR_URL}

   This script did NOT merge anything. To pull + merge into main LOCALLY:

       git fetch origin
       git checkout main
       git merge --no-ff ${BRANCH}

   Note: the built PKHeX.exe is not in this commit (gitignored). Attach it to a
   GitHub Release if you want to distribute the binary.

EOF
