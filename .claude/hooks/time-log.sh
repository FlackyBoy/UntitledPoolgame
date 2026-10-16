#!/usr/bin/env bash
# Logs when Claude starts/stops working, tagged with the active Notion task, so the
# notion-todo skill can report real working time instead of a guess.
# Usage (from hooks): time-log.sh start|stop
cat > /dev/null  # drain the hook payload on stdin; we only need the timestamp
dir="${CLAUDE_PROJECT_DIR:-.}/.claude/time"
task_file="$dir/current-task"
[ -f "$task_file" ] || exit 0
task=$(head -n1 "$task_file" | tr -d '\r')
[ -n "$task" ] || exit 0
printf '%s %s %s\n' "$(date +%s)" "$1" "$task" >> "$dir/log"
exit 0
