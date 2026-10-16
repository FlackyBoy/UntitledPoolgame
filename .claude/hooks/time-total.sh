#!/usr/bin/env bash
# Prints the hours Claude worked on a Notion task (sum of start->stop intervals).
# An interval still open (the current turn) counts up to now.
# Usage: time-total.sh <notion-page-id>
log="${CLAUDE_PROJECT_DIR:-.}/.claude/time/log"
[ -n "$1" ] || { echo "usage: time-total.sh <notion-page-id>" >&2; exit 1; }
[ -f "$log" ] || { echo "0.00"; exit 0; }
awk -v t="$1" -v now="$(date +%s)" '
  $3 == t && $2 == "start" { s = $1 }
  $3 == t && $2 == "stop" && s { total += $1 - s; s = 0 }
  END { if (s) total += now - s; printf "%.2f\n", total / 3600 }
' "$log"
