// Smoke test for #659:
//
// Two static Tailwind class combinations on "My Profile -> Engagements"
// failed WCAG AA color-contrast (axe-core "serious" color-contrast
// violation):
// - The "Signed up: {date}" text used text-gray-400 directly on the white
//   card background (~2.87:1 contrast, below the 4.5:1 AA minimum).
// - The Withdrawn status badge used text-gray-500 on bg-gray-100
//   (~4.39:1, narrowly below 4.5:1).
//
// Fix: darkened the date line to text-gray-500 (~4.83:1) and the Withdrawn
// badge to text-gray-700 (~9.4:1), matching the -700 text shade the other
// three status entries already use.
//
// Run: node scripts/smoke-test-659-contrast.mjs

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
			description: "Automated smoke test opportunity for #659.",
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
	const orgId = await createOrganization(
		olafToken,
		`Smoke659 Org ${Date.now()}`,
	);
	const opportunity = await createOpportunity(
		olafToken,
		orgId,
		`Smoke659 Contrast ${Date.now()}`,
	);
	console.log(`OK  Created opportunity ${opportunity.id}`);

	const veraToken = await getToken("vera", "vera123");
	const engagement = await applyToOpportunity(
		veraToken,
		opportunity.id,
		"Smoke test application for #659.",
	);
	await withdrawEngagement(veraToken, engagement.id);
	console.log("OK  Applied and withdrew - produced a Withdrawn engagement");

	{
		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "vera", "vera123");
			await page.goto(`${BASE}/profile?tab=engagements`, {
				waitUntil: "networkidle",
			});

			const withdrawnBadge = page.getByText("Withdrawn").first();
			await withdrawnBadge.waitFor({ timeout: 15000 });
			const badgeClass = await withdrawnBadge.getAttribute("class");
			if (!badgeClass?.includes("text-gray-700")) {
				throw new Error(
					`Expected the Withdrawn badge to use text-gray-700, got class="${badgeClass}"`,
				);
			}
			if (badgeClass.includes("text-gray-500")) {
				throw new Error(
					`Withdrawn badge still uses the low-contrast text-gray-500 class="${badgeClass}"`,
				);
			}
			const badgeColor = await withdrawnBadge.evaluate(
				(el) => getComputedStyle(el).color,
			);
			console.log(
				`OK  Withdrawn badge uses text-gray-700 (computed color: ${badgeColor})`,
			);

			const dateLine = page.getByText(/Signed up:/).first();
			await dateLine.waitFor({ timeout: 15000 });
			const dateLineClass = await dateLine.getAttribute("class");
			if (!dateLineClass?.includes("text-gray-500")) {
				throw new Error(
					`Expected the "Signed up" date line to use text-gray-500, got class="${dateLineClass}"`,
				);
			}
			if (dateLineClass.includes("text-gray-400")) {
				throw new Error(
					`"Signed up" date line still uses the low-contrast text-gray-400 class="${dateLineClass}"`,
				);
			}
			console.log(
				'OK  "Signed up" date line uses text-gray-500 (was text-gray-400)',
			);
		} finally {
			await browser.close();
		}
	}

	await deleteOpportunity(olafToken, opportunity.id);
	await deleteOrganization(olafToken, orgId);
	console.log("OK  Cleaned up throwaway opportunity and organization");

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
