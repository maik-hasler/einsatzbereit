// Smoke test for #667:
//
// GetByVolunteerAsync (backing GET /v1/me/engagements, which powers "My
// Profile -> Engagements") used an inner join against
// VolunteerOpportunitiesQuery. Deleting an opportunity hard-deletes that row
// while only cancelling (not deleting) affected Engagement rows, so the
// inner join silently dropped the volunteer's own engagement from the list
// entirely once its opportunity was gone - no "Cancelled" entry, no
// placeholder, nothing.
//
// Fix: EngagementReadRepository.GetByVolunteerAsync now looks up
// opportunities/organizations separately and merges them in, falling back to
// null (rendered as "This opportunity has been removed" / "Dieser Bedarf
// wurde entfernt" in the UI) when the opportunity no longer exists, instead
// of dropping the row.
//
// Run: node scripts/smoke-test-667-engagement-history-deleted-opportunity.mjs

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

async function createOpportunity(token, orgId, title) {
	const res = await fetch(`${API}/v1/volunteer-opportunities`, {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify({
			title,
			description: "Automated smoke test opportunity for #667.",
			organizationId: orgId,
			isRemote: true,
			occurrence: "OneTime",
			participationType: "IndividualContact",
			checkInMethod: "None",
			isDraft: false,
		}),
	});
	if (!res.ok)
		throw new Error(
			`Create opportunity failed: ${res.status} ${await res.text()}`,
		);
	return res.json();
}

async function applyToOpportunity(token, opportunityId, message) {
	const res = await fetch(
		`${API}/v1/volunteer-opportunities/${opportunityId}/engagements`,
		{
			method: "POST",
			headers: {
				Authorization: `Bearer ${token}`,
				"Content-Type": "application/json",
			},
			body: JSON.stringify({ message }),
		},
	);
	if (!res.ok)
		throw new Error(`Apply failed: ${res.status} ${await res.text()}`);
	return res.json();
}

async function confirmEngagement(token, engagementId) {
	const res = await fetch(`${API}/v1/engagements/${engagementId}/confirm`, {
		method: "POST",
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok)
		throw new Error(`Confirm failed: ${res.status} ${await res.text()}`);
}

async function deleteOpportunity(token, opportunityId) {
	const res = await fetch(`${API}/v1/volunteer-opportunities/${opportunityId}`, {
		method: "DELETE",
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok)
		throw new Error(`Delete failed: ${res.status} ${await res.text()}`);
}

async function getMyEngagements(token) {
	const res = await fetch(`${API}/v1/me/engagements`, {
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok) throw new Error(`GET /me/engagements failed: ${res.status}`);
	return res.json();
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

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok)
		throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	// --- Setup: olaf creates an opportunity, vera applies and gets confirmed, olaf deletes it ---
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

	const opportunity = await createOpportunity(
		olafToken,
		orgId,
		`Smoke667 EngagementHistory ${Date.now()}`,
	);
	console.log(`OK  Created opportunity ${opportunity.id}`);

	const veraToken = await getToken("vera", "vera123");
	const engagement = await applyToOpportunity(
		veraToken,
		opportunity.id,
		"Smoke test application for #667.",
	);
	console.log(`OK  vera applied - engagement ${engagement.id}`);

	await confirmEngagement(olafToken, engagement.id);
	console.log("OK  olaf confirmed vera's application");

	await deleteOpportunity(olafToken, opportunity.id);
	console.log("OK  olaf deleted the opportunity");

	// === Ground-truth data check: the engagement still appears, marked Cancelled ===
	const myEngagements = await getMyEngagements(veraToken);
	const historyEntry = myEngagements.find((e) => e.id === engagement.id);
	if (!historyEntry) {
		throw new Error(
			"Engagement for the deleted opportunity is missing entirely from GET /v1/me/engagements - it should still appear as Cancelled",
		);
	}
	if (historyEntry.status !== "Cancelled") {
		throw new Error(
			`Expected status "Cancelled" for the deleted-opportunity engagement, got "${historyEntry.status}"`,
		);
	}
	if (historyEntry.opportunityTitle !== null && historyEntry.opportunityTitle !== undefined) {
		throw new Error(
			`Expected opportunityTitle to be null for a deleted opportunity, got "${historyEntry.opportunityTitle}"`,
		);
	}
	console.log(
		"OK  GET /v1/me/engagements still lists the engagement, status Cancelled, opportunityTitle: null",
	);

	// === UI check: "My Profile -> Engagements" shows the fallback title, not a blank/missing card ===
	{
		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "vera", "vera123");
			await page.goto(`${BASE}/my-engagements`, { waitUntil: "networkidle" });

			const fallbackTitle = page.getByText(
				/this opportunity has been removed|dieser bedarf wurde entfernt/i,
			);
			await fallbackTitle.first().waitFor({ timeout: 15000 });
			console.log(
				'OK  "My Profile -> Engagements" shows the fallback title for the deleted opportunity instead of dropping the card',
			);
		} finally {
			await browser.close();
		}
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
