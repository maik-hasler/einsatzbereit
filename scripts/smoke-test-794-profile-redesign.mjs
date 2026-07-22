/**
 * Smoke test for issue #794: the personal profile page (/profile) was
 * consolidated from a Profile/Activity tab switcher with a two-column grid
 * into a single cohesive, single-column-first page. The "Share achievements"
 * feature was removed entirely, Edit/Save/Cancel moved from inline buttons
 * into the header's quick-actions toolbar, and the danger-zone copy no
 * longer mentions the internal "Keycloak" term.
 *
 * Verifies against live staging:
 *  - /profile shows Profile Details, Badges, and My Engagements all on one
 *    page, with no Profile/Activity tab buttons and no Share-achievements
 *    button/modal.
 *  - Legacy ?tab= deep links (/my-engagements, /achievements) still redirect
 *    and land on a working page.
 *  - Edit/Save/Cancel appear via the header's quick actions (data-testid
 *    quick-action-*), not inline - editing and saving the bio still works.
 *  - The danger-zone description no longer contains "Keycloak".
 *  - Cleans up after itself: reverts the bio change it made.
 *
 * Run: node scripts/smoke-test-794-profile-redesign.mjs
 */

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function signIn(page, username, password) {
	await page.goto(BASE, { waitUntil: "networkidle" });
	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	await signInBtn.first().click();
	await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
	await loginKeycloak(page, username, password);
	await page.waitForURL(`${BASE}/`, { timeout: 15000 });
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const { browser, page } = await launchLiveBrowser();
	try {
		await signIn(page, "vera", "vera123");
		console.log("OK  Logged in as vera");

		await page.goto(`${BASE}/profile`, { waitUntil: "networkidle" });

		await page
			.getByRole("heading", { name: "Profile Details" })
			.waitFor({ state: "visible", timeout: 15000 });
		await page
			.getByRole("heading", { name: "Badges" })
			.waitFor({ state: "visible", timeout: 15000 });
		await page
			.getByRole("heading", { name: "My Engagements" })
			.waitFor({ state: "visible", timeout: 15000 });
		console.log("OK  Profile Details, Badges, and My Engagements all render on one page");

		if ((await page.getByRole("button", { name: "Activity", exact: true }).count()) > 0) {
			throw new Error("Activity tab button must not exist on the consolidated page");
		}
		if ((await page.getByRole("button", { name: /share achievements/i }).count()) > 0) {
			throw new Error("Share achievements button must be removed");
		}
		console.log("OK  No tab buttons and no Share achievements button");

		const dangerZoneText = await page
			.getByText(/permanently delete your account/i)
			.textContent();
		if (dangerZoneText?.toLowerCase().includes("keycloak")) {
			throw new Error("Danger zone description must not mention Keycloak");
		}
		console.log("OK  Danger zone description no longer mentions Keycloak");

		// --- legacy ?tab= deep links still work ---
		await page.goto(`${BASE}/my-engagements`, { waitUntil: "networkidle" });
		if (!/\/profile\?tab=engagements/.test(page.url())) {
			throw new Error(`/my-engagements should redirect to /profile?tab=engagements, got ${page.url()}`);
		}
		await page.getByRole("heading", { name: "My Engagements" }).waitFor({ state: "visible", timeout: 10000 });
		console.log("OK  /my-engagements redirects to /profile?tab=engagements and renders");

		await page.goto(`${BASE}/achievements`, { waitUntil: "networkidle" });
		if (!/\/profile\?tab=achievements/.test(page.url())) {
			throw new Error(`/achievements should redirect to /profile?tab=achievements, got ${page.url()}`);
		}
		await page.getByRole("heading", { name: "Badges" }).waitFor({ state: "visible", timeout: 10000 });
		console.log("OK  /achievements redirects to /profile?tab=achievements and renders");

		// --- Edit/Save/Cancel via the header quick actions ---
		await page.goto(`${BASE}/profile`, { waitUntil: "networkidle" });
		await page.getByTestId("quick-action-edit").click();
		await page.getByTestId("quick-action-save").waitFor({ state: "visible", timeout: 10000 });
		console.log("OK  Edit opens Save/Cancel via the header quick actions");

		const bioField = page.locator("#bio");
		const originalBio = await bioField.inputValue();
		const testBio = `Smoke test 794 ${Date.now()}`;
		await bioField.fill(testBio);
		await page.getByTestId("quick-action-save").click();
		await page.getByText("Profile saved.").waitFor({ state: "visible", timeout: 10000 });
		await page.getByText(testBio).waitFor({ state: "visible", timeout: 10000 });
		console.log("OK  Saving the profile via the quick-actions toolbar works");

		// Clean up: revert the bio so this script leaves no debris on shared staging.
		await page.getByTestId("quick-action-edit").click();
		await page.getByTestId("quick-action-save").waitFor({ state: "visible", timeout: 10000 });
		await bioField.fill(originalBio);
		await page.getByTestId("quick-action-save").click();
		await page.getByText("Profile saved.").waitFor({ state: "visible", timeout: 10000 });
		console.log("OK  Reverted bio, cleaning up after the run");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
