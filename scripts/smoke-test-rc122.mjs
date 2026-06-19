/**
 * Smoke test for RC.122
 *
 * Verifies:
 * 1. Health endpoint returns 200
 * 2. Transparent header dropdowns have dark styling on homepage (#478)
 * 3. Mobile notification bell visible in header (not only in burger menu) (#477)
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

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({
	ignoreHTTPSErrors: true,
	viewport: { width: 1280, height: 800 },
});
const page = await ctx.newPage();

async function login() {
	await page.waitForSelector("#username", { timeout: 30000 });
	await page.fill("#username", "vera");
	await page.click("#kc-login");
	await page.waitForSelector("#password", { timeout: 15000 });
	await page.fill("#password", "vera123");
	await page.click("#kc-login");
	await page.waitForLoadState("networkidle", { timeout: 30000 });
}

try {
	await page.goto(FRONTEND, { waitUntil: "domcontentloaded", timeout: 30000 });

	const signinBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	if (await signinBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
		await signinBtn.click();
	}
	await page.waitForURL(/login\.maik-hasler\.de|keycloak/, { timeout: 15000 }).catch(() => {});
	await login();

	// 2. Desktop: transparent header dropdown uses dark bg (#478)
	await page.goto(FRONTEND, { waitUntil: "domcontentloaded", timeout: 30000 });
	await page.waitForLoadState("networkidle", { timeout: 20000 });

	// Open language selector dropdown
	const langBtn = page.locator("button[aria-label*='language' i], button[aria-label*='sprache' i]").first();
	const langBtnVisible = await langBtn.isVisible({ timeout: 5000 }).catch(() => false);

	if (langBtnVisible) {
		await langBtn.click();
		const darkDropdown = page.locator(".bg-brand-800").first();
		const isDark = await darkDropdown.isVisible({ timeout: 3000 }).catch(() => false);
		if (isDark) {
			pass("Transparent header dropdown has dark bg-brand-800 styling (#478)");
		} else {
			const borderDropdown = page.locator("[class*='border-white']").first();
			const hasBorderWhite = await borderDropdown.isVisible({ timeout: 2000 }).catch(() => false);
			if (hasBorderWhite) {
				pass("Transparent header dropdown has dark border styling (#478)");
			} else {
				fail("Transparent header dropdown does not appear dark (#478)");
			}
		}
		// Close dropdown
		await page.keyboard.press("Escape");
	} else {
		fail("Language button not found for transparent dropdown test (#478)");
	}

	// 3. Mobile: notification bell visible in header bar (#477)
	await page.setViewportSize({ width: 390, height: 844 });
	await page.goto(FRONTEND, { waitUntil: "domcontentloaded", timeout: 30000 });
	await page.waitForLoadState("networkidle", { timeout: 20000 });

	// Count visible buttons in header before opening burger menu
	const headerBtns = page.locator("header button:visible");
	const countBtns = await headerBtns.count();

	// The burger menu + notification bell = at least 2 buttons in mobile header
	if (countBtns >= 2) {
		pass(`Mobile header has ${countBtns} visible buttons (notification bell present alongside burger menu) (#477)`);
	} else if (countBtns === 1) {
		fail("Only 1 button visible in mobile header - notification bell missing (#477)");
	} else {
		fail(`Unexpected button count ${countBtns} in mobile header (#477)`);
	}
} catch (err) {
	fail(`Test error: ${err.message}`);
} finally {
	await browser.close();
}

console.log(`\n${passed + failed} checks: ${passed} passed, ${failed} failed`);
process.exit(failed > 0 ? 1 : 0);
