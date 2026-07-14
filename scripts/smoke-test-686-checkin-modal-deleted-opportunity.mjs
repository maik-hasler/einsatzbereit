/**
 * Smoke test for #686: CheckInModal's opportunity-details fetch had no
 * .catch(), so a 404 (opportunity deleted after the engagements list had
 * already loaded) left the modal stuck on "Loading..." forever with an
 * unhandled promise rejection.
 *
 * Verifies against the live staging environment:
 * - Creating an opportunity, applying as vera, and confirming as olaf (all
 *   via the API).
 * - Loading vera's "My Profile -> Engagements" list in the browser, where
 *   the Confirmed engagement's "Check in" button is visible.
 * - Deleting the opportunity via the API (simulating an organizer deleting
 *   it in another tab, after the volunteer's page has already loaded).
 * - Clicking the still-rendered "Check in" button shows the friendly error
 *   message instead of hanging on "Loading...".
 * - Cleans up the throwaway organization/opportunity afterwards.
 *
 * Run: node scripts/smoke-test-686-checkin-modal-deleted-opportunity.mjs
 */

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const KEYCLOAK = "https://login.maik-hasler.de/realms/einsatzbereit";
const CLIENT_ID = "frontend";

async function getToken(username, password) {
	const body = new URLSearchParams({
		grant_type: "password",
		client_id: CLIENT_ID,
		username,
		password,
		scope: "openid",
	});
	const res = await fetch(`${KEYCLOAK}/protocol/openid-connect/token`, {
		method: "POST",
		body,
	});
	if (!res.ok) throw new Error(`Token request failed: ${res.status}`);
	const data = await res.json();
	return data.access_token;
}

async function apiPost(path, token, body) {
	const res = await fetch(`${API}${path}`, {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: body === undefined ? undefined : JSON.stringify(body),
	});
	if (!res.ok) {
		const text = await res.text();
		throw new Error(`POST ${path} failed: ${res.status} - ${text}`);
	}
	const text = await res.text();
	return text ? JSON.parse(text) : null;
}

async function apiDelete(path, token) {
	const res = await fetch(`${API}${path}`, {
		method: "DELETE",
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok) {
		const text = await res.text();
		throw new Error(`DELETE ${path} failed: ${res.status} - ${text}`);
	}
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const olafToken = await getToken("olaf", "olaf123");
	const veraToken = await getToken("vera", "vera123");
	console.log("OK  Got olaf and vera tokens");

	const suffix = Date.now();

	const org = await apiPost("/v1/organizations", olafToken, {
		name: `Smoke686 Org ${suffix}`,
	});
	const organizationId = org.id.value;
	console.log(`OK  Created organization ${organizationId}`);

	const oppTitle = `Smoke686 CheckIn Deleted Opp ${suffix}`;
	const opportunity = await apiPost("/v1/volunteer-opportunities", olafToken, {
		title: oppTitle,
		description: "Created by smoke-test-686-checkin-modal-deleted-opportunity.mjs",
		organizationId,
		isRemote: true,
		occurrence: "OneTime",
		participationType: "IndividualContact",
		checkInMethod: "None",
		isDraft: false,
	});
	const opportunityId = opportunity.id;
	console.log(`OK  Created opportunity ${opportunityId}`);

	const engagement = await apiPost(
		`/v1/volunteer-opportunities/${opportunityId}/engagements`,
		veraToken,
		{ message: "Applying via smoke-test-686-checkin-modal-deleted-opportunity.mjs" },
	);
	const engagementId = engagement.id;
	console.log(`OK  vera applied, engagement ${engagementId}`);

	await apiPost(`/v1/engagements/${engagementId}/confirm`, olafToken);
	console.log("OK  olaf confirmed the application");

	const { browser, page } = await launchLiveBrowser();
	try {
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
		if ((await signInBtn.count()) > 0) {
			await signInBtn.first().click();
			await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
			await loginKeycloak(page, "vera", "vera123");
		}
		await page.waitForSelector("main", { timeout: 15000 });
		console.log("OK  Logged in as vera");

		await page.goto(`${BASE}/profile?tab=engagements`, { waitUntil: "networkidle" });

		const row = page.locator("li", { hasText: oppTitle });
		await row.waitFor({ state: "visible", timeout: 15000 });
		const checkInButton = row.getByRole("button", { name: "Check in" });
		await checkInButton.waitFor({ state: "visible", timeout: 15000 });
		console.log('OK  "Check in" button visible on the Confirmed engagement');

		// Simulate the race: the organizer deletes the opportunity (e.g. in
		// another tab) after vera's engagements list has already loaded.
		await apiDelete(`/v1/volunteer-opportunities/${opportunityId}`, olafToken);
		console.log("OK  olaf deleted the opportunity while vera's page was still open");

		await checkInButton.click();
		const dialog = page.locator("[role='dialog']");
		await dialog.waitFor({ state: "visible", timeout: 10000 });

		const errorMessage = dialog.getByText("This opportunity is no longer available.");
		await errorMessage.waitFor({ state: "visible", timeout: 15000 });
		console.log("OK  Modal shows the friendly error message instead of hanging");

		const stillLoading = await dialog.getByText("Loading…").count();
		if (stillLoading > 0) {
			throw new Error('Modal is still showing "Loading..." alongside the error message');
		}
		console.log('OK  Modal is not stuck on "Loading..."');

		await dialog.getByRole("button", { name: "Done" }).click();
		await dialog.waitFor({ state: "hidden", timeout: 5000 });
		console.log('OK  "Done" closes the modal');
	} finally {
		await browser.close();
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
