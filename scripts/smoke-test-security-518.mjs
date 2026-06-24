/**
 * Smoke test: verify security fixes from issue #518.
 * 1. Public GET /v1/volunteer-opportunities does NOT expose checkInPin in any item.
 * 2. GET /v1/volunteer-opportunities/{id}/check-in-pin returns 401 for unauthenticated requests.
 * Run: node scripts/smoke-test-security-518.mjs
 */

const API_BASE = 'https://api.maik-hasler.de';

let passed = 0;
let failed = 0;

function assert(condition, label) {
	if (condition) {
		console.log(`  PASS  ${label}`);
		passed++;
	} else {
		console.error(`  FAIL  ${label}`);
		failed++;
	}
}

async function main() {
	console.log('=== Security Fix #518 Smoke Test ===');

	// 1. Public list endpoint should not expose checkInPin
	console.log('\n[1] GET /v1/volunteer-opportunities - checkInPin not in response');
	const listRes = await fetch(`${API_BASE}/v1/volunteer-opportunities?pageNumber=1&pageSize=20`);
	assert(listRes.ok, `List endpoint returns 2xx (got ${listRes.status})`);

	const listBody = await listRes.json();
	const items = listBody.items ?? listBody.data ?? listBody ?? [];
	const itemsArr = Array.isArray(items) ? items : [];

	const anyHasPin = itemsArr.some(
		(item) => 'checkInPin' in item || item.checkInPin !== undefined,
	);
	assert(!anyHasPin, 'No item in list response contains checkInPin');

	// 2. Get details of first opportunity (if any) and verify no checkInPin
	let opportunityId = itemsArr[0]?.id;

	if (opportunityId) {
		console.log(`\n[2] GET /v1/volunteer-opportunities/${opportunityId} - checkInPin not in details`);
		const detailRes = await fetch(`${API_BASE}/v1/volunteer-opportunities/${opportunityId}`);
		assert(detailRes.ok, `Details endpoint returns 2xx (got ${detailRes.status})`);
		const detailBody = await detailRes.json();
		assert(
			!('checkInPin' in detailBody) && detailBody.checkInPin === undefined,
			'Opportunity details do not contain checkInPin',
		);
	} else {
		// Use a well-formed but non-existent ID if list is empty
		opportunityId = '00000000-0000-0000-0000-000000000001';
		console.log('\n[2] List was empty - using placeholder ID for pin endpoint test');
	}

	// 3. The organizer-only check-in-pin endpoint returns 401 without auth
	console.log(`\n[3] GET /v1/volunteer-opportunities/${opportunityId}/check-in-pin - 401 without auth`);
	const pinRes = await fetch(`${API_BASE}/v1/volunteer-opportunities/${opportunityId}/check-in-pin`);
	assert(
		pinRes.status === 401,
		`check-in-pin endpoint returns 401 for unauthenticated request (got ${pinRes.status})`,
	);

	// 4. POST check-in-with-pin for a random engagement also requires auth
	const fakeEngagementId = '00000000-0000-0000-0000-000000000002';
	console.log(`\n[4] POST /v1/me/engagements/${fakeEngagementId}/check-in - 401 without auth`);
	const checkInRes = await fetch(`${API_BASE}/v1/me/engagements/${fakeEngagementId}/check-in`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify({ pin: '1234' }),
	});
	assert(
		checkInRes.status === 401,
		`check-in endpoint returns 401 for unauthenticated request (got ${checkInRes.status})`,
	);

	console.log('\n=====================================');
	console.log(`Results: ${passed} passed, ${failed} failed`);

	if (failed > 0) {
		process.exit(1);
	}
}

main().catch((err) => {
	console.error('Fatal error:', err);
	process.exit(1);
});
