// Smoke test for #708: after saving a new volunteer opportunity as a draft,
// organizers could not tell where the draft landed - the toast only said "on
// your organization dashboard" and the Drafts section lives on the Calendar
// tab. This drives the create wizard on the live site and verifies:
//   1. The success toast now names the "Drafts section" (accurate copy).
//   2. The just-saved draft shows up in the Drafts section on the same tab it
//      was created from, highlighted (data-highlighted="true") so it is easy
//      to spot without exploring other tabs.
// Creates a throwaway organization + drives the wizard, then deletes the draft
// opportunity and the organization so this script leaves no junk on the live
// site.
// Run: node scripts/smoke-test-708-draft-discoverability.mjs

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
	if (!healthRes.ok)
		throw new Error(`Health check failed: ${healthRes.status}`);
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
		body: JSON.stringify({ name: `Smoke708 Org ${Date.now()}` }),
	});
	if (!orgRes.ok)
		throw new Error(
			`Create organization failed: ${orgRes.status} ${await orgRes.text()}`,
		);
	const org = await orgRes.json();
	const orgId = org.id.value;
	console.log(`OK  Created throwaway organization ${orgId}`);

	let opportunityId;
	try {
		const { browser, page } = await launchLiveBrowser();
		try {
			await page.goto(BASE, { waitUntil: "networkidle" });
			await page.click("text=/sign in|anmelden/i");
			await page.waitForURL(/\/realms\//, { timeout: 30000 });
			await loginKeycloak(page, "olaf", "olaf123");

			// Land on the throwaway org's Calendar tab, where "Create
			// opportunity" and the Drafts section both live.
			await page.goto(`${BASE}/app/${orgId}/dashboard`, {
				waitUntil: "networkidle",
			});

			const createBtn = page.getByRole("button", {
				name: "Create opportunity",
			});
			await createBtn.waitFor({ state: "visible", timeout: 15000 });
			await createBtn.click();

			const dialog = page.locator("[role='dialog']");
			await dialog.waitFor({ state: "visible", timeout: 5000 });

			const draftTitle = `Smoke708 Draft ${Date.now()}`;
			await page.locator("#opportunity-title").fill(draftTitle);
			await page.getByTestId("modal-save-draft").click();

			// Dialog close waits on the create-draft API call.
			await dialog.waitFor({ state: "hidden", timeout: 30000 });

			// 1. Toast copy now names the Drafts section instead of the old
			//    vague "on your organization dashboard" wording.
			const toast = page
				.getByRole("alert")
				.filter({ hasText: "Drafts section" });
			await toast.waitFor({ state: "visible", timeout: 4000 });
			console.log('OK  Success toast names the "Drafts section" (#708 copy)');

			// 2. The draft is revealed in the Drafts section on the same tab,
			//    highlighted so it is easy to spot.
			const draftsSection = page.getByTestId("drafts-section");
			await draftsSection.waitFor({ state: "visible", timeout: 10000 });

			const highlighted = draftsSection.locator("li[data-highlighted='true']");
			await highlighted.waitFor({ state: "visible", timeout: 5000 });
			const highlightedText = await highlighted.innerText();
			if (!highlightedText.includes(draftTitle))
				throw new Error(
					`Highlighted draft does not contain the saved title. Saw: ${highlightedText}`,
				);
			console.log(
				"OK  Saved draft is highlighted in the Drafts section on the same tab",
			);

			// Recover the created opportunity id from the draft card link so we
			// can clean it up afterwards.
			const href = await highlighted
				.locator("a[href*='/volunteer-opportunities/']")
				.first()
				.getAttribute("href");
			const match = href?.match(/\/volunteer-opportunities\/([^/?#]+)/);
			if (match) opportunityId = match[1];
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
