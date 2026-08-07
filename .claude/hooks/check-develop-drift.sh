#!/usr/bin/env bash
# Avisa si la rama actual quedó atrasada respecto a origin/develop, resaltando
# los commits que tocan UI / localización / escenas (la causa típica de que el
# juego se vea "distinto a dev"). Pensado para correr en SessionStart y a mano:
#   bash .claude/hooks/check-develop-drift.sh
# Nunca falla el arranque: ante cualquier problema (offline, sin git) sale 0.

set +e
BASE="origin/develop"

git rev-parse --is-inside-work-tree >/dev/null 2>&1 || exit 0

CUR=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
[ -z "$CUR" ] && exit 0
[ "$CUR" = "develop" ] && exit 0
[ "$CUR" = "main" ] && exit 0

# Fetch corto y silencioso; si no hay red, seguimos con lo que haya en local.
git fetch origin develop --quiet 2>/dev/null

BEHIND=$(git rev-list --count "HEAD..$BASE" 2>/dev/null)
[ -z "$BEHIND" ] && exit 0

echo "=== dev-drift: rama '$CUR' vs develop ==="
if [ "$BEHIND" -eq 0 ]; then
  echo "  ✅ al día con develop."
  exit 0
fi

echo "  ⚠️  $BEHIND commit(s) de develop NO están en esta rama:"
git log --oneline "HEAD..$BASE" 2>/dev/null | head -12 | sed 's/^/     /'

UIDRIFT=$(git log --oneline "HEAD..$BASE" -- '*UI*' '*Localization*' '*Screens*' '*.unity' 2>/dev/null | head -12)
if [ -n "$UIDRIFT" ]; then
  echo "  --- de esos, tocan UI / localización / escenas (probable causa de UI desactualizada): ---"
  echo "$UIDRIFT" | sed 's/^/     /'
  echo "  >> Sync: 'git merge origin/develop' (puede haber conflictos; coordinar antes de mergear)."
fi
exit 0
