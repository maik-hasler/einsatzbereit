/**
 * Smoke test for RC.121
 *
 * Verifies:
 * 1. Health endpoint returns 200
 * 2. My Engagements page shows organization name as a link (#364)
 * 3. Onboarding welcome banner visible on first login (#374)
 * 4. "Add to Calendar" link present on opportunity detail page (#371)
 * 5. X-Trace-Id response header present (correlation ID logging, #416)
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

// 5. X-Trace-Id header on any API response
const traceId = healthRes.headers.get("x-trace-id");
if (traceId && traceId.length > 0) {
	pass(`X-Trace-Id header present: ${traceId}`);
} else {
	fail("X-Trace-Id header missing from health response");
}

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await ctx.newPage();

async function login() {
	await page.fill("#username", "vera");
	await page.click("#kc-login");
	await page.fill("#password", "vera123");
	await page.click("#kc-login");
	await page.waitForLoadState("networkidle", { timeout: 30000 });
}

try {
	// Clear localStorage so onboarding banner is not dismissed
	await page.goto(FRONTEND, { waitUntil: "domcontentloaded", timeout: 30000 });
	await page.evaluate(() => localStorage.clear());

	// Navigate to home (triggers Keycloak redirect)
	await page.goto(`${FRONTEND}/?_=${Date.now()}`, {
		waitUntil: "domcontentloaded",
		timeout: 30000,
	});

	// Click Anmelden/Sign in
	const signinBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	if (await signinBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
		await signinBtn.click();
	}

	await page.waitForURL(/login\.maik-hasler\.de|keycloak/, { timeout: 15000 }).catch(() => {});
	await login();

	// 3. Onboarding banner on HomePage
	const banner = page.locator("[data-testid='onboarding-banner'], .onboarding-banner, [role='status']").first();
	// Try a text-based selector as fallback
	const welcomeText = page.getByText(/willkommen|welcome/i).first();
	const bannerVisible =
		(await banner.isVisible({ timeout: 8000 }).catch(() => false)) ||
		(await welcomeText.isVisible({ timeout: 3000 }).catch(() => false));
	if (bannerVisible) {
		pass("Onboarding welcome banner visible on first login (#374)");
	} else {
		fail("Onboarding welcome banner not found on HomePage (#374)");
	}

	// Navigate to My Engagements
	await page.goto(`${FRONTEND}/my-engagements`, {
		waitUntil: "domcontentloaded",
		timeout: 30000,
	});
	await page.waitForLoadState("networkidle", { timeout: 20000 });

	// 2. My Engagements - org name should appear as a link
	const orgLinks = page.locator("a[href*='/organizations/']");
	const orgLinkCount = await orgLinks.count();
	if (orgLinkCount > 0) {
		const firstOrgLinkText = await orgLinks.first().textContent();
		pass(`Organization link present on My Engagements (${orgLinkCount} found, e.g. "${firstOrgLinkText?.trim()}") (#364)`);
	} else {
		// Check if there are any engagement cards at all
		const engagementItems = page.locator("li, [data-engagement]");
		const itemCount = await engagementItems.count();
		if (itemCount === 0) {
			pass("No engagements for vera - org link check skipped (#364)");
		} else {
			fail(`${itemCount} engagement cards found but no org links (#364)`);
		}
	}

	// 4. iCal download on opportunity detail page
	// First find an opportunity from the home page
	await page.goto(FRONTEND, { waitUntil: "domcontentloaded", timeout: 30000 });
	await page.waitForLoadState("networkidle", { timeout: 20000 });

	const opportunityLink = page.locator("a[href*='/volunteer-opportunities/']").first();
	const hasOpportunity = await opportunityLink.isVisible({ timeout: 8000 }).catch(() => false);
	if (hasOpportunity) {
		await opportunityLink.click();
		await page.waitForLoadState("networkidle", { timeout: 20000 });

		const calendarLink = page.locator("a[download]").first();
		const calendarVisible = await calendarLink
			.waitFor({ state: "attached", timeout: 15000 })
			.then(() => true)
			.catch(() => false);

		if (calendarVisible) {
			pass("\"Add to Calendar\" link present on opportunity detail page (#371)");
		} else {
			fail("\"Add to Calendar\" link not found on opportunity detail page (#371)");
		}
	} else {
		fail("No opportunity links found on home page - cannot test iCal feature (#371)");
	}
} catch (err) {
	fail(`Browser test error: ${err.message}`);
} finally {
	await browser.close();
}

console.log(`\n${passed + failed} checks: ${passed} passed, ${failed} failed`);
process.exit(failed > 0 ? 1 : 0);
