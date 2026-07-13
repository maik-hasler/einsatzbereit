// Smoke test for #675:
//
// "My Profile -> Engagements" loaded the volunteer's entire engagement
// history in one unbounded flat list, with no way to separate active/
// upcoming engagements from historical ones. Per the repo owner's direction
// on the issue, the tab is now split into "Current & Upcoming" (default) and
// "Past", each paginated via GET /v1/me/engagements?pageNumber&pageSize&upcoming.
//
// Fix: EngagementReadRepository.GetByVolunteerAsync now filters + paginates
// server-side. Pending/Confirmed-not-checked-in engagements are "upcoming";
// Cancelled/Withdrawn/Confirmed-checked-in engagements are "past".
//
// Run: node scripts/smoke-test-675-engagements-scope-tabs.mjs

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

async function createOrganization(token, name) {
	const res = await fetch(`${API}/v1/organizations`, {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify({ name }),
	});
	if (!res.ok)
		throw new Error(
			`Create organization failed: ${res.status} ${await res.text()}`,
		);
	const org = await res.json();
	return org.id.value ?? org.id;
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
			description: "Automated smoke test opportunity for #675.",
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

async function deleteOpportunity(token, opportunityId) {
	const res = await fetch(
		`${API}/v1/volunteer-opportunities/${opportunityId}`,
		{
			method: "DELETE",
			headers: { Authorization: `Bearer ${token}` },
		},
	);
	if (!res.ok) throw new Error(`Delete opportunity failed: ${res.status}`);
}

async function deleteOrganization(token, orgId) {
	const res = await fetch(`${API}/v1/organizations/${orgId}`, {
		method: "DELETE",
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok) throw new Error(`Delete organization failed: ${res.status}`);
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

	const suffix = Date.now();
	const olafToken = await getToken("olaf", "olaf123");
	const orgId = await createOrganization(olafToken, `Smoke675 Org ${suffix}`);

	const upcomingOpportunity = await createOpportunity(
		olafToken,
		orgId,
		`Smoke675Upcoming ${suffix}`,
	);
	const pastOpportunity = await createOpportunity(
		olafToken,
		orgId,
		`Smoke675Past ${suffix}`,
	);
	console.log(
		`OK  Created opportunities ${upcomingOpportunity.id} (upcoming) and ${pastOpportunity.id} (past)`,
	);

	const veraToken = await getToken("vera", "vera123");
	const upcomingEngagement = await applyToOpportunity(
		veraToken,
		upcomingOpportunity.id,
		"Staying pending for #675 smoke test.",
	);
	const pastEngagement = await applyToOpportunity(
		veraToken,
		pastOpportunity.id,
		"About to withdraw for #675 smoke test.",
	);
	await withdrawEngagement(veraToken, pastEngagement.id);
	console.log(
		"OK  vera applied to both - one stays Pending, the other was withdrawn",
	);

	// === Ground-truth data check: server-side split is correct ===
	const upcomingPage = await getMyEngagements(veraToken, true);
	const pastPage = await getMyEngagements(veraToken, false);

	const upcomingIds = upcomingPage.items.map((e) => e.id);
	const pastIds = pastPage.items.map((e) => e.id);

	if (!upcomingIds.includes(upcomingEngagement.id)) {
		throw new Error(
			"Pending engagement missing from upcoming=true page - expected it in Current & Upcoming",
		);
	}
	if (upcomingIds.includes(pastEngagement.id)) {
		throw new Error(
			"Withdrawn engagement leaked into upcoming=true page - should only be in Past",
		);
	}
	if (!pastIds.includes(pastEngagement.id)) {
		throw new Error(
			"Withdrawn engagement missing from upcoming=false page - expected it in Past",
		);
	}
	if (pastIds.includes(upcomingEngagement.id)) {
		throw new Error(
			"Pending engagement leaked into upcoming=false page - should only be in Current & Upcoming",
		);
	}
	console.log(
		"OK  GET /v1/me/engagements correctly splits Pending into upcoming=true and Withdrawn into upcoming=false",
	);

	// === UI check: tabs correctly show/hide each engagement ===
	{
		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "vera", "vera123");
			await page.goto(`${BASE}/profile?tab=engagements`, {
				waitUntil: "networkidle",
			});

			await page
				.getByText(`Smoke675Upcoming ${suffix}`)
				.first()
				.waitFor({ timeout: 15000 });
			const pastVisibleByDefault = await page
				.getByText(`Smoke675Past ${suffix}`)
				.count();
			if (pastVisibleByDefault > 0) {
				throw new Error(
					'Withdrawn engagement is visible under the default "Current & Upcoming" tab - it should only show under "Past"',
				);
			}
			console.log(
				'OK  Default "Current & Upcoming" tab shows the pending engagement and hides the withdrawn one',
			);

			await page.locator("[data-testid='engagements-scope-past']").click();
			await page
				.getByText(`Smoke675Past ${suffix}`)
				.first()
				.waitFor({ timeout: 15000 });
			const upcomingVisibleInPast = await page
				.getByText(`Smoke675Upcoming ${suffix}`)
				.count();
			if (upcomingVisibleInPast > 0) {
				throw new Error(
					'Pending engagement is visible under the "Past" tab - it should only show under "Current & Upcoming"',
				);
			}
			console.log(
				'OK  "Past" tab shows the withdrawn engagement and hides the pending one'
			);
		} finally {
			await browser.close();
		}
	}

	// --- Cleanup ---
	await withdrawEngagement(veraToken, upcomingEngagement.id);
	await deleteOpportunity(olafToken, upcomingOpportunity.id);
	await deleteOpportunity(olafToken, pastOpportunity.id);
	await deleteOrganization(olafToken, orgId);
	console.log("OK  Cleaned up test opportunities and organization");

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
