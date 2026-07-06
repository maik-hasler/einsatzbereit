/**
 * Smoke test for #576: unify profile view/edit into one shared component for
 * users and organizations.
 *
 * Verifies against the live staging environment:
 * - Bio/skills/languages/preferred contact set on a volunteer's own profile
 *   (ProfileOverviewPage) now appear on their public profile page
 *   (UserProfilePage), which previously only showed avatar/name/badges.
 * - The organization dashboard's Settings tab (read-only view) and the
 *   public organization profile page render the same organization name via
 *   the shared OrganizationProfileView component.
 *
 * Run: node scripts/smoke-test-576-shared-profile.mjs
 */

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function main() {
	const apiRes = await fetch(`${API}/health`);
	if (!apiRes.ok) throw new Error(`Health check failed: ${apiRes.status}`);
	console.log("OK  API health check passed");

	const { browser, page } = await launchLiveBrowser();

	try {
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		await page.waitForSelector("h1", { timeout: 15000 });

		const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
		if ((await signInBtn.count()) > 0) {
			await signInBtn.first().click();
			await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
			await loginKeycloak(page, "vera", "vera123");
		}
		await page.waitForSelector("main", { timeout: 10000 });
		console.log("OK  Logged in as vera");

		// --- Set bio/skill/language/preferred contact on vera's own profile ---
		const marker = `Smoke576 ${Date.now()}`;
		await page.goto(`${BASE}/profile`, { waitUntil: "networkidle" });
		await page.getByRole("button", { name: /^edit$|^bearbeiten$/i }).click();

		await page.fill("#bio", marker);
		await page.fill("#skill-input", "Erste Hilfe");
		await page.press("#skill-input", "Enter");
		await page.fill("#lang-input", "Klingonisch");
		await page.press("#lang-input", "Enter");
		// Preferred-contact is a custom Dropdown (WAI-ARIA combobox), not a
		// native <select> - open it and pick "Email" by visible option text.
		await page.locator("#preferred-contact").click();
		await page.getByRole("option", { name: /^email$/i }).click();

		const [saveResponse] = await Promise.all([
			page.waitForResponse(
				(r) =>
					r.url().endsWith("/v1/users/me") && r.request().method() === "PUT",
			),
			page.getByRole("button", { name: /^save$|^speichern$/i }).click(),
		]);
		if (saveResponse.status() !== 204) {
			throw new Error(
				`PUT /v1/users/me returned ${saveResponse.status()}, expected 204`,
			);
		}
		await page.waitForSelector("text=" + marker, { timeout: 10000 });
		console.log(
			"OK  Saved bio/skill/language/preferred contact on own profile (PUT 204)",
		);

		// --- Get vera's public userId via the achievements share link ---
		await page.goto(`${BASE}/profile?tab=achievements`, {
			waitUntil: "networkidle",
		});
		await page
			.getByRole("button", {
				name: /share achievements|errungenschaften teilen/i,
			})
			.click();
		const shareText = await page
			.locator("[role='dialog'] span.truncate")
			.first()
			.textContent();
		const userIdMatch = shareText?.match(/\/users\/([^/]+)\/achievements/);
		if (!userIdMatch)
			throw new Error(`Could not extract userId from "${shareText}"`);
		const userId = userIdMatch[1];
		await page.keyboard.press("Escape");
		console.log(`OK  Resolved public profile userId ${userId}`);

		// --- Public profile page shows the previously-missing fields ---
		// Poll (fresh navigation each attempt) instead of a single wait, in case
		// of a brief propagation delay between the write and this read.
		let bioVisible = false;
		for (let attempt = 0; attempt < 5 && !bioVisible; attempt++) {
			if (attempt > 0) await page.waitForTimeout(1000);
			await page.goto(`${BASE}/users/${userId}`, { waitUntil: "networkidle" });
			bioVisible = (await page.getByText(marker).count()) > 0;
		}
		if (!bioVisible)
			throw new Error("Public profile page never showed the saved bio");
		console.log("OK  Public profile page shows bio");

		if ((await page.locator("text=Erste Hilfe").count()) === 0) {
			throw new Error("Public profile page does not show the skill chip");
		}
		console.log("OK  Public profile page shows skills");

		if ((await page.locator("text=Klingonisch").count()) === 0) {
			throw new Error("Public profile page does not show the language chip");
		}
		console.log("OK  Public profile page shows languages");

		if ((await page.getByText(/^E-?Mail$/i).count()) === 0) {
			throw new Error(
				"Public profile page does not show the preferred contact method",
			);
		}
		console.log("OK  Public profile page shows preferred contact method");

		// --- Organization Settings tab and public profile render the same name ---
		const orgName = `Smoke576 Org ${Date.now()}`;
		await page.goto(`${BASE}/profile`, { waitUntil: "networkidle" });
		await page.getByTestId("create-org-btn").click();
		await page.waitForSelector("[role='dialog']", { timeout: 10000 });
		await page.fill("input[type='text']", orgName);
		const [createResponse] = await Promise.all([
			page.waitForResponse(
				(r) =>
					r.url().endsWith("/v1/organizations") &&
					r.request().method() === "POST",
			),
			page.getByTestId("modal-submit").click(),
		]);
		const createdOrg = await createResponse.json();
		const orgId = createdOrg.id?.value ?? createdOrg.id;
		if (!orgId)
			throw new Error(
				`Create organization response had no id: ${JSON.stringify(createdOrg)}`,
			);
		await page.waitForSelector("[role='dialog']", {
			state: "detached",
			timeout: 10000,
		});
		console.log(`OK  Created organization "${orgName}" (${orgId})`);

		await page.goto(`${BASE}/organizations/${orgId}/dashboard?tab=settings`, {
			waitUntil: "networkidle",
		});
		await page.waitForSelector(`text=${orgName}`, { timeout: 10000 });
		console.log(
			"OK  Org dashboard Settings tab (read-only view) shows the org name",
		);

		await page.goto(`${BASE}/organizations/${orgId}`, {
			waitUntil: "networkidle",
		});
		await page.waitForSelector(`h1:has-text("${orgName}")`, { timeout: 10000 });
		console.log("OK  Public organization profile page shows the same org name");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
