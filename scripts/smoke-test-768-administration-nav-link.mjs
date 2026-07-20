/**
 * Smoke test for the PR #768 review feedback: admins had no way to reach
 * /administration except by typing the URL directly. Adds an "Administration"
 * entry to the account dropdown (desktop) and mobile menu, gated to the admin
 * role.
 *
 * Verifies against live staging:
 *  - admin sees "Administration" in the account dropdown and it navigates to
 *    /administration
 *  - a non-admin (vera) does not see the "Administration" entry, but still
 *    sees "My Profile"
 *
 * Run: node scripts/smoke-test-768-administration-nav-link.mjs
 */

import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function signIn(page, username, password) {
	await page.goto(BASE, { waitUntil: "networkidle" });
	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	await signInBtn.first().click();
	await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
	await page.fill("#username", username);
	await page.click("#kc-login");
	await page.fill("#password", password);
	await page.click("#kc-login");
	await page.waitForURL(`${BASE}/`, { timeout: 15000 });
	await page
		.getByRole("button", { name: /user menu|benutzermen/i })
		.waitFor({ state: "visible", timeout: 15000 });
}

async function openAccountDropdown(page) {
	const menuBtn = page.getByRole("button", { name: /user menu|benutzermen/i });
	await menuBtn.waitFor({ state: "visible", timeout: 10000 });
	await menuBtn.click();
	// Dropdown mount is instant but not synchronous with the click event settling.
	await page.waitForTimeout(300);
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const browser = await chromium.launch();
	const context = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await context.newPage();
	try {
		// --- Admin: sees the entry and it navigates correctly ---
		await signIn(page, "admin", "admin123");
		console.log("OK  Logged in as admin");

		await openAccountDropdown(page);
		const adminLink = page.getByRole("link", { name: "Administration" });
		await adminLink.waitFor({ state: "visible", timeout: 10000 });
		console.log("OK  Admin sees the Administration nav entry");

		await adminLink.click();
		await page.waitForURL(`${BASE}/administration`, { timeout: 10000 });
		await page
			.getByRole("heading", { name: "Administration", level: 1 })
			.waitFor({ state: "visible", timeout: 10000 });
		console.log("OK  Administration entry navigates to /administration");

		await page.getByRole("button", { name: /sign out|abmelden/i }).click().catch(async () => {
			await openAccountDropdown(page);
			await page.getByRole("button", { name: /sign out|abmelden/i }).click();
		});
		await page.waitForURL(`${BASE}/`, { timeout: 15000 });
		console.log("OK  Signed out admin");

		// --- Non-admin (vera): no Administration entry, My Profile still there ---
		await signIn(page, "vera", "vera123");
		console.log("OK  Logged in as vera");

		await openAccountDropdown(page);
		// vera's account locale is German - match both languages, same as the
		// sign-in/sign-out button regexes above.
		await page
			.getByRole("link", { name: /my profile|mein profil/i })
			.waitFor({ state: "visible", timeout: 10000 });
		const veraAdminLink = page.getByRole("link", { name: "Administration" });
		if ((await veraAdminLink.count()) > 0) {
			throw new Error("Non-admin (vera) must not see the Administration nav entry");
		}
		console.log("OK  Non-admin (vera) does not see the Administration nav entry");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
