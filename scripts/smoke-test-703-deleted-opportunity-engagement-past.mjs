// Smoke test for #703:
//
// EngagementReadRepository.GetByVolunteerAsync bucketed a volunteer's own
// engagements into "Current & Upcoming" vs. "Past" purely by
// Engagement.Status. A Pending, or Confirmed-but-not-checked-in, engagement
// whose opportunity had been hard-deleted has no date field left to compare
// against and no code path ever re-evaluated it, so it stayed in "Current &
// Upcoming" forever.
//
// Fix: GetByVolunteerAsync now also checks whether the engagement's
// opportunity still exists. A non-terminal engagement whose opportunity is
// gone is now bucketed into Past instead.
//
// Note on live-verification scope: DeleteVolunteerOpportunityCommandHandler
// already cancels every active (Pending/Confirmed) engagement before
// deleting the opportunity row, so a *freshly* deleted opportunity's
// engagement always ends up Cancelled, not Pending/Confirmed - the exact
// "opportunity gone but engagement still non-terminal" state #703 reports
// can only arise from stale data (or any path that removes the row without
// going through that handler), which cannot be reproduced against live
// staging without direct DB access. That state - and the fix's bucketing of
// it into Past - is covered by the new automated
// IntegrationTests/EngagementTests.cs::GetMyEngagements_MovesToPast_WhenOpportunityIsGoneAndEngagementIsNonTerminal
// and VisualTests/EngagementHistoryForDeletedOpportunityTests.cs::MyEngagementsPage_MovesToPast_ForNonTerminalEngagementWithGoneOpportunity
// tests instead, both of which delete the opportunity row directly. This
// script instead verifies the *normal* delete-opportunity path is
// unaffected by the query rewrite: the Cancelled-and-deleted case (#667)
// still resolves the same way as before.
//
// Run: node scripts/smoke-test-703-deleted-opportunity-engagement-past.mjs

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
			description: "Automated smoke test opportunity for #703.",
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

async function deleteOpportunity(token, opportunityId) {
	const res = await fetch(
		`${API}/v1/volunteer-opportunities/${opportunityId}`,
		{
			method: "DELETE",
			headers: { Authorization: `Bearer ${token}` },
		},
	);
	if (!res.ok)
		throw new Error(`Delete failed: ${res.status} ${await res.text()}`);
}

async function getMyEngagements(token, upcoming) {
	const res = await fetch(
		`${API}/v1/me/engagements?pageNumber=1&pageSize=50&upcoming=${upcoming}`,
		{ headers: { Authorization: `Bearer ${token}` } },
	);
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
		`Smoke703 EngagementBucketing ${Date.now()}`,
	);
	console.log(`OK  Created opportunity ${opportunity.id}`);

	const veraToken = await getToken("vera", "vera123");
	const engagement = await applyToOpportunity(
		veraToken,
		opportunity.id,
		"Smoke test application for #703 - left Pending, never confirmed.",
	);
	console.log(`OK  vera applied (left Pending) - engagement ${engagement.id}`);

	await deleteOpportunity(olafToken, opportunity.id);
	console.log("OK  olaf deleted the opportunity");

	// === Ground-truth data check: the normal delete flow still cancels a
	// Pending engagement (not just Confirmed) and buckets it into Past. ===
	const upcoming = await getMyEngagements(veraToken, true);
	if (upcoming.items.some((e) => e.id === engagement.id)) {
		throw new Error(
			"Deleted-opportunity engagement unexpectedly still appears under upcoming=true",
		);
	}
	console.log("OK  upcoming=true no longer lists the deleted-opportunity engagement");

	const past = await getMyEngagements(veraToken, false);
	const historyEntry = past.items.find((e) => e.id === engagement.id);
	if (!historyEntry) {
		throw new Error(
			"Engagement for the deleted opportunity is missing from upcoming=false - it should still appear as Cancelled",
		);
	}
	if (historyEntry.status !== "Cancelled") {
		throw new Error(
			`Expected status "Cancelled" for the deleted-opportunity engagement, got "${historyEntry.status}"`,
		);
	}
	console.log(
		"OK  upcoming=false lists the engagement, status Cancelled (normal delete-cancels-active-engagements path unaffected)",
	);

	// === UI check: "My Profile -> Activity" Past tab shows the fallback title ===
	{
		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "vera", "vera123");
			await page.goto(`${BASE}/my-engagements`, { waitUntil: "networkidle" });

			await page.click("[data-testid='engagements-scope-past']");

			const fallbackTitle = page.getByText(
				/this opportunity has been removed|dieser bedarf wurde entfernt/i,
			);
			await fallbackTitle.first().waitFor({ timeout: 15000 });
			console.log(
				'OK  "My Profile -> Activity" Past tab shows the fallback title for the deleted opportunity',
			);
		} finally {
			await browser.close();
		}
	}

	console.log(
		"\nNote: the exact bug scenario (opportunity gone, engagement still Pending/Confirmed, i.e. not cancelled first) cannot be reproduced against live staging - the app's own delete flow always cancels active engagements first. That state is covered by the automated IntegrationTests/VisualTests added in this PR, which delete the opportunity row directly.",
	);
	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
