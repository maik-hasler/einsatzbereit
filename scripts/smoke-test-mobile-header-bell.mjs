/**
 * Smoke test for mobile header bell positioning fix (#497 / PR #499).
 *
 * The notification bell was appearing in the center of the mobile header
 * row because justify-between spread all three flex children (logo, bell,
 * burger) across the full width. The fix wraps bell + burger in a single
 * flex container so they stay grouped flush-right.
 *
 * Verifies on a 375-wide mobile viewport (logged in as vera):
 * 1. Health endpoint returns 200
 * 2. Frontend loads
 * 3. Login works
 * 4. Notification bell button is visible on mobile
 * 5. Bell and burger buttons are adjacent (right-side group): the bell's
 *    right edge is within 60px of the burger's left edge (no gap in the
 *    middle of the header)
 * 6. Both buttons are in the right half of the viewport (x-center > 50%)
 */
import { chromium } from "playwright";

const API = "https://api.maik-hasler.de";
const FRONTEND = "https://einsatzbereit.maik-hasler.de";
const MOBILE_WIDTH = 375;
const MOBILE_HEIGHT = 812;

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
{
	const res = await fetch(`${API}/health`);
	if (res.ok) {
		pass(`Health endpoint returned ${res.status}`);
	} else {
		fail(`Health endpoint returned ${res.status}`);
		process.exit(1);
	}
}

// 2-6. Browser checks on mobile viewport
const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({
	ignoreHTTPSErrors: true,
	viewport: { width: MOBILE_WIDTH, height: MOBILE_HEIGHT },
	userAgent:
		"Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1",
});
const page = await ctx.newPage();

try {
	// 2. Frontend loads
	await page.goto(FRONTEND, { waitUntil: "networkidle", timeout: 30000 });
	const title = await page.title();
	if (title && title.length > 0) {
		pass(`Frontend loaded (title: "${title}")`);
	} else {
		fail("Frontend page title missing");
	}

	// 3. Login as vera - on mobile the sign-in link is inside the hamburger menu
	const burger = page.getByRole("button", { name: /open menu|menu öffnen/i }).first();
	const burgerVisibleBefore = await burger.isVisible({ timeout: 5000 }).catch(() => false);
	if (burgerVisibleBefore) {
		await burger.click({ timeout: 5000 });
		await page.waitForTimeout(500);
	}
	const signinBtn = page.getByRole("button", { name: /sign in|anmelden/i }).first();
	await signinBtn.click({ timeout: 10000 });
	await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
	await page.fill("#username", "vera");
	await page.getByRole("button", { name: /sign in|anmelden/i }).click();
	await page.fill("#password", "vera123");
	await page.getByRole("button", { name: /sign in|anmelden/i }).click();
	await page.waitForURL(`${FRONTEND}/**`, { timeout: 15000 });
	await page.waitForTimeout(1500);
	pass("Logged in as vera");

	// 4. Notification bell button is visible on mobile
	const bell = page.getByTestId("notification-bell-mobile");
	const bellVisible = await bell.isVisible({ timeout: 5000 }).catch(() => false);
	if (bellVisible) {
		pass("notification-bell-mobile is visible on mobile viewport");
	} else {
		fail("notification-bell-mobile not visible on mobile viewport");
	}

	if (bellVisible) {
		// 5. Bell and burger are adjacent (no gap in the middle of header)
		const bellBox = await bell.boundingBox();
		// The burger button is the last button in the header (no data-testid, use aria-label)
		const burger = page.getByRole("button", { name: /open menu|menu öffnen/i }).first();
		const burgerVisible = await burger.isVisible({ timeout: 3000 }).catch(() => false);

		if (burgerVisible && bellBox) {
			const burgerBox = await burger.boundingBox();

			if (burgerBox) {
				const bellRight = bellBox.x + bellBox.width;
				const burgerLeft = burgerBox.x;
				const gap = burgerLeft - bellRight;

				console.log(
					`  Bell: x=${Math.round(bellBox.x)}, right=${Math.round(bellRight)}; Burger: x=${Math.round(burgerLeft)}; gap=${Math.round(gap)}px`,
				);

				if (gap >= 0 && gap <= 60) {
					pass(`Bell and burger are adjacent (gap=${Math.round(gap)}px <= 60px)`);
				} else if (gap < 0) {
					// overlapping is fine too
					pass(`Bell and burger are adjacent/overlapping (gap=${Math.round(gap)}px)`);
				} else {
					fail(
						`Bell and burger have a gap of ${Math.round(gap)}px - they may not be grouped (regression risk)`,
					);
				}

				// 6. Both buttons are in the right half of the viewport
				const bellCenter = bellBox.x + bellBox.width / 2;
				const burgerCenter = burgerBox.x + burgerBox.width / 2;
				const midpoint = MOBILE_WIDTH / 2;

				if (bellCenter > midpoint) {
					pass(
						`Bell center (${Math.round(bellCenter)}px) is in the right half of viewport (midpoint=${midpoint}px)`,
					);
				} else {
					fail(
						`Bell center (${Math.round(bellCenter)}px) is NOT in the right half - bell may be in center of header`,
					);
				}

				if (burgerCenter > midpoint) {
					pass(
						`Burger center (${Math.round(burgerCenter)}px) is in the right half of viewport`,
					);
				} else {
					fail(
						`Burger center (${Math.round(burgerCenter)}px) is NOT in the right half`,
					);
				}
			} else {
				fail("Could not get burger bounding box");
			}
		} else {
			if (!burgerVisible) {
				fail("Burger button not visible - cannot check positioning");
			}
			if (!bellBox) {
				fail("Could not get bell bounding box");
			}
		}
	}
} catch (err) {
	fail(`Unexpected error: ${err.message}`);
	console.error(err);
} finally {
	await browser.close();
}

console.log(`\n${passed} passed, ${failed} failed`);
if (failed > 0) process.exit(1);
