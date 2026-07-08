// Smoke test for #641: the organization dashboard's outer wrapper applied
// `max-w-2xl` (with no `mx-auto`) to the header, tab bar, AND tab content
// together whenever a non-calendar tab was active. That shrank and
// left-hugged the tab bar itself, so the Calendar tab rendered full width
// while Engagements/Members/Settings rendered narrow and left-aligned, with
// a visible layout jump when switching tabs. The fix moves the width
// constraint to only wrap the per-tab content, centering it with `mx-auto`,
// leaving the header/tab bar full width and consistently positioned across
// all tabs.
//
// Verifies the tab bar's bounding box (position + width) is identical on
// the Calendar tab and the Settings tab.
//
// No throwaway data is created (an existing org from seed data is used), so
// there is nothing to clean up (see #630).
// Run: node scripts/smoke-test-641-org-tabs-alignment.mjs

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
		await loginKeycloak(page, "olaf", "olaf123");

		await page.goto(BASE, { waitUntil: "networkidle" });

		const switcherBtn = page.getByRole("button", { name: "Switch organization" });
		if ((await switcherBtn.count()) === 0) {
			throw new Error("No org switcher found - olaf has no organizations in seed data");
		}
		await switcherBtn.first().click();

		const dashboardLink = page.getByTestId("org-dashboard-link");
		if ((await dashboardLink.count()) === 0) {
			throw new Error("No org-dashboard-link found in switcher dropdown");
		}
		await dashboardLink.first().click();

		await page.waitForURL(/\/organizations\/.+\/dashboard/, { timeout: 15000 });
		console.log("OK  Navigated to org dashboard");

		const tabBar = page.locator("nav").filter({ hasText: "Settings" });
		await tabBar.waitFor({ state: "visible", timeout: 15000 });

		const calendarBox = await tabBar.boundingBox();
		if (!calendarBox) throw new Error("Could not measure tab bar bounding box on Calendar tab");
		console.log(
			`OK  Calendar tab bar box: x=${calendarBox.x}, width=${calendarBox.width}`,
		);

		await page.getByRole("button", { name: "Settings" }).click();
		await page.getByRole("button", { name: "Edit" }).waitFor({ timeout: 10000 });

		const settingsBox = await tabBar.boundingBox();
		if (!settingsBox) throw new Error("Could not measure tab bar bounding box on Settings tab");
		console.log(
			`OK  Settings tab bar box: x=${settingsBox.x}, width=${settingsBox.width}`,
		);

		if (Math.abs(settingsBox.x - calendarBox.x) > 1) {
			throw new Error(
				`Tab bar shifted horizontally between tabs: calendar x=${calendarBox.x}, settings x=${settingsBox.x}`,
			);
		}
		if (Math.abs(settingsBox.width - calendarBox.width) > 1) {
			throw new Error(
				`Tab bar width changed between tabs: calendar width=${calendarBox.width}, settings width=${settingsBox.width}`,
			);
		}
		console.log("OK  Tab bar position and width are identical across Calendar and Settings tabs");
	} finally {
		await browser.close();
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
