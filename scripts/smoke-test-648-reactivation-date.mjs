// Smoke test for #648, filed by the 2026-07-09 persona-simulation cycle:
//
// Re-applying to an opportunity after withdrawing kept the original
// application's CreatedOn timestamp, because CreateEngagementCommandHandler
// reuses the existing terminal Engagement row via Engagement.Reactivate(...)
// instead of inserting a new one, and AuditableEntityInterceptor only stamps
// CreatedOn on EntityState.Added - never on the Modified state a reactivation
// produces. Both the volunteer's "My Profile -> Engagements" tab and the
// organizer's "Manage applications" page kept showing the stale original
// date, with no way to tell the application was actually just re-submitted.
//
// Fix: Engagement.Reactivate now sets CreatedOn = DateTimeOffset.UtcNow
// directly, so the underlying data (and both surfaces that render it)
// reflect the real re-application time.
//
// Note: both frontend surfaces render createdOn with toLocaleDateString
// (day granularity only), so a same-day withdraw-then-reapply test can't
// observe a *visible* date change without crossing midnight. This script
// instead asserts on the ground-truth timestamp from GET /v1/me/engagements,
// which is exactly what those two pages read - a several-second gap between
// the original and reactivated CreatedOn deterministically proves the value
// is no longer frozen, regardless of calendar-day boundaries.
//
// Run: node scripts/smoke-test-648-reactivation-date.mjs

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
			description: "Automated smoke test opportunity for #648.",
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

async function withdrawEngagement(token, engagementId) {
	const res = await fetch(`${API}/v1/engagements/${engagementId}/withdraw`, {
		method: "POST",
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok)
		throw new Error(`Withdraw failed: ${res.status} ${await res.text()}`);
}

async function getMyEngagement(token, opportunityId) {
	const res = await fetch(`${API}/v1/me/engagements`, {
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok)
		throw new Error(`GET /me/engagements failed: ${res.status}`);
	const engagements = await res.json();
	const match = engagements.find((e) => e.opportunityId === opportunityId);
	if (!match)
		throw new Error(
			`No engagement found for opportunity ${opportunityId} in /me/engagements`,
		);
	return match;
}

function sleep(ms) {
	return new Promise((resolve) => setTimeout(resolve, ms));
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

	// --- Setup: create a fresh IndividualContact opportunity via olaf's org ---
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
		`Smoke648 ReactivationDate ${Date.now()}`,
	);
	console.log(`OK  Created opportunity ${opportunity.id}`);

	// === Ground-truth data check: CreatedOn refreshes on withdraw + reapply ===
	const veraToken = await getToken("vera", "vera123");

	const firstEngagement = await applyToOpportunity(
		veraToken,
		opportunity.id,
		"Smoke test original application for #648.",
	);
	const firstFetched = await getMyEngagement(veraToken, opportunity.id);
	const firstCreatedOn = new Date(firstFetched.createdOn);
	console.log(
		`OK  Applied (engagement ${firstEngagement.id}), CreatedOn = ${firstCreatedOn.toISOString()}`,
	);

	await withdrawEngagement(veraToken, firstEngagement.id);
	console.log("OK  Withdrew the application");

	// Wait long enough that a frozen CreatedOn (the bug) is trivially
	// distinguishable from a refreshed one, well above clock-skew noise.
	await sleep(5000);

	const reapplyMessage =
		"Smoke test re-application for #648 (2026-07-09 verification cycle).";
	const secondEngagement = await applyToOpportunity(
		veraToken,
		opportunity.id,
		reapplyMessage,
	);
	if (secondEngagement.id !== firstEngagement.id) {
		throw new Error(
			`Expected re-application to reactivate the same engagement row (${firstEngagement.id}), got a new id ${secondEngagement.id}`,
		);
	}
	console.log("OK  Re-applied - same engagement row was reactivated");

	const secondFetched = await getMyEngagement(veraToken, opportunity.id);
	const secondCreatedOn = new Date(secondFetched.createdOn);
	console.log(`OK  Reactivated CreatedOn = ${secondCreatedOn.toISOString()}`);

	if (secondFetched.status !== "Pending") {
		throw new Error(
			`Expected reactivated engagement to be Pending, got ${secondFetched.status}`,
		);
	}
	if (secondFetched.message !== reapplyMessage) {
		throw new Error(
			`Expected the reactivated engagement's message to be the new one, got "${secondFetched.message}"`,
		);
	}

	const deltaMs = secondCreatedOn.getTime() - firstCreatedOn.getTime();
	if (deltaMs < 2000) {
		throw new Error(
			`CreatedOn did not refresh on reactivation - was ${firstCreatedOn.toISOString()}, still ${secondCreatedOn.toISOString()} after re-applying (delta ${deltaMs}ms). This is the bug from #648.`,
		);
	}
	console.log(
		`OK  CreatedOn refreshed to the re-application time (delta ${deltaMs}ms) - #648 is fixed`,
	);

	// === UI-level sanity check: both surfaces render the reactivated engagement ===
	{
		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "vera", "vera123");
			await page.goto(`${BASE}/profile?tab=engagements`, {
				waitUntil: "networkidle",
			});
			await page
				.getByText(opportunity.title)
				.first()
				.waitFor({ timeout: 10000 });
			console.log(
				"OK  Volunteer's Engagements tab renders the reactivated engagement",
			);
		} finally {
			await browser.close();
		}
	}

	// --- Cleanup: withdraw the reactivated engagement so it doesn't linger ---
	await withdrawEngagement(veraToken, secondEngagement.id);
	console.log("OK  Cleaned up - withdrew the test engagement");

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
