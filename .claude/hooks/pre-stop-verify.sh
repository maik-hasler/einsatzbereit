#!/bin/bash

INPUT=$(cat)
cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0

export PATH="$HOME/.dotnet:$PATH"

SESSION_ID=$(printf '%s' "$INPUT" | grep -o '"session_id"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 | sed -E 's/.*:[[:space:]]*"(.*)"$/\1/')
SESSION_ID="${SESSION_ID:-unknown}"
COUNTER_FILE="/tmp/claude-stop-verify-count-${SESSION_ID}"
COUNT=$(cat "$COUNTER_FILE" 2>/dev/null || echo 0)

if [ "$COUNT" -ge 2 ] 2>/dev/null; then
	exit 0
fi

# Diff against the branch point, not HEAD: AGENTS.md mandates committing to a
# claude/... branch, so a HEAD-only diff goes empty the moment work is committed
# - exactly the flow this hook exists to guard. Untracked files are invisible to
# git diff entirely, so they are unioned in separately.
BASE=$(git merge-base HEAD origin/main 2>/dev/null || echo HEAD)
CHANGED=$(
	{
		git diff --name-only "$BASE" -- backend/src frontend/src
		git ls-files --others --exclude-standard -- backend/src frontend/src
	} 2>/dev/null | sort -u
)
if [ -z "$CHANGED" ]; then
	exit 0
fi

FAILED=""
LOG="/tmp/claude-stop-verify-${SESSION_ID}.log"
: > "$LOG"

if printf '%s\n' "$CHANGED" | grep -q '^backend/src'; then
	if ! dotnet build backend/src/Api/Api.csproj --configuration Debug --verbosity quiet >>"$LOG" 2>&1; then
		FAILED="backend build"
	fi
fi

if [ -z "$FAILED" ] && printf '%s\n' "$CHANGED" | grep -q '^frontend/src'; then
	if ! (cd frontend && pnpm lint >>"$LOG" 2>&1 && pnpm check >>"$LOG" 2>&1); then
		FAILED="frontend lint/typecheck"
	fi
fi

if [ -n "$FAILED" ]; then
	echo "$((COUNT + 1))" > "$COUNTER_FILE"
	REASON="$FAILED failed - see $LOG for details. Fix it before finishing. (checked at most twice per session)"
	python3 -c "import json,sys; print(json.dumps({'decision': 'block', 'reason': sys.argv[1]}))" "$REASON"
	exit 0
fi

exit 0
