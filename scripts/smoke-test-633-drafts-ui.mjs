// Smoke test for #633 (PR #632): a draft volunteer opportunity had no UI
// anywhere in the app to see it again after creation - PR #561 deleted the
// only page that rendered an org's drafts as "dead code" without noticing it
// was load-bearing. Verifies the restored drafts section on the org
// dashboard's default "Dashboard" tab shows a draft opportunity with the
// "Draft" badge. Creates a throwaway organization + draft opportunity and
// deletes both afterwards so this script doesn't leave junk data on the
// live site (see #630).
// Run: node scripts/smoke-test-633-drafts-ui.mjs

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

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const token = await getToken("olaf", "olaf123");
	const authHeaders = {
		Authorization: `Bearer ${token}`,
		"Content-Type": "application/json",
	};
	console.log("OK  Got access token for olaf (organisator)");

	const orgRes = await fetch(`${API}/v1/organizations`, {
		method: "POST",
		headers: authHeaders,
		body: JSON.stringify({ name: `Smoke633 Org ${Date.now()}` }),
	});
	if (!orgRes.ok)
		throw new Error(`Create organization failed: ${orgRes.status} ${await orgRes.text()}`);
	const org = await orgRes.json();
	const orgId = org.id.value;
	console.log(`OK  Created throwaway organization ${orgId}`);

	let opportunityId;
	try {
		const draftTitle = `Smoke633 Draft ${Date.now()}`;
		const oppRes = await fetch(`${API}/v1/volunteer-opportunities`, {
			method: "POST",
			headers: authHeaders,
			body: JSON.stringify({
				title: draftTitle,
				description: "Automated smoke test for #633.",
				organizationId: orgId,
				isRemote: true,
				occurrence: "OneTime",
				participationType: "IndividualContact",
				checkInMethod: "None",
				isDraft: true,
			}),
		});
		if (!oppRes.ok)
			throw new Error(`Create draft opportunity failed: ${oppRes.status} ${await oppRes.text()}`);
		const opportunity = await oppRes.json();
		opportunityId = opportunity.id;
		if (opportunity.status !== "Draft")
			throw new Error(`Expected Draft status, got ${opportunity.status}`);
		console.log(`OK  Created draft opportunity ${opportunityId}`);

		const { browser, page } = await launchLiveBrowser();
		try {
			await page.goto(BASE, { waitUntil: "networkidle" });
			await page.click("text=/sign in|anmelden/i");
			await page.waitForURL(/\/realms\//, { timeout: 30000 });
			await loginKeycloak(page, "olaf", "olaf123");

			await page.goto(`${BASE}/organizations/${orgId}/dashboard`, {
				waitUntil: "networkidle",
			});

			const draftsSection = page.getByTestId("drafts-section");
			await draftsSection.waitFor({ state: "visible", timeout: 15000 });

			const draftRow = draftsSection.locator("li", { hasText: draftTitle });
			await draftRow.waitFor({ state: "visible", timeout: 15000 });

			const badgeText = (await draftRow.getByText("Draft", { exact: true }).innerText()).trim();
			if (badgeText !== "Draft") {
				throw new Error(`Expected a "Draft" badge on the draft row, got "${badgeText}"`);
			}
			console.log(
				`OK  Drafts section on org dashboard shows "${draftTitle}" with a "Draft" badge (#633 fixed)`,
			);
		} finally {
			await browser.close();
		}
	} finally {
		if (opportunityId) {
			await fetch(`${API}/v1/volunteer-opportunities/${opportunityId}`, {
				method: "DELETE",
				headers: authHeaders,
			});
		}
		await fetch(`${API}/v1/organizations/${orgId}`, {
			method: "DELETE",
			headers: authHeaders,
		});
		console.log("OK  Cleaned up throwaway draft opportunity and organization");
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
