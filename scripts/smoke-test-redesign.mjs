/**
 * Smoke test for the sub-pages redesign.
 * Verifies that key pages render a plain h1 page title
 * instead of the old PageHero (brand-800 banner) pattern.
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

async function assertH1(page, label, expectedText) {
	const h1 = page.locator("h1").first();
	const count = await h1.count();
	if (count === 0) throw new Error(`${label}: no h1 found`);
	if (expectedText) {
		const text = await h1.textContent();
		if (!text?.toLowerCase().includes(expectedText.toLowerCase())) {
			throw new Error(
				`${label}: h1 text "${text}" does not contain "${expectedText}"`,
			);
		}
	}
	console.log(`OK  ${label}: h1 rendered`);
}

async function assertNoPageHero(page, label) {
	const hero = page.locator("section.bg-brand-800").first();
	const count = await hero.count();
	if (count > 0)
		throw new Error(`${label}: PageHero (bg-brand-800) still present`);
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
		await page.goto(`${BASE}/my-engagements`, { waitUntil: "networkidle" });
		await assertH1(page, "MyEngagementsPage");
		await assertNoPageHero(page, "MyEngagementsPage");

		// --- Achievements ---
		await page.goto(`${BASE}/achievements`, { waitUntil: "networkidle" });
		await assertH1(page, "AchievementsPage");
		await assertNoPageHero(page, "AchievementsPage");

		// --- Profile ---
		await page.goto(`${BASE}/profile`, { waitUntil: "networkidle" });
		await assertH1(page, "ProfilePage");
		await assertNoPageHero(page, "ProfilePage");

		// --- Find an organization via the org switcher cookie (active-org) ---
		let orgId = null;
		const cookies = await ctx.cookies();
		const activeOrgCookie = cookies.find((c) => c.name === "active-org");
		if (activeOrgCookie) {
			orgId = activeOrgCookie.value;
			console.log(`OK  Found active-org cookie: ${orgId}`);
		} else {
			// Extract bearer token from oidc storage and call the API
			const token = await page.evaluate(() => {
				for (let i = 0; i < localStorage.length; i++) {
					const key = localStorage.key(i);
					if (!key) continue;
					try {
						const val = JSON.parse(localStorage.getItem(key) ?? "");
						if (val?.access_token) return val.access_token;
					} catch {
						// ignore
					}
				}
				return null;
			});
			if (token) {
				const orgsRes = await fetch(`${API}/v1/organizations`, {
					headers: { Authorization: `Bearer ${token}` },
					signal: AbortSignal.timeout(10000),
				});
				if (orgsRes.ok) {
					const orgsData = await orgsRes.json();
					orgId = orgsData?.items?.[0]?.id ?? orgsData?.[0]?.id ?? null;
					if (orgId) console.log(`OK  Found org via API: ${orgId}`);
				}
			}
		}

		if (orgId) {
			// --- Organization Profile ---
			await page.goto(`${BASE}/organizations/${orgId}`, {
				waitUntil: "networkidle",
			});
			await assertH1(page, "OrganizationProfilePage");
			await assertNoPageHero(page, "OrganizationProfilePage");

			// --- Organization Settings (olaf is organisator) ---
			await page.goto(`${BASE}/organizations/${orgId}/settings`, {
				waitUntil: "networkidle",
			});
			await assertH1(page, "OrganizationSettingsPage");
			await assertNoPageHero(page, "OrganizationSettingsPage");

			// --- Organization Dashboard ---
			await page.goto(`${BASE}/organizations/${orgId}/dashboard`, {
				waitUntil: "networkidle",
			});
			await assertH1(page, "OrganizationDashboardPage");
			await assertNoPageHero(page, "OrganizationDashboardPage");
		} else {
			console.log("WARN  No org ID found - skipping org page checks");
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
				await assertH1(page, "EngagementManagementPage");
				await assertNoPageHero(page, "EngagementManagementPage");
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
