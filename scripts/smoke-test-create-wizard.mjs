/**
 * Smoke test for the create-opportunity multi-step wizard (#439 follow-up).
 * Verifies: simplified header with clickable stepper, floating labels, free
 * step navigation, save-as-draft button, banner upload affordance, and that
 * validation only blocks on publish (jumping to the failing step).
 * Run: node scripts/smoke-test-create-wizard.mjs
 *
 * Requires a logged-in organisator account (olaf/olaf123) and an existing org.
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
		// --- Login as organisator ---
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });

		const signInBtn = page.getByRole("button", {
			name: /sign in|anmelden/i,
		});
		if ((await signInBtn.count()) > 0) {
			await signInBtn.first().click();
			await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });

			await page.fill("#username", "olaf");
			await page.click("#kc-login");
			await page.fill("#password", "olaf123");
			await page.click("#kc-login");
			await page.waitForURL(BASE + "/**", { timeout: 15000 });
			console.log("OK  Logged in as olaf (organisator)");
		}

		await page.waitForSelector("main", { timeout: 10000 });

		// Wait for OrganizationSwitcher to auto-set the active-org cookie, then
		// reload so VolunteerOpportunitiesList re-renders with activeOrgId != null.
		try {
			await page.waitForFunction(
				() => document.cookie.includes("active-org="),
				{ timeout: 8000 },
			);
			console.log("OK  active-org cookie set by OrganizationSwitcher");
			await page.reload({ waitUntil: "networkidle" });
		} catch {
			console.log("WARN  active-org cookie not set - olaf may have no org");
		}

		// --- Open the create-opportunity wizard ---
		const createBtn = page.getByTestId("create-opportunity-btn");
		if ((await createBtn.count()) === 0) {
			console.log(
				"WARN  Create button not visible (no org / not organisator) - skipping wizard checks",
			);
			return;
		}
		await createBtn.first().click();

		await page.waitForSelector('[role="dialog"]', { timeout: 8000 });
		console.log("OK  Create-opportunity dialog opened");

		// --- Simplified header: brand accent + clickable stepper ---
		const accent = page.locator('[role="dialog"] [class*="from-brand-600"]');
		if ((await accent.count()) === 0)
			throw new Error("Brand accent bar missing");
		console.log("OK  Brand accent bar present");

		for (let n = 1; n <= 4; n++) {
			const stepBtn = page.getByTestId(`wizard-stepper-${n}`);
			if ((await stepBtn.count()) === 0)
				throw new Error(`Stepper button ${n} missing`);
		}
		console.log("OK  Clickable stepper with 4 labelled steps present");

		// --- Floating labels on step 1 ---
		await page.waitForSelector('[data-testid="wizard-step-1"]', {
			timeout: 5000,
		});
		const floatingLabel = page.locator(
			'label[for="opportunity-title"][class*="peer-placeholder-shown"]',
		);
		if ((await floatingLabel.count()) === 0)
			throw new Error("Floating label missing on title field");
		console.log("OK  Floating labels present");

		// Banner upload affordance present
		if ((await page.locator("#opportunity-banner").count()) === 0)
			throw new Error("Banner upload input missing on step 1");
		console.log("OK  Banner upload affordance present");

		// --- Free navigation: Next works with empty fields ---
		const nextBtn = page.getByTestId("modal-next");
		if ((await nextBtn.getAttribute("disabled")) !== null)
			throw new Error("Next button should not be disabled (free navigation)");
		await nextBtn.click();
		await page.waitForSelector('[data-testid="wizard-step-2"]', {
			timeout: 5000,
		});
		console.log("OK  Free navigation: advanced to step 2 with empty step 1");

		// Stepper jump: 2 -> 4 directly
		await page.getByTestId("wizard-stepper-4").click();
		await page.waitForSelector('[data-testid="wizard-step-4"]', {
			timeout: 5000,
		});
		console.log("OK  Stepper jump 2 -> 4 works");

		// --- Save-as-draft button present ---
		const draftBtn = page.getByTestId("modal-save-draft");
		if ((await draftBtn.count()) === 0)
			throw new Error("Save-as-draft button missing");
		console.log("OK  Save-as-draft button present");

		// --- Publish with empty fields jumps back to step 1 with errors ---
		await page.getByTestId("modal-submit").click();
		await page.waitForSelector('[data-testid="wizard-step-1"]', {
			timeout: 5000,
		});
		const fieldError = page.locator(
			'[data-testid="wizard-step-1"] [role="alert"]',
		);
		if ((await fieldError.count()) === 0)
			throw new Error("Validation errors not shown after empty publish");
		console.log("OK  Publish with empty fields jumps to step 1 with errors");

		// --- Fill step 1 and check the location step ---
		await page.fill("#opportunity-title", "Smoke Test Wizard Opportunity");
		await page.fill(
			"#opportunity-description",
			"This is a smoke test opportunity created by the automated wizard test.",
		);

		await page.getByTestId("wizard-stepper-2").click();
		await page.waitForSelector('[data-testid="wizard-step-2"]', {
			timeout: 5000,
		});
		const hint = page.locator(
			'[data-testid="wizard-step-2"] [class*="bg-brand-50"]',
		);
		if ((await hint.count()) === 0)
			throw new Error("Location hint card missing on step 2");
		console.log("OK  Location hint card present");

		// Fill address (overwrites or completes any org pre-fill)
		await page.fill("#opportunity-street", "Musterstrasse");
		await page.fill("#opportunity-house", "42");
		await page.fill("#opportunity-zip", "10115");
		await page.fill("#opportunity-city", "Berlin");

		// --- Step 3 format cards still working ---
		await page.getByTestId("wizard-stepper-3").click();
		await page.waitForSelector('[data-testid="wizard-step-3"]', {
			timeout: 5000,
		});
		const occurrenceCards = page.locator(
			'[data-testid="wizard-step-3"] label[class*="rounded-xl"]',
		);
		if ((await occurrenceCards.count()) < 6)
			throw new Error("Format step card radio options missing");
		console.log("OK  Format card options rendered");

		// --- Step 4 and submit availability ---
		await page.getByTestId("wizard-stepper-4").click();
		await page.waitForSelector('[data-testid="wizard-step-4"]', {
			timeout: 5000,
		});
		const submitBtn = page.getByTestId("modal-submit");
		if ((await submitBtn.getAttribute("disabled")) !== null)
			throw new Error("Publish button unexpectedly disabled on step 4");
		console.log("OK  Publish button present and enabled");

		// Close wizard without creating data
		await page.keyboard.press("Escape");
		await page.waitForSelector('[role="dialog"]', {
			state: "hidden",
			timeout: 5000,
		});
		console.log("OK  Escape key closes dialog");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
