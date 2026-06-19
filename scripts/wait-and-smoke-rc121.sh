#!/usr/bin/env bash
# Polls publish workflow run 27770514884 until deploy-staging completes,
# then runs the rc121 smoke test.
set -euo pipefail

RUN_ID=27770514884
API_BASE="https://api.github.com/repos/maik-hasler/einsatzbereit/actions/runs/$RUN_ID/jobs"

echo "=== Polling publish workflow run $RUN_ID for deploy-staging completion ==="

for attempt in $(seq 1 60); do
	echo "Poll $attempt/60..."

	# Fetch job list - need GITHUB_TOKEN if set, else try unauthenticated
	if [ -n "${GITHUB_TOKEN:-}" ]; then
		JOBS_JSON=$(curl -sf -H "Authorization: Bearer $GITHUB_TOKEN" "$API_BASE" 2>/dev/null || echo '{}')
	else
		JOBS_JSON=$(curl -sf "$API_BASE" 2>/dev/null || echo '{}')
	fi

	# Check overall run status
	RUN_STATUS=$(echo "$JOBS_JSON" | python3 -c "
import sys, json
data = json.load(sys.stdin)
jobs = data.get('jobs', [])
statuses = [j['status'] for j in jobs]
conclusions = [j.get('conclusion', 'null') for j in jobs]
# Find deploy-staging
deploy = next((j for j in jobs if j['name'] == 'Deploy to Staging'), None)
if deploy:
	print(f'deploy_status={deploy[\"status\"]}')
	print(f'deploy_conclusion={deploy.get(\"conclusion\", \"null\")}')
else:
	# Check if all jobs are done
	all_done = all(s == 'completed' for s in statuses)
	has_failures = any(c in ('failure', 'cancelled') for c in conclusions)
	print(f'deploy_status=pending')
	print(f'all_done={all_done}')
	print(f'has_failures={has_failures}')
" 2>/dev/null || echo "deploy_status=error")

	echo "Status: $RUN_STATUS"

	# Parse status
	DEPLOY_STATUS=$(echo "$RUN_STATUS" | grep "deploy_status=" | cut -d= -f2)
	DEPLOY_CONCLUSION=$(echo "$RUN_STATUS" | grep "deploy_conclusion=" | cut -d= -f2 || echo "")
	ALL_DONE=$(echo "$RUN_STATUS" | grep "all_done=" | cut -d= -f2 || echo "false")
	HAS_FAILURES=$(echo "$RUN_STATUS" | grep "has_failures=" | cut -d= -f2 || echo "false")

	if [ "$DEPLOY_STATUS" = "completed" ]; then
		if [ "$DEPLOY_CONCLUSION" = "success" ]; then
			echo "=== deploy-staging succeeded! Running smoke test... ==="
			break
		else
			echo "=== deploy-staging finished with conclusion: $DEPLOY_CONCLUSION - aborting smoke test ==="
			exit 1
		fi
	elif [ "$ALL_DONE" = "True" ] && [ "$HAS_FAILURES" = "True" ]; then
		echo "=== Publish workflow completed with failures, no deploy-staging ran ==="
		exit 1
	fi

	sleep 30
done

# Run smoke test
cd /home/user/einsatzbereit
echo ""
echo "=== Running smoke-test-rc121.mjs ==="
node scripts/smoke-test-rc121.mjs
