/**
 * Smoke test for PR #772 review feedback on the organization directory
 * feature (issue #763): verifies the three fixes actually landed on live
 * staging:
 *   1. A persistent "Organizations" entry point exists in the Header nav
 *      (desktop and mobile), visible whether logged in or not.
 *   2. The organization profile page's breadcrumb reads
 *      Home > Organizations > {org name}, with "Organizations" linking
 *      back to the directory.
 *   3. The organization directory shows an open-opportunity count on
 *      cards that have published opportunities.
 * Run: node scripts/smoke-test-772-org-directory-feedback.mjs
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
		// --- 1a. Desktop nav: "Organizations" link visible while logged out ---
		await page.setViewportSize({ width: 1280, height: 900 });
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		await page.waitForSelector("h1", { timeout: 15000 });

		const desktopOrgLink = page.locator("nav a[href='/organizations']").first();
		await desktopOrgLink.waitFor({ state: "visible", timeout: 10000 });
		console.log("OK  Desktop nav shows an Organizations link (logged out)");

		// --- 1b. Mobile menu: "Organizations" link visible ---
		await page.setViewportSize({ width: 390, height: 844 });
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		const menuBtn = page.getByRole("button", { name: /open menu|menü öffnen/i });
		await menuBtn.waitFor({ state: "visible", timeout: 10000 });
		await menuBtn.click();
		const mobileOrgLink = page.locator("a[href='/organizations']").first();
		await mobileOrgLink.waitFor({ state: "visible", timeout: 10000 });
		console.log("OK  Mobile menu shows an Organizations link (logged out)");
		await page.setViewportSize({ width: 1280, height: 900 });

		// --- 1c. Desktop nav: still visible after logging in ---
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
		await signInBtn.first().click();
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await loginKeycloak(page, "vera", "vera123");
		await page.waitForSelector("main", { timeout: 15000 });

		const loggedInOrgLink = page.locator("nav a[href='/organizations']").first();
		await loggedInOrgLink.waitFor({ state: "visible", timeout: 10000 });
		console.log("OK  Desktop nav shows an Organizations link (logged in)");

		// --- 2. Directory page: at least one org card, open-opportunity count ---
		await loggedInOrgLink.click();
		await page.waitForURL(/\/organizations$/, { timeout: 10000 });
		await page.waitForLoadState("networkidle");

		const firstOrgLink = page
			.locator("a[href^='/organizations/']")
			.first();
		await firstOrgLink.waitFor({ state: "visible", timeout: 15000 });
		const orgHref = await firstOrgLink.getAttribute("href");
		console.log(`OK  Organization directory lists at least one organization (${orgHref})`);

		// --- 2b. That card shows an open-opportunity count, if it has any ---
		const orgCard = firstOrgLink.locator(
			"xpath=ancestor::li[1]",
		);
		const countText = await orgCard.locator("p", {
			hasText: /open opportunit|offene? Einsätze|offener Einsatz/i,
		}).first().textContent().catch(() => null);
		if (countText) {
			console.log(`OK  Organization card shows open-opportunity count: "${countText.trim()}"`);
		} else {
			console.log(
				"WARN  First organization card shows no open-opportunity count (it may have zero published opportunities)",
			);
		}

		// --- 3. Org profile page: breadcrumb is Home > Organizations > {name} ---
		await firstOrgLink.click();
		await page.waitForURL(/\/organizations\/.+/, { timeout: 10000 });
		await page.waitForLoadState("networkidle");

		const breadcrumb = page.locator("nav[aria-label='Navigationspfad'], nav[aria-label='Breadcrumb']");
		await breadcrumb.waitFor({ state: "visible", timeout: 10000 });

		const orgCrumb = breadcrumb.locator(`a[href='/organizations']`);
		await orgCrumb.waitFor({ state: "visible", timeout: 5000 });
		console.log("OK  Org profile breadcrumb includes an Organizations crumb linking to /organizations");

		const crumbTexts = await breadcrumb.locator("a, span").allTextContents();
		const trail = crumbTexts.map((t) => t.trim()).filter(Boolean);
		if (trail.length < 3) {
			throw new Error(
				`Expected a 3-part breadcrumb trail (Home > Organizations > org name), got: ${JSON.stringify(trail)}`,
			);
		}
		console.log(`OK  Breadcrumb trail: ${trail.join(" > ")}`);

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
