#!/usr/bin/env bash
# Prints the hours Claude worked on a Notion task: the time inside turns
# (start -> stop) while that task was the current one (time-task.sh).
# A start inside an open turn (a message sent while Claude works) keeps
# the turn going instead of restarting it. An open turn counts up to now.
# Older log lines "<ts> start|stop <task>" are still read (task taken at
# the turn's start).
# Usage: time-total.sh <notion-page-id>
log="${CLAUDE_PROJECT_DIR:-.}/.claude/time/log"
[ -n "$1" ] || { echo "usage: time-total.sh <notion-page-id>" >&2; exit 1; }
[ -f "$log" ] || { echo "0.00"; exit 0; }
awk -v t="$1" -v now="$(date +%s)" '
  function flush(ts) { if (open) { if (cur == t) total += ts - from; from = ts } }
  $2 == "task"  { flush($1); cur = $3; next }
  $2 == "start" { if (!open) { open = 1; from = $1; if (NF >= 3) cur = $3 } next }
  $2 == "stop"  { if (open) { flush($1); open = 0 } next }
  END { flush(now); printf "%.2f\n", total / 3600 }
' "$log"
