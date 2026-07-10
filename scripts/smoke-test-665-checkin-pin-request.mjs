// Smoke test for #665:
//
// EngagementManagementPage ("Manage applications") unconditionally called
// GET /v1/volunteer-opportunities/{id}/check-in-pin on every page load,
// regardless of the opportunity's checkInMethod. The backend returns 404
// whenever the PIN is null, i.e. for every checkInMethod other than
// "PINCode" - so 3 of the 4 possible values guaranteed a wasted, silently
// swallowed 404 request on every view.
//
// Fix: the PIN fetch now lives in its own effect gated on
// opportunity?.checkInMethod === "PINCode", the same condition already
// used to render the PIN block.
//
// This script checks both sides: no check-in-pin request for a "None"
// opportunity, and the request still succeeds (200) for a "PINCode" one.
//
// Run: node scripts/smoke-test-665-checkin-pin-request.mjs

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

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

async function createOpportunity(token, orgId, title, checkInMethod) {
	const res = await fetch(`${API}/v1/volunteer-opportunities`, {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify({
			title,
			description: "Automated smoke test opportunity for #665.",
			organizationId: orgId,
			isRemote: true,
			occurrence: "OneTime",
			participationType: "IndividualContact",
			checkInMethod,
			isDraft: false,
		}),
	});
	if (!res.ok)
		throw new Error(
			`Create opportunity failed: ${res.status} ${await res.text()}`,
		);
	return res.json();
}

async function deleteOpportunity(token, opportunityId) {
	const res = await fetch(
		`${API}/v1/volunteer-opportunities/${opportunityId}`,
		{ method: "DELETE", headers: { Authorization: `Bearer ${token}` } },
	);
	if (!res.ok)
		throw new Error(`Delete failed: ${res.status} ${await res.text()}`);
}

async function loginAsUser(page, username, password) {
	await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	if ((await signInBtn.count()) > 0) {
		await signInBtn.first().click();
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await loginKeycloak(page, username, password);
	}
	await page.waitForSelector("main", { timeout: 10000 });
}

async function visitManageApplications(opportunityId) {
	const { browser, page } = await launchLiveBrowser();
	const pinRequests = [];
	page.on("response", (r) => {
		if (r.url().includes("/check-in-pin")) {
			pinRequests.push({ url: r.url(), status: r.status() });
		}
	});
	try {
		await loginAsUser(page, "olaf", "olaf123");
		await page.goto(
			`${BASE}/volunteer-opportunities/${opportunityId}/engagements`,
			{ waitUntil: "networkidle" },
		);
		await page.waitForSelector("main", { timeout: 10000 });
		// give any stray requests a moment to land after networkidle
		await page.waitForTimeout(1000);
	} finally {
		await browser.close();
	}
	return pinRequests;
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok)
		throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const olafToken = await getToken("olaf", "olaf123");
	const orgsRes = await fetch(`${API}/v1/organizations`, {
		headers: { Authorization: `Bearer ${olafToken}` },
	});
	if (!orgsRes.ok)
		throw new Error(`GET /organizations failed: ${orgsRes.status}`);
	const orgs = await orgsRes.json();
	if (!Array.isArray(orgs) || orgs.length === 0)
		throw new Error("olaf has no organizations - cannot run this smoke test");
	const orgId = orgs[0].id;

	let noneOpportunity;
	let pinOpportunity;
	try {
		// === Case 1: checkInMethod "None" - no check-in-pin request expected ===
		noneOpportunity = await createOpportunity(
			olafToken,
			orgId,
			`Smoke665 NoneCheckIn ${Date.now()}`,
			"None",
		);
		console.log(
			`OK  Created "None" check-in opportunity ${noneOpportunity.id}`,
		);

		const noneRequests = await visitManageApplications(noneOpportunity.id);
		if (noneRequests.length > 0) {
			throw new Error(
				`Expected no check-in-pin request for a "None" opportunity, got: ${JSON.stringify(noneRequests)}`,
			);
		}
		console.log(
			'OK  No GET .../check-in-pin request fired for a "None" check-in opportunity',
		);

		// === Case 2: checkInMethod "PINCode" - request still fires and succeeds ===
		pinOpportunity = await createOpportunity(
			olafToken,
			orgId,
			`Smoke665 PINCodeCheckIn ${Date.now()}`,
			"PINCode",
		);
		console.log(`OK  Created "PINCode" check-in opportunity ${pinOpportunity.id}`);

		const pinRequests = await visitManageApplications(pinOpportunity.id);
		if (pinRequests.length !== 1) {
			throw new Error(
				`Expected exactly one check-in-pin request for a "PINCode" opportunity, got: ${JSON.stringify(pinRequests)}`,
			);
		}
		if (pinRequests[0].status !== 200) {
			throw new Error(
				`Expected check-in-pin request to return 200, got ${pinRequests[0].status}`,
			);
		}
		console.log(
			'OK  GET .../check-in-pin request fired and returned 200 for a "PINCode" check-in opportunity',
		);
	} finally {
		if (noneOpportunity) {
			await deleteOpportunity(olafToken, noneOpportunity.id);
			console.log(`OK  Cleaned up opportunity ${noneOpportunity.id}`);
		}
		if (pinOpportunity) {
			await deleteOpportunity(olafToken, pinOpportunity.id);
			console.log(`OK  Cleaned up opportunity ${pinOpportunity.id}`);
		}
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
