// Smoke test for #668:
//
// Milestone achievements ("First Step" / "Dedicated" / "Century") used to be
// awarded by matching a volunteer's live "currently confirmed" engagement
// count against an exact threshold. That count is not monotonic - it drops
// whenever a confirmed engagement is cancelled, including when an organizer
// deletes the opportunity behind it (a normal, supported action), so a
// volunteer could permanently skip past a threshold and never land on it
// again.
//
// Fix: a new monotonically-increasing UserStreak.TotalConfirmedEngagements
// counter is incremented on every confirmation and never decremented, and
// milestones are evaluated with >= against it instead of an exact match.
//
// Reproducing the exact "permanently denied a badge" scenario needs a
// disposable volunteer account with a guaranteed-clean history (the seeded
// vera/olaf accounts have long, mixed histories that make isolating a clean
// threshold crossing impractical) - that exact scenario is covered by the
// automated MilestoneAchievementTests VisualTests suite in CI, which
// provisions and tears down such an account against the local Aspire stack.
// This live-staging script instead exercises the fixed code path itself
// end-to-end against production data: confirm an engagement, delete its
// opportunity (pulling the live confirmed count back down - the exact
// trigger from #667/#668), then confirm another engagement immediately
// after, and assert the confirmation succeeds and no previously-earned
// achievement is lost.
//
// Run: node scripts/smoke-test-668-milestone-achievements.mjs

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
			description: "Automated smoke test opportunity for #668.",
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

async function getAchievements(token) {
	const res = await fetch(`${API}/v1/me/achievements`, {
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok) throw new Error(`GET /me/achievements failed: ${res.status}`);
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
	const veraToken = await getToken("vera", "vera123");

	const orgsRes = await fetch(`${API}/v1/organizations`, {
		headers: { Authorization: `Bearer ${olafToken}` },
	});
	if (!orgsRes.ok)
		throw new Error(`GET /organizations failed: ${orgsRes.status}`);
	const orgs = await orgsRes.json();
	if (!Array.isArray(orgs) || orgs.length === 0)
		throw new Error("olaf has no organizations - cannot run this smoke test");
	const orgId = orgs[0].id;

	const achievementsBefore = await getAchievements(veraToken);
	const namesBefore = new Set(achievementsBefore.map((a) => a.name));
	console.log(
		`OK  vera has ${namesBefore.size} achievement(s) before the test`,
	);

	// --- Engagement A: confirm, then delete its opportunity. This is the
	// exact trigger from #667/#668: DeleteVolunteerOpportunityCommandHandler
	// cancels the confirmed Engagement, pulling vera's *live* confirmed count
	// back down without touching the new lifetime counter. ---
	const opportunityA = await createOpportunity(
		olafToken,
		orgId,
		`Smoke668 MilestoneA ${Date.now()}`,
	);
	const engagementA = await applyToOpportunity(
		veraToken,
		opportunityA.id,
		"Smoke test application A for #668.",
	);
	await confirmEngagement(olafToken, engagementA.id);
	console.log(`OK  Confirmed engagement A (${engagementA.id})`);

	await deleteOpportunity(olafToken, opportunityA.id);
	console.log(
		"OK  Deleted opportunity A - vera's live confirmed count just dropped by 1",
	);

	// --- Engagement B: confirm immediately after the count-lowering deletion.
	// Before the fix, a volunteer landing exactly on a threshold here could
	// have that crossing silently swallowed by the deflated live count; the
	// fix's >= check against the monotonic lifetime counter means this must
	// still succeed and never regress an already-earned badge. ---
	const opportunityB = await createOpportunity(
		olafToken,
		orgId,
		`Smoke668 MilestoneB ${Date.now()}`,
	);
	const engagementB = await applyToOpportunity(
		veraToken,
		opportunityB.id,
		"Smoke test application B for #668.",
	);
	await confirmEngagement(olafToken, engagementB.id);
	console.log(
		`OK  Confirmed engagement B (${engagementB.id}) right after the count-lowering deletion - RecordConfirmedEngagement()/milestone evaluation did not throw`,
	);

	const achievementsAfter = await getAchievements(veraToken);
	const namesAfter = new Set(achievementsAfter.map((a) => a.name));
	const lost = [...namesBefore].filter((n) => !namesAfter.has(n));
	if (lost.length > 0) {
		throw new Error(
			`Achievement(s) disappeared after the confirm/delete/confirm sequence: ${lost.join(", ")}`,
		);
	}
	console.log(
		"OK  No previously-earned achievement was lost or revoked (idempotent awarding still holds)",
	);

	// === UI check: vera's achievements tab still renders cleanly ===
	{
		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "vera", "vera123");
			await page.goto(`${BASE}/profile?tab=achievements`, {
				waitUntil: "networkidle",
			});
			await page.waitForSelector("main", { timeout: 10000 });
			const errorLocator = page.getByText(
				/something went wrong|ein fehler ist aufgetreten/i,
			);
			if ((await errorLocator.count()) > 0) {
				throw new Error(
					"Achievements tab rendered an error state after the confirm/delete/confirm sequence",
				);
			}
			console.log("OK  vera's Achievements tab renders without error");
		} finally {
			await browser.close();
		}
	}

	// Cleanup: opportunity A is already gone; delete opportunity B too.
	await deleteOpportunity(olafToken, opportunityB.id);
	console.log("OK  Cleaned up opportunity B");

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
