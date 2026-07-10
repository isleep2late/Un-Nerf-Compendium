#!/usr/bin/env bash
#
# push_pr.command — double-clickable, SELF-CONTAINED launcher for macOS Finder.
#
# Pushes the Un-Nerf Compendium (PKHaX) working tree to GitHub as a PR branch.
# All logic lives in this one file, so moving/renaming the folder can't break it.
#
# Safe by design:
#   * Never force-pushes.
#   * Never pushes / rewrites the remote `main` branch.
#   * Creates one fresh, timestamped branch per run and opens a PR into main.
#   * Idempotent: re-running just makes another branch; nothing is clobbered.
#
# The diff is computed as (origin/main -> your current working tree), so the PR
# contains exactly your local changes regardless of whether this folder was a git
# repo before. The built PKHeX.exe is intentionally NOT committed (gitignored).
#
clear
echo "=============================================================="
echo "  Un-Nerf Compendium (PKHaX) — push changes as a GitHub PR"
echo "=============================================================="

run() {
set -euo pipefail

# --- locate repo root (this script lives at the repo root) ---------------------
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$REPO_ROOT"

CONF_FILE="$REPO_ROOT/.push_pr.conf"   # remembers the remote URL across runs (gitignored)
DEFAULT_REMOTE_URL="https://github.com/isleep2late/Un-Nerf-Compendium.git"  # used automatically if nothing else is set
COMMIT_MSG=""  # resolved at branch time: unpushed local main commit message, else generic

# --- pretty step printing -----------------------------------------------------
step()  { printf '\n\033[1;36m==>\033[0m \033[1m%s\033[0m\n' "$1"; }
info()  { printf '    %s\n' "$1"; }
ok()    { printf '    \033[1;32m✓\033[0m %s\n' "$1"; }
warn()  { printf '    \033[1;33m!\033[0m %s\n' "$1"; }
die()   { printf '\n\033[1;31m✗ %s\033[0m\n' "$1" >&2; exit 1; }

# --- 1. preflight: required tooling ------------------------------------------
step "Checking prerequisites"
command -v git >/dev/null 2>&1 || die "git is not installed. Install Xcode Command Line Tools:  xcode-select --install"
ok "git found ($(git --version | awk '{print $3}'))"

HAVE_GH=0
if command -v gh >/dev/null 2>&1; then
    HAVE_GH=1
    ok "gh CLI found ($(gh --version | head -n1 | awk '{print $3}'))"
else
    warn "gh CLI not found — will print a compare URL instead of opening the PR automatically."
    warn "Install later with:  brew install gh"
fi

# --- 2. ensure this is a git repo --------------------------------------------
step "Ensuring this folder is a git repository"
if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    info "Not a git repo yet — running 'git init'."
    git init -b main >/dev/null
    ok "Initialized empty repo (default branch: main)"
else
    ok "Already a git repository"
fi

# --- 3. ensure 'origin' remote is configured ---------------------------------
step "Resolving the 'origin' remote"
REMOTE_URL=""
if git remote get-url origin >/dev/null 2>&1; then
    REMOTE_URL="$(git remote get-url origin)"
    ok "origin already set: $REMOTE_URL"
else
    if [[ -f "$CONF_FILE" ]]; then
        # shellcheck disable=SC1090
        source "$CONF_FILE"
        REMOTE_URL="${REMOTE_URL:-}"
    fi
    if [[ -z "$REMOTE_URL" && -n "$DEFAULT_REMOTE_URL" ]]; then
        REMOTE_URL="$DEFAULT_REMOTE_URL"
        info "Using built-in default remote: $REMOTE_URL"
    fi
    if [[ -z "$REMOTE_URL" ]]; then
        printf '\n    No GitHub remote is configured for this folder.\n'
        printf '    Enter your Un-Nerf Compendium repo URL\n'
        printf '    (e.g. https://github.com/<you>/Un-Nerf-Compendium.git or git@github.com:<you>/Un-Nerf-Compendium.git)\n'
        printf '    > '
        read -r REMOTE_URL
        [[ -n "$REMOTE_URL" ]] || die "No remote URL entered. Aborting."
    fi
    git remote add origin "$REMOTE_URL"
    ok "Added origin: $REMOTE_URL"
fi
printf 'REMOTE_URL=%q\n' "$REMOTE_URL" > "$CONF_FILE"

# --- 4. make sure build junk is ignored --------------------------------------
step "Verifying .gitignore covers build artifacts"
ensure_ignore() {
    local pat="$1"
    if ! grep -qxF "$pat" .gitignore 2>/dev/null; then
        printf '%s\n' "$pat" >> .gitignore
        info "added to .gitignore: $pat"
    fi
}
touch .gitignore
ensure_ignore "**/bin/"
ensure_ignore "**/obj/"
ensure_ignore ".vs/"
ensure_ignore "*.pdb"
ensure_ignore ".push_pr.conf"
ensure_ignore "PKHeX.exe"
ensure_ignore "PKHaX/PKHeX.exe"
ok ".gitignore is good (bin/, obj/, .vs/, *.pdb, PKHeX.exe excluded)"

# --- 5. fetch origin/main (the PR base) --------------------------------------
step "Fetching origin/main"
if ! git fetch origin main 2>/dev/null; then
    git fetch origin || die "Could not reach origin ($REMOTE_URL). Check the URL and your network/auth."
fi
if ! git rev-parse --verify --quiet origin/main >/dev/null; then
    die "Remote has no 'main' branch yet. Create/push 'main' on GitHub first, then re-run.
     (This script refuses to create main for you so it can never touch it directly.)"
fi
ok "Got origin/main"

# --- 6. build the PR branch from origin/main, carrying your working changes ---
step "Creating the PR branch"
TS="$(date +%Y%m%d-%H%M)"
BRANCH="compendium-update-${TS}"
if [[ -z "$COMMIT_MSG" ]] && git rev-parse --verify --quiet main >/dev/null && [[ -n "$(git rev-list origin/main..main 2>/dev/null | head -n1)" ]]; then
    COMMIT_MSG="$(git log -1 --format=%s main)"
    info "Using unpushed local main commit message: $COMMIT_MSG"
fi
[[ -n "$COMMIT_MSG" ]] || COMMIT_MSG="Compendium update ${TS}"

git update-ref "refs/heads/${BRANCH}" origin/main
git symbolic-ref HEAD "refs/heads/${BRANCH}"
git reset --mixed >/dev/null    # index := origin/main; working tree left untouched
git update-ref refs/heads/main origin/main
ok "Branch '${BRANCH}' created off origin/main"

# --- 7. stage + commit your changes ------------------------------------------
step "Staging and committing changes"
git add -A
if git diff --cached --quiet; then
    warn "Your working tree is identical to origin/main — nothing to commit."
    warn "Did you already merge these changes? Leaving branch '${BRANCH}' empty and stopping."
    exit 0
fi
git status --short
git -c user.useConfigOnly=false commit -m "$COMMIT_MSG" >/dev/null \
    || die "Commit failed. If git complains about identity, run:
     git config --global user.name  \"Your Name\"
     git config --global user.email \"you@example.com\"
     then re-run this script."
ok "Committed: $COMMIT_MSG"

# --- 8. push the branch (never force) ----------------------------------------
step "Pushing '${BRANCH}' to origin"
git push -u origin "$BRANCH"
ok "Pushed origin/${BRANCH}"

# --- 9. open the PR (gh) or print a compare URL ------------------------------
step "Opening a Pull Request into main"
web_url() {
    local u="$1"
    u="${u%.git}"
    if [[ "$u" == git@*:* ]]; then
        local host="${u#git@}"; host="${host%%:*}"
        local path="${u#*:}"
        printf 'https://%s/%s' "$host" "$path"
    elif [[ "$u" == ssh://git@* ]]; then
        u="${u#ssh://git@}"; u="${u/:/\/}"
        printf 'https://%s' "$u"
    else
        printf '%s' "$u"
    fi
}
REPO_WEB="$(web_url "$REMOTE_URL")"
COMPARE_URL="${REPO_WEB}/compare/main...${BRANCH}?expand=1"
PR_BODY="${COMMIT_MSG}

$(git diff --stat origin/main..HEAD | tail -n 1)
$(git diff --name-only origin/main..HEAD | head -n 20)"

PR_URL=""
if [[ "$HAVE_GH" -eq 1 ]] && gh auth status >/dev/null 2>&1; then
    if PR_URL="$(gh pr create --base main --head "$BRANCH" \
            --title "$COMMIT_MSG" --body "$PR_BODY" 2>/dev/null)"; then
        ok "Pull request created: $PR_URL"
    else
        warn "gh pr create did not return a URL (a PR may already exist for this branch)."
        PR_URL=""
    fi
else
    [[ "$HAVE_GH" -eq 1 ]] && warn "gh is installed but not authenticated (run: gh auth login)."
fi
if [[ -z "$PR_URL" ]]; then
    info "Open this URL to create the PR in your browser:"
    printf '\n      %s\n' "$COMPARE_URL"
    PR_URL="$COMPARE_URL"
fi

# --- 10. summary + local pull/merge instructions -----------------------------
step "Done — summary"
cat <<EOF

   Branch pushed : ${BRANCH}
   Pull request  : ${PR_URL}

   This script did NOT merge anything. To pull the branch and merge into main
   LOCALLY yourself, run:

       git fetch origin
       git checkout main
       git merge --no-ff ${BRANCH}

   (Or just click "Merge pull request" on GitHub, then 'git pull' on main.)

   Note: the built PKHeX.exe is not in this commit (gitignored). Attach it to a
   GitHub Release if you want to distribute the binary.

EOF
}

# --- run in a subshell, then pause so the window always stays open ------------
# (subshell so an internal `exit` doesn't skip the pause below)
set +e
( run )
STATUS=$?
set -e

echo
if [[ $STATUS -ne 0 ]]; then
    echo ">>> Finished with errors (exit $STATUS). See messages above."
else
    echo ">>> Finished successfully."
fi
echo
echo "Press Return to close this window."
read -r _
