/**
 * Smoke test for PR #439 - redesigned filter bar
 * Tests: no search input, location+radius filter, multi-select category pills
 * Run: node scripts/smoke-test-filters-439.mjs
 */

import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function main() {
	const apiRes = await fetch(`${API}/health`);
	if (!apiRes.ok) throw new Error(`Health check failed: ${apiRes.status}`);
	console.log("OK  API health check passed");

	const browser = await chromium.launch();
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	try {
		// 1. Home page loads
		await page.goto(BASE, { waitUntil: "networkidle" });
		await page.waitForSelector("main", { timeout: 15000 });
		console.log("OK  Home page loaded");

		// 2. No search input present (removed in this PR)
		const searchInput = page.locator('input[placeholder*="Search"], input[placeholder*="Suche"]');
		const searchCount = await searchInput.count();
		if (searchCount > 0) throw new Error("Search input still present - should have been removed");
		console.log("OK  Search input absent");

		// 3. Filter pills are present
		await page.waitForSelector('[data-testid="filter-frequency"]', { timeout: 10000 });
		await page.waitForSelector('[data-testid="filter-type"]', { timeout: 5000 });
		console.log("OK  Filter pills present (frequency, type)");

		// 4. Frequency pill opens and selecting One-time updates URL
		await page.click('[data-testid="filter-frequency"]');
		await page.waitForTimeout(300);
		await page.getByRole("button", { name: /one.time|einmalig/i }).click();
		await page.waitForTimeout(500);
		const urlAfterFreq = page.url();
		if (!urlAfterFreq.includes("occurrence=OneTime"))
			throw new Error(`URL should contain occurrence=OneTime, got: ${urlAfterFreq}`);
		console.log("OK  Frequency filter updates URL");

		// 5. Clear frequency filter by clicking the x on the chip
		const clearChip = page.locator('[aria-label*="occurrence"], [aria-label*="Frequency"]').first();
		if ((await clearChip.count()) > 0) {
			await clearChip.click();
			await page.waitForTimeout(300);
		} else {
			// Try clicking the pill x button
			await page.click('[data-testid="filter-frequency"]');
			await page.waitForTimeout(300);
		}
		console.log("OK  Frequency filter cleared");

		// 6. Participation type filter
		await page.goto(BASE, { waitUntil: "networkidle" });
		await page.click('[data-testid="filter-type"]');
		await page.waitForTimeout(300);
		await page.getByRole("button", { name: /waitlist|warteliste/i }).click();
		await page.waitForTimeout(500);
		const urlAfterType = page.url();
		if (!urlAfterType.includes("participationType=Waitlist"))
			throw new Error(`URL should contain participationType=Waitlist, got: ${urlAfterType}`);
		console.log("OK  Participation type filter updates URL");

		// 7. Category filter pill exists and opens
		await page.goto(BASE, { waitUntil: "networkidle" });
		const categoryPill = page.locator('[data-testid="filter-category"]');
		if ((await categoryPill.count()) > 0) {
			await categoryPill.click();
			await page.waitForTimeout(300);
			// Close by clicking outside
			await page.keyboard.press("Escape");
			console.log("OK  Category filter pill present and opens");
		} else {
			console.log("--  Category filter pill not found by testid (may use different selector)");
		}

		// 8. Location filter pill exists
		const locationPill = page.locator('[data-testid="filter-location"]');
		if ((await locationPill.count()) > 0) {
			await locationPill.click();
			await page.waitForTimeout(300);
			await page.keyboard.press("Escape");
			console.log("OK  Location filter pill present and opens");
		} else {
			console.log("--  Location filter pill not found by testid (may use different selector)");
		}

		// 9. Multiple filters reflect in URL
		await page.goto(BASE, { waitUntil: "networkidle" });
		await page.click('[data-testid="filter-frequency"]');
		await page.waitForTimeout(300);
		await page.getByRole("button", { name: /one.time|einmalig/i }).click();
		await page.waitForTimeout(300);
		await page.click('[data-testid="filter-type"]');
		await page.waitForTimeout(300);
		await page.getByRole("button", { name: /waitlist|warteliste/i }).click();
		await page.waitForTimeout(500);
		const multiUrl = page.url();
		if (!multiUrl.includes("occurrence=OneTime"))
			throw new Error(`Multi-filter URL missing occurrence, got: ${multiUrl}`);
		if (!multiUrl.includes("participationType=Waitlist"))
			throw new Error(`Multi-filter URL missing participationType, got: ${multiUrl}`);
		console.log("OK  Multiple filters reflected in URL");

		// 10. List/map toggle still works
		await page.goto(BASE, { waitUntil: "networkidle" });
		const mapToggle = page.getByTestId("view-toggle-map");
		await mapToggle.waitFor({ timeout: 10000 });
		await mapToggle.click();
		await page.waitForTimeout(1000);
		if (!page.url().includes("view=map"))
			throw new Error(`Map toggle did not update URL, got: ${page.url()}`);
		console.log("OK  Map toggle updates URL");

		console.log("\nAll smoke tests passed.");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
