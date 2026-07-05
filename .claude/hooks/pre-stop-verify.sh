#!/bin/bash
# Safety net for the unattended, no-human-review routine: before Claude ends
# its turn, if backend/frontend source actually changed, build/lint it once
# so a broken build never silently ships to a PR.
#
# Hard-capped at 2 blocks per session (via a counter file keyed by
# session_id) regardless of outcome - this repo has no documented
# loop-prevention field on Stop hook input, so the cap is self-imposed
# rather than relied upon from the framework.

INPUT=$(cat)
cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0

# SessionStart only adds this to ~/.bashrc, which a non-interactive hook
# shell doesn't source - without it, `dotnet` is "not found" and every
# backend change would falsely fail the build check below.
export PATH="$HOME/.dotnet:$PATH"

SESSION_ID=$(printf '%s' "$INPUT" | grep -o '"session_id"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 | sed -E 's/.*:[[:space:]]*"(.*)"$/\1/')
SESSION_ID="${SESSION_ID:-unknown}"
COUNTER_FILE="/tmp/claude-stop-verify-count-${SESSION_ID}"
COUNT=$(cat "$COUNTER_FILE" 2>/dev/null || echo 0)

if [ "$COUNT" -ge 2 ] 2>/dev/null; then
	exit 0
fi

CHANGED=$(git diff --name-only HEAD -- backend/src frontend/src 2>/dev/null)
if [ -z "$CHANGED" ]; then
	exit 0
fi

FAILED=""
LOG="/tmp/claude-stop-verify.log"
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
