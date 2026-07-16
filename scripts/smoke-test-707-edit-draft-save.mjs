// Smoke test for #707: reopening a saved draft volunteer opportunity via
// "Edit" hid the "Save as draft" action entirely (gated on create-vs-edit
// mode instead of the opportunity's actual Draft/Published status), so an
// organizer could not persist further incremental edits without first
// satisfying full publish-level validation. Verifies:
//   1. A draft created with only a title (deliberately incomplete) can be
//      reopened via "Edit".
//   2. The "Save as draft" button IS now visible in edit mode (the
//      regression - it used to be entirely absent).
//   3. A partial edit (title change only, still no address/time slots) can
//      be persisted via that button without full-publish validation
//      blocking it.
// Creates a throwaway organization + draft opportunity and deletes both
// afterwards so this script doesn't leave junk data on the live site.
// Run: node scripts/smoke-test-707-edit-draft-save.mjs

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
		body: JSON.stringify({ name: `Smoke707 Org ${Date.now()}` }),
	});
	if (!orgRes.ok)
		throw new Error(`Create organization failed: ${orgRes.status} ${await orgRes.text()}`);
	const org = await orgRes.json();
	const orgId = org.id.value;
	console.log(`OK  Created throwaway organization ${orgId}`);

	let opportunityId;
	try {
		const draftTitle = `Smoke707 Draft ${Date.now()}`;
		const oppRes = await fetch(`${API}/v1/volunteer-opportunities`, {
			method: "POST",
			headers: authHeaders,
			body: JSON.stringify({
				title: draftTitle,
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
		console.log(`OK  Created draft opportunity ${opportunityId} (title only, no description/address)`);

		const { browser, page } = await launchLiveBrowser();
		try {
			await page.goto(BASE, { waitUntil: "networkidle" });
			await page.click("text=/sign in|anmelden/i");
			await page.waitForURL(/\/realms\//, { timeout: 30000 });
			await loginKeycloak(page, "olaf", "olaf123");

			await page.goto(`${BASE}/volunteer-opportunities/${opportunityId}`, {
				waitUntil: "networkidle",
			});

			const editBtn = page.getByRole("button", { name: "Edit" });
			await editBtn.waitFor({ state: "visible", timeout: 15000 });
			await editBtn.click();
			console.log("OK  Opened the draft's edit wizard via \"Edit\"");

			const dialog = page.locator("[role='dialog']");
			await dialog.waitFor({ state: "visible", timeout: 5000 });

			// The regression: this action used to be entirely absent in edit mode.
			const saveDraftBtn = page.getByTestId("modal-save-draft");
			await saveDraftBtn.waitFor({ state: "visible", timeout: 5000 });
			console.log(
				'OK  "Save as draft" button IS visible while editing an existing draft (#707 fixed)',
			);

			const updatedTitle = `${draftTitle} Updated`;
			await page.locator("#opportunity-title").fill(updatedTitle);
			await saveDraftBtn.click();

			// A lenient partial save must succeed without full-publish validation
			// blocking it - still no address, still no time slots.
			await dialog.waitFor({ state: "hidden", timeout: 10000 });
			console.log("OK  Partial edit saved via \"Save as draft\" without full-publish validation blocking it");

			await page.locator("h1", { hasText: updatedTitle }).waitFor({
				state: "visible",
				timeout: 10000,
			});
			console.log(`OK  Detail page reflects the persisted edit ("${updatedTitle}")`);
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
