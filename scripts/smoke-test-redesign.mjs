/**
 * Smoke test for the sub-pages redesign (PR #458).
 * Verifies that key pages render the brand-800 PageHero banner
 * instead of the old plain h1 + Breadcrumb pattern.
 * Run: node scripts/smoke-test-redesign.mjs
 */

import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const KC = "https://login.maik-hasler.de";

async function login(page, username, password) {
	await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	await signInBtn.click();
	// Two-step Keycloak login
	await page.waitForURL(`${KC}/**`, { timeout: 15000 });
	await page.fill("#username", username);
	await page.click("#kc-login");
	await page.waitForSelector("#password", { timeout: 10000 });
	await page.fill("#password", password);
	await page.click("#kc-login");
	await page.waitForURL(`${BASE}/**`, { timeout: 15000 });
	await page.waitForLoadState("networkidle");
	console.log(`OK  Logged in as ${username}`);
}

async function assertPageHero(page, label) {
	// PageHero renders a section with bg-brand-800
	const hero = page.locator("section.bg-brand-800").first();
	const count = await hero.count();
	if (count === 0) throw new Error(`${label}: PageHero (bg-brand-800) not found`);
	console.log(`OK  ${label}: PageHero rendered`);
}

async function main() {
	const apiRes = await fetch(`${API}/health`, { signal: AbortSignal.timeout(10000) });
	if (!apiRes.ok) throw new Error(`API health check failed: ${apiRes.status}`);
	console.log("OK  API health check passed");

	const browser = await chromium.launch();
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	try {
		// --- Log in as olaf (has organisator role) ---
		await login(page, "olaf", "olaf123");

		// --- My Engagements ---
		await page.goto(`${BASE}/engagements`, { waitUntil: "networkidle" });
		await assertPageHero(page, "MyEngagementsPage");

		// --- Achievements ---
		await page.goto(`${BASE}/achievements`, { waitUntil: "networkidle" });
		await assertPageHero(page, "AchievementsPage");

		// --- Profile ---
		await page.goto(`${BASE}/account`, { waitUntil: "networkidle" });
		await assertPageHero(page, "ProfilePage");

		// --- Find an organization via API ---
		const orgsRes = await fetch(`${API}/v1/organizations`, {
			signal: AbortSignal.timeout(10000),
		});
		if (orgsRes.ok) {
			const orgsData = await orgsRes.json();
			const orgId = orgsData?.items?.[0]?.id ?? orgsData?.[0]?.id;
			if (orgId) {
				// --- Organization Profile ---
				await page.goto(`${BASE}/organizations/${orgId}`, {
					waitUntil: "networkidle",
				});
				await assertPageHero(page, "OrganizationProfilePage");

				// --- Organization Settings (olaf is organisator) ---
				await page.goto(`${BASE}/organizations/${orgId}/settings`, {
					waitUntil: "networkidle",
				});
				await assertPageHero(page, "OrganizationSettingsPage");

				// --- Organization Dashboard ---
				await page.goto(`${BASE}/organizations/${orgId}/dashboard`, {
					waitUntil: "networkidle",
				});
				await assertPageHero(page, "OrganizationDashboardPage");
			} else {
				console.log("WARN  No organizations found - skipping org page checks");
			}
		}

		// --- Find an opportunity with engagements ---
		const oppRes = await fetch(`${API}/v1/volunteer-opportunities?page=1&pageSize=1`, {
			signal: AbortSignal.timeout(10000),
		});
		if (oppRes.ok) {
			const oppData = await oppRes.json();
			const oppId = oppData?.items?.[0]?.id ?? oppData?.[0]?.id;
			if (oppId) {
				await page.goto(
					`${BASE}/volunteer-opportunities/${oppId}/engagements`,
					{ waitUntil: "networkidle" },
				);
				await assertPageHero(page, "EngagementManagementPage");
			}
		}

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
