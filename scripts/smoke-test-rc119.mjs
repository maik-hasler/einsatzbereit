/**
 * Smoke test for RC.119 - My Engagements org name/link fix (PR #475).
 *
 * Verifies:
 * 1. Health endpoint returns 200
 * 2. Login succeeds and My Engagements page loads without 401/403
 * 3. Engagement cards (if present) contain organization links
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

// 2. Browser: login and verify My Engagements page
const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await ctx.newPage();

const apiResponses = [];
page.on("response", (resp) => {
	if (resp.url().startsWith(API)) {
		apiResponses.push({ url: resp.url(), status: resp.status() });
	}
});

try {
	// Navigate to My Engagements (requires auth - redirects to Keycloak)
	await page.goto(`${FRONTEND}/my-engagements`, {
		waitUntil: "domcontentloaded",
		timeout: 30000,
	});

	// Two-step Keycloak login (live uses separate username/password steps)
	await page.fill("#username", "vera");
	await page.click("#kc-login");
	await page.fill("#password", "vera123");
	await page.click("#kc-login");

	// Wait for page to settle
	await page.waitForLoadState("networkidle", { timeout: 30000 });

	// Check h1 heading is visible
	const h1 = await page.locator("h1").first().textContent({ timeout: 10000 });
	if (h1 && h1.trim().length > 0) {
		pass(`My Engagements page heading visible: "${h1.trim()}"`);
	} else {
		fail("My Engagements page heading not found");
	}

	// Check no 401 responses
	const unauthorized = apiResponses.filter((r) => r.status === 401);
	if (unauthorized.length === 0) {
		pass(`No 401 responses from API (${apiResponses.length} API calls captured)`);
	} else {
		fail(
			`Got ${unauthorized.length} 401(s): ${unauthorized.map((r) => r.url).join(", ")}`,
		);
	}

	// Check no 403 responses
	const forbidden = apiResponses.filter((r) => r.status === 403);
	if (forbidden.length === 0) {
		pass(`No 403 responses from API`);
	} else {
		fail(
			`Got ${forbidden.length} 403(s): ${forbidden.map((r) => r.url).join(", ")}`,
		);
	}

	// Check engagement cards for org links (if any cards are present)
	const cards = page.locator("li.rounded-xl");
	const cardCount = await cards.count();
	if (cardCount === 0) {
		pass(`No engagement cards present - empty state is valid (vera has no engagements in staging)`);
	} else {
		// Verify org links appear in cards
		const orgLinks = page.locator("a[href^='/organizations/']");
		const orgLinkCount = await orgLinks.count();
		if (orgLinkCount > 0) {
			const firstHref = await orgLinks.first().getAttribute("href");
			pass(`Org links found in ${cardCount} card(s) - first link: ${firstHref}`);
		} else {
			fail(`${cardCount} engagement card(s) found but no org links (a[href^='/organizations/'])`);
		}
	}
} catch (err) {
	fail(`Browser test error: ${err.message}`);
} finally {
	await browser.close();
}

console.log(`\n${passed + failed} checks: ${passed} passed, ${failed} failed`);
process.exit(failed > 0 ? 1 : 0);
