// Smoke test for #542 follow-up (PR #569): a Waitlist opportunity could still
// be published with zero time slots via the direct-create-as-Published API
// path, bypassing the Publish()-time guard entirely. Verifies:
//   1. Direct API create with isDraft:false + ParticipationType:Waitlist is
//      now rejected (400), instead of silently creating a dead-end listing.
//   2. The supported flow (create as draft -> add a time slot -> publish)
//      still works end-to-end and the opportunity becomes visible/Published.
// Run: node scripts/smoke-test-542-waitlist-publish-gap.mjs

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const KEYCLOAK = "https://login.maik-hasler.de";
const CLIENT_ID = "frontend";
const REALM = "einsatzbereit";

async function getToken(username, password) {
	const res = await fetch(
		`${KEYCLOAK}/realms/${REALM}/protocol/openid-connect/token`,
		{
			method: "POST",
			headers: { "Content-Type": "application/x-www-form-urlencoded" },
			body: new URLSearchParams({
				grant_type: "password",
				client_id: CLIENT_ID,
				username,
				password,
				scope: "openid",
			}),
		},
	);
	if (!res.ok) throw new Error(`Token request failed: ${res.status}`);
	const data = await res.json();
	if (!data.access_token) throw new Error("No access_token in response");
	return data.access_token;
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const token = await getToken("olaf", "olaf123");
	console.log("OK  Got access token for olaf (organisator)");
	const authHeaders = {
		Authorization: `Bearer ${token}`,
		"Content-Type": "application/json",
	};

	// Find (or reuse) an organization olaf belongs to.
	const orgsRes = await fetch(`${API}/v1/organizations`, {
		headers: authHeaders,
	});
	if (!orgsRes.ok) throw new Error(`GET /organizations failed: ${orgsRes.status}`);
	const orgs = await orgsRes.json();
	if (!Array.isArray(orgs) || orgs.length === 0)
		throw new Error("olaf has no organizations - cannot run this smoke test");
	const orgId = orgs[0].id;
	console.log(`OK  Using organization ${orgId}`);

	const basePayload = {
		title: `Smoke Test 542 ${Date.now()}`,
		description: "Automated smoke test for the Waitlist publish-gap fix.",
		organizationId: orgId,
		isRemote: true,
		occurrence: "OneTime",
		participationType: "Waitlist",
		checkInMethod: "None",
	};

	// --- 1. Direct create as Published with no slots must now be rejected ---
	const rejectedRes = await fetch(`${API}/v1/volunteer-opportunities`, {
		method: "POST",
		headers: authHeaders,
		body: JSON.stringify({ ...basePayload, isDraft: false }),
	});
	if (rejectedRes.status !== 400) {
		const body = await rejectedRes.text();
		throw new Error(
			`Expected 400 when creating a Published Waitlist opportunity with no slots, got ${rejectedRes.status}: ${body}`,
		);
	}
	console.log(
		"OK  Direct create-as-Published Waitlist-with-no-slots rejected (400)",
	);

	// --- 2. Supported flow: draft -> add slot -> publish ---
	const draftRes = await fetch(`${API}/v1/volunteer-opportunities`, {
		method: "POST",
		headers: authHeaders,
		body: JSON.stringify({ ...basePayload, isDraft: true }),
	});
	if (!draftRes.ok)
		throw new Error(`Draft create failed: ${draftRes.status} ${await draftRes.text()}`);
	const draft = await draftRes.json();
	if (draft.status !== "Draft")
		throw new Error(`Expected Draft status, got ${draft.status}`);
	console.log(`OK  Created draft opportunity ${draft.id}`);

	const start = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000);
	const end = new Date(start.getTime() + 2 * 60 * 60 * 1000);
	const slotRes = await fetch(
		`${API}/v1/volunteer-opportunities/${draft.id}/time-slots`,
		{
			method: "POST",
			headers: authHeaders,
			body: JSON.stringify({
				startDateTime: start.toISOString(),
				endDateTime: end.toISOString(),
				maxParticipants: 5,
				recurrenceCount: 1,
			}),
		},
	);
	if (!slotRes.ok)
		throw new Error(`Time slot create failed: ${slotRes.status} ${await slotRes.text()}`);
	console.log("OK  Added a time slot to the draft");

	const publishRes = await fetch(
		`${API}/v1/volunteer-opportunities/${draft.id}/publish`,
		{ method: "POST", headers: authHeaders },
	);
	if (!publishRes.ok)
		throw new Error(`Publish failed: ${publishRes.status} ${await publishRes.text()}`);
	console.log("OK  Published the draft after adding a time slot");

	// Verify it's now visible/Published via the public listing.
	const listRes = await fetch(
		`${API}/v1/volunteer-opportunities?pageNumber=1&pageSize=50`,
	);
	if (!listRes.ok) throw new Error(`GET /volunteer-opportunities failed: ${listRes.status}`);
	const list = await listRes.json();
	const found = (list.items ?? []).find((i) => i.id === draft.id);
	if (!found)
		throw new Error("Published opportunity not visible in public listing");
	console.log("OK  Published opportunity visible in public listing");

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
