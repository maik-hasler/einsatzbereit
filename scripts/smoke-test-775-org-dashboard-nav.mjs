/**
 * Smoke test for issue #775: users with >=1 organization had no way to reach
 * the org dashboard from the mobile burger menu. Adds an "Organization
 * Dashboard" entry there, gated on org count, resolved via the same
 * active-org-cookie-then-alphabetical logic HomePage already uses.
 *
 * Verifies against live staging (mobile viewport):
 *  - olaf (organizes an org) sees "Organization Dashboard" in the mobile
 *    menu and it navigates to /app/:id/dashboard
 *  - admin (no organizations) does not see the entry, but still sees
 *    "Administration"
 *
 * Run: node scripts/smoke-test-775-org-dashboard-nav.mjs
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

async function openMobileMenu(page) {
	const burger = page.getByRole("button", { name: /open menu|men. öffnen/i });
	await burger.waitFor({ state: "visible", timeout: 10000 });
	await burger.click();
	await page.waitForTimeout(300);
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const { browser, page } = await launchLiveBrowser();
	await page.setViewportSize({ width: 375, height: 812 });
	try {
		// --- olaf: organizes an org, sees the entry and it navigates correctly ---
		await signIn(page, "olaf", "olaf123");
		console.log("OK  Logged in as olaf");

		await openMobileMenu(page);
		const orgLink = page.getByRole("link", { name: "Organization Dashboard" });
		await orgLink.waitFor({ state: "visible", timeout: 10000 });
		console.log("OK  olaf sees the Organization Dashboard nav entry");

		await orgLink.click();
		await page.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });
		console.log("OK  Organization Dashboard entry navigates to /app/:id/dashboard");

		await page
			.getByRole("button", { name: /sign out|abmelden/i })
			.click()
			.catch(async () => {
				await openMobileMenu(page);
				await page.getByRole("button", { name: /sign out|abmelden/i }).click();
			});
		await page.waitForURL(`${BASE}/`, { timeout: 15000 });
		console.log("OK  Signed out olaf");

		// --- admin: no organizations, no entry, Administration still there ---
		await signIn(page, "admin", "admin123");
		console.log("OK  Logged in as admin");

		await openMobileMenu(page);
		await page
			.getByRole("link", { name: "Administration" })
			.waitFor({ state: "visible", timeout: 10000 });
		const adminOrgLink = page.getByRole("link", { name: "Organization Dashboard" });
		if ((await adminOrgLink.count()) > 0) {
			throw new Error("admin (no orgs) must not see the Organization Dashboard nav entry");
		}
		console.log("OK  admin (no orgs) does not see the Organization Dashboard nav entry");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
