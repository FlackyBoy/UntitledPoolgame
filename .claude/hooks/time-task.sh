#!/usr/bin/env bash
# Switches the Notion task the working time goes to (notion-todo skill):
# time-task.sh <notion-page-id> starts it, time-task.sh with no argument
# stops tracking. Logged with its time, so the switch can happen mid-turn.
dir="${CLAUDE_PROJECT_DIR:-.}/.claude/time"
mkdir -p "$dir"
task="${1:--}"
printf '%s task %s\n' "$(date +%s)" "$task" >> "$dir/log"
if [ "$task" = "-" ]; then : > "$dir/current-task"; else printf '%s\n' "$task" > "$dir/current-task"; fi
exit 0
