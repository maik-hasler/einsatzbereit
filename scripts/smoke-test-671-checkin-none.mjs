/**
 * Smoke test for #671: "Check in" opened a blank modal with no way to check
 * in when the opportunity's check-in method is "None".
 *
 * Verifies against the live staging environment:
 * - Creating a "None" check-in opportunity, applying, and confirming the
 *   engagement (all via the API, as olaf both organizer and applicant).
 * - Opening the "Check in" modal from "My Profile -> Engagements" in the
 *   browser shows the new instruction text instead of a blank dialog.
 * - Cleans up the throwaway opportunity afterwards.
 *
 * Run: node scripts/smoke-test-671-checkin-none.mjs
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
	console.log("OK  Got olaf token");

	const suffix = Date.now();

	const org = await apiPost("/v1/organizations", olafToken, {
		name: `Smoke671 Org ${suffix}`,
	});
	const organizationId = org.id.value;
	console.log(`OK  Created organization ${organizationId}`);

	const oppTitle = `Smoke671 None CheckIn ${suffix}`;
	const opportunity = await apiPost("/v1/volunteer-opportunities", olafToken, {
		title: oppTitle,
		description: "Created by smoke-test-671-checkin-none.mjs",
		organizationId,
		isRemote: true,
		occurrence: "OneTime",
		participationType: "IndividualContact",
		checkInMethod: "None",
		isDraft: false,
	});
	const opportunityId = opportunity.id;
	console.log(`OK  Created "None" check-in opportunity ${opportunityId}`);

	const engagement = await apiPost(
		`/v1/volunteer-opportunities/${opportunityId}/engagements`,
		olafToken,
		{ message: "Applying via smoke-test-671-checkin-none.mjs" },
	);
	const engagementId = engagement.id;
	console.log(`OK  Applied, engagement ${engagementId}`);

	await apiPost(`/v1/engagements/${engagementId}/confirm`, olafToken);
	console.log("OK  Confirmed engagement");

	const { browser, page } = await launchLiveBrowser();
	try {
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
		if ((await signInBtn.count()) > 0) {
			await signInBtn.first().click();
			await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
			await loginKeycloak(page, "olaf", "olaf123");
		}
		await page.waitForSelector("main", { timeout: 15000 });
		console.log("OK  Logged in as olaf");

		await page.goto(`${BASE}/profile?tab=engagements`, { waitUntil: "networkidle" });

		const row = page.locator("li", { hasText: oppTitle });
		await row.waitFor({ state: "visible", timeout: 15000 });
		console.log("OK  Engagement card visible in My Profile -> Engagements");

		await row.getByRole("button", { name: "Check in" }).click();
		const dialog = page.locator("[role='dialog']");
		await dialog.waitFor({ state: "visible", timeout: 10000 });

		const instruction = dialog.getByText(
			"This opportunity doesn't require an explicit check-in step.",
		);
		await instruction.waitFor({ state: "visible", timeout: 10000 });
		console.log('OK  Modal shows the "None" instruction text instead of a blank dialog');

		const dialogText = await dialog.textContent();
		if (!dialogText || dialogText.trim() === "Check in") {
			throw new Error(`Expected modal content beyond the title, got: "${dialogText}"`);
		}

		await dialog.getByRole("button", { name: "Done" }).click();
		await dialog.waitFor({ state: "hidden", timeout: 5000 });
		console.log('OK  "Done" closes the modal');
	} finally {
		await browser.close();
	}

	await apiDelete(`/v1/volunteer-opportunities/${opportunityId}`, olafToken);
	console.log("OK  Deleted throwaway opportunity (cleanup)");

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
