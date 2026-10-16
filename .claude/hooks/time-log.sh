#!/usr/bin/env bash
# Logs when Claude starts/stops working (hooks: UserPromptSubmit = start,
# Stop = stop). Only the turn boundaries: which task the time belongs to
# comes from the "task" lines written by time-task.sh, so a task started
# in the middle of a turn gets that turn from the moment it started (the
# task used to be read at the turn's start: a turn where a task began was
# split between two tasks and counted for neither).
# Usage (from hooks): time-log.sh start|stop
cat > /dev/null  # drain the hook payload on stdin; we only need the timestamp
dir="${CLAUDE_PROJECT_DIR:-.}/.claude/time"
mkdir -p "$dir"
printf '%s %s\n' "$(date +%s)" "$1" >> "$dir/log"
exit 0
