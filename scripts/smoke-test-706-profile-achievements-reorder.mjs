// Smoke test for #706: achievements on the Profile tab used to render last,
// below the "Create organization" and "Danger zone" account-action cards, in
// a separate full-width block outside the profile column's narrow wrapper.
// The fix reorders achievements to render directly under the identity/profile
// content, above both account-action cards, inside a single responsive grid.
//
// Verifies the "Badges" heading's bounding-box Y position is above both the
// "Organizations" and "Danger zone" headings on the live /profile page.
//
// No throwaway data is created (an existing seed user is used), so there is
// nothing to clean up (see #630).
// Run: node scripts/smoke-test-706-profile-achievements-reorder.mjs

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const { browser, page } = await launchLiveBrowser();
	try {
		await page.goto(BASE, { waitUntil: "networkidle" });
		await page.click("text=/sign in|anmelden/i");
		await page.waitForURL(/\/realms\//, { timeout: 30000 });
		await loginKeycloak(page, "vera", "vera123");
		console.log("OK  Logged in as vera");

		await page.goto(`${BASE}/profile`, { waitUntil: "networkidle" });

		const badgesHeading = page.getByRole("heading", { name: "Badges" });
		const organizationsHeading = page.getByRole("heading", { name: "Organizations" });
		const dangerZoneHeading = page.getByRole("heading", { name: "Danger zone" });

		await badgesHeading.waitFor({ state: "visible", timeout: 20000 });
		await organizationsHeading.waitFor({ state: "visible", timeout: 5000 });
		await dangerZoneHeading.waitFor({ state: "visible", timeout: 5000 });
		console.log("OK  Badges, Organizations, and Danger zone headings all visible on /profile");

		const badgesBox = await badgesHeading.boundingBox();
		const organizationsBox = await organizationsHeading.boundingBox();
		const dangerZoneBox = await dangerZoneHeading.boundingBox();
		if (!badgesBox || !organizationsBox || !dangerZoneBox) {
			throw new Error("Could not measure bounding box of one of the headings");
		}

		if (badgesBox.y >= organizationsBox.y) {
			throw new Error(
				`Achievements (y=${badgesBox.y}) do not render above the "Organizations" card (y=${organizationsBox.y})`,
			);
		}
		if (badgesBox.y >= dangerZoneBox.y) {
			throw new Error(
				`Achievements (y=${badgesBox.y}) do not render above the "Danger zone" card (y=${dangerZoneBox.y})`,
			);
		}
		console.log(
			`OK  Achievements render above both account-action cards (badges y=${badgesBox.y}, organizations y=${organizationsBox.y}, danger zone y=${dangerZoneBox.y})`,
		);
	} finally {
		await browser.close();
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
