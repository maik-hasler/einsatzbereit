/**
 * Smoke test for RC.134 - "Near me" geolocation button in location filter (#372).
 *
 * Verifies:
 * 1. Health endpoint returns 200
 * 2. Homepage loads with the location filter dropdown
 * 3. Opening the Location filter reveals the "Use my location" button
 * 4. The button has an aria-label for accessibility
 * 5. Clicking with a mocked geolocation sets lat/lng URL params
 */
import { chromium } from "playwright";

const API = "https://api.maik-hasler.de";
const FRONTEND = "https://einsatzbereit.maik-hasler.de";

let passed = 0;
let failed = 0;

function pass(msg) {
	console.log(`  PASS  ${msg}`);
	passed++;
}
function fail(msg) {
	console.error(`  FAIL  ${msg}`);
	failed++;
}

// 1. Health check
const healthRes = await fetch(`${API}/health`);
if (healthRes.ok) {
	pass(`Health endpoint returned ${healthRes.status}`);
} else {
	fail(`Health endpoint returned ${healthRes.status}`);
}

// 2. Browser checks - grant geolocation with Munich coordinates
const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({
	ignoreHTTPSErrors: true,
	geolocation: { latitude: 48.1351, longitude: 11.582 },
	permissions: ["geolocation"],
});
const page = await ctx.newPage();

try {
	await page.goto(FRONTEND, { waitUntil: "networkidle", timeout: 30000 });

	// Find the Location filter button using loose text match (same as debug script)
	const locationFilterBtn = page
		.getByRole("button")
		.filter({ hasText: /location/i })
		.first();
	const isVisible = await locationFilterBtn.isVisible({ timeout: 5000 });
	if (isVisible) {
		pass("Location filter button is visible");
	} else {
		fail("Location filter button not found");
		await browser.close();
		process.exit(1);
	}

	// Open the dropdown and wait for it to render
	await locationFilterBtn.click();
	await page.waitForTimeout(600);

	// Verify the Near me button exists in the open dropdown
	// aria-label is "Use my current location to filter opportunities"
	const nearMeBtn = page.getByRole("button", {
		name: /use my current location/i,
	});
	const nearMeVisible = await nearMeBtn.isVisible({ timeout: 5000 });
	if (nearMeVisible) {
		pass('"Use my location" button is visible in location filter');
	} else {
		fail('"Use my location" button not found in location filter dropdown');
		await page.screenshot({
			path: "/home/user/einsatzbereit/scripts/debug-nearme-fail.png",
		});
		await browser.close();
		process.exit(1);
	}

	// Check aria-label
	const ariaLabel = await nearMeBtn.getAttribute("aria-label");
	if (ariaLabel && ariaLabel.length > 0) {
		pass(`Button has aria-label: "${ariaLabel}"`);
	} else {
		fail("Button is missing aria-label");
	}

	// Click - geolocation is mocked with Munich coordinates
	await nearMeBtn.click();

	// Wait for URL to update with lat/lng params (geolocation callback sets them)
	try {
		await page.waitForURL((url) => url.searchParams.has("lat"), { timeout: 8000 });
		const url = new URL(page.url());
		pass(
			`URL params set: lat=${url.searchParams.get("lat")}, lng=${url.searchParams.get("lng")}, radius=${url.searchParams.get("radius")}`,
		);
	} catch {
		fail("lat/lng not set in URL params after geolocation click (timed out 8s)");
	}
} catch (err) {
	fail(`Unexpected error: ${err.message}`);
} finally {
	await browser.close();
}

console.log(`\n${passed} passed, ${failed} failed`);
if (failed > 0) process.exit(1);
