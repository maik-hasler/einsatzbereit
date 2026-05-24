// Manual Playwright smoke test for the "Errungenschaften teilen" fix (issue #278).
// Runs against the live staging site - no local stack needed.
//
// Usage:
//   node scripts/smoke-test-achievements-share.mjs
//
// Prerequisites: npm install --save-dev playwright && npx playwright install chromium

import { chromium } from "playwright";

const BASE_URL = "https://einsatzbereit.maik-hasler.de";
const USERNAME = "vera";
const PASSWORD = "vera123";

let passed = 0;
let failed = 0;

function pass(msg) {
	console.log(`  PASS  ${msg}`);
	passed++;
}

function fail(msg, err) {
	console.error(`  FAIL  ${msg}${err ? ` - ${err}` : ""}`);
	failed++;
}

async function assert(label, fn) {
	try {
		await fn();
		pass(label);
	} catch (err) {
		fail(label, err?.message ?? String(err));
	}
}

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await context.newPage();

console.log(`\nSmoke test: Errungenschaften teilen modal (issue #278)`);
console.log(`Target: ${BASE_URL}\n`);

try {
	// --- Login ---
	// Keycloak on login.maik-hasler.de uses a two-step flow:
	// fill #username -> click #kc-login -> fill #password -> click #kc-login
	console.log("Step 1: Login as vera");
	await page.goto(BASE_URL);
	await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
	await page.waitForURL("**/realms/einsatzbereit/**", { timeout: 15_000 });
	await page.locator("#username").fill(USERNAME);
	await page.locator("#kc-login").click();
	await page.locator("#password").waitFor({ timeout: 10_000 });
	await page.locator("#password").fill(PASSWORD);
	await page.locator("#kc-login").click();
	await page.waitForURL(`${BASE_URL}/`, { timeout: 30_000 });
	await assert("Login redirects back to homepage", async () => {
		if (!page.url().startsWith(BASE_URL)) throw new Error(`Unexpected URL: ${page.url()}`);
	});

	// --- Navigate to achievements ---
	console.log("\nStep 2: Navigate to /achievements");
	await page.goto(`${BASE_URL}/achievements`);
	await page.waitForLoadState("networkidle");
	await assert("Achievements page title is visible", async () => {
		await page.locator("h1").waitFor({ timeout: 10_000 });
	});

	// --- Share button is present ---
	console.log("\nStep 3: Locate share button");
	const shareBtn = page.getByRole("button", {
		name: /errungenschaften teilen|share achievements/i,
	});
	await assert("Share button exists", async () => {
		await shareBtn.waitFor({ timeout: 5_000 });
	});

	// --- Click share - modal opens ---
	console.log("\nStep 4: Click share button");
	await shareBtn.click();

	await assert("Modal dialog appears (role=dialog)", async () => {
		await page.locator('[role="dialog"]').waitFor({ timeout: 5_000 });
	});

	await assert("Modal contains a QR code (SVG)", async () => {
		const svg = page.locator('[role="dialog"] svg').first();
		await svg.waitFor({ timeout: 5_000 });
	});

	await assert("Modal shows the share URL", async () => {
		const text = await page.locator('[role="dialog"]').textContent();
		if (!text?.includes("/achievements")) throw new Error("Share URL not visible in modal");
	});

	await assert("Copy link button is present", async () => {
		const btn = page
			.locator('[role="dialog"]')
			.getByRole("button", { name: /link kopieren|copy link/i });
		await btn.waitFor({ timeout: 3_000 });
	});

	// --- Escape closes the modal ---
	console.log("\nStep 5: Close modal with Escape");
	await page.keyboard.press("Escape");
	await assert("Modal closes after Escape", async () => {
		await page.locator('[role="dialog"]').waitFor({ state: "hidden", timeout: 3_000 });
	});

	// --- Reopen and close via backdrop ---
	console.log("\nStep 6: Close modal via backdrop click");
	await shareBtn.click();
	await page.locator('[role="dialog"]').waitFor({ timeout: 3_000 });
	await page.mouse.click(5, 5);
	await assert("Modal closes after backdrop click", async () => {
		await page.locator('[role="dialog"]').waitFor({ state: "hidden", timeout: 3_000 });
	});
} catch (err) {
	fail("Unexpected error during test run", err?.message ?? String(err));
} finally {
	await browser.close();
}

console.log(`\n${"=".repeat(50)}`);
console.log(`Results: ${passed} passed, ${failed} failed`);
console.log("=".repeat(50));

if (failed > 0) {
	process.exit(1);
}
