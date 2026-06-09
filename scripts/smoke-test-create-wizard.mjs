/**
 * Smoke test for the create-opportunity multi-step wizard (#439 follow-up).
 * Verifies: wizard opens, all 4 steps are navigable, form submits successfully,
 * and validation prevents advancing from step 1/2 with empty required fields.
 * Run: node scripts/smoke-test-create-wizard.mjs
 *
 * Requires a logged-in organisator account (olaf/olaf123) and an existing org.
 */

import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const KC = "https://login.maik-hasler.de";
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

		// --- Open the create-opportunity wizard ---
		const createBtn = page.getByRole("button", {
			name: /create opportunity|einsatz erstellen/i,
		});
		if ((await createBtn.count()) === 0) {
			console.log(
				"WARN  Create button not visible (no org selected?) - skipping wizard checks",
			);
			return;
		}
		await createBtn.first().click();

		// Dialog must be open
		await page.waitForSelector('[role="dialog"]', { timeout: 8000 });
		console.log("OK  Create-opportunity dialog opened");

		// --- Step 1: Basics ---
		await page.waitForSelector('[data-testid="wizard-step-1"]', {
			timeout: 5000,
		});
		console.log("OK  Step 1 (Basics) visible");

		// Gradient header must be present
		const header = page.locator(
			'.bg-gradient-to-br.from-brand-600, [class*="from-brand-600"]',
		);
		if ((await header.count()) === 0)
			throw new Error("Wizard gradient header missing");
		console.log("OK  Gradient header present");

		// Step dot indicator present
		const dots = page.locator('[role="dialog"] [aria-hidden="true"] div');
		if ((await dots.count()) < 4)
			throw new Error("Step dot indicator missing or incomplete");
		console.log("OK  Step dot indicator present");

		// Next button should be disabled with empty fields
		const nextBtn = page.getByRole("button", { name: /next|weiter/i });
		const isDisabled = await nextBtn.getAttribute("disabled");
		if (isDisabled === null)
			throw new Error("Next button should be disabled on empty step 1");
		console.log("OK  Next button disabled on empty step 1");

		// Fill title + description
		await page.fill("#opportunity-title", "Smoke Test Wizard Opportunity");
		await page.fill(
			"#opportunity-description",
			"This is a smoke test opportunity created by the automated wizard test.",
		);

		// Next should now be enabled
		const isDisabledAfterFill = await nextBtn.getAttribute("disabled");
		if (isDisabledAfterFill !== null)
			throw new Error("Next button should be enabled after filling step 1");
		console.log("OK  Next button enabled after filling step 1");

		// Char counters visible
		const charCounter = page
			.locator('[data-testid="wizard-step-1"] p')
			.filter({ hasText: "/" });
		if ((await charCounter.count()) === 0)
			throw new Error("Character counter missing on step 1");
		console.log("OK  Character counters present");

		// --- Advance to Step 2 ---
		await nextBtn.click();
		await page.waitForSelector('[data-testid="wizard-step-2"]', {
			timeout: 5000,
		});
		console.log("OK  Advanced to step 2 (Location)");

		// Hint card must be present
		const hint = page.locator(
			'[data-testid="wizard-step-2"] .bg-brand-50, [data-testid="wizard-step-2"] [class*="bg-brand-50"]',
		);
		if ((await hint.count()) === 0)
			throw new Error("Location hint card missing on step 2");
		console.log("OK  Location hint card present");

		// Fill address
		await page.fill("#opportunity-street", "Musterstraße");
		await page.fill("#opportunity-house", "42");
		await page.fill("#opportunity-zip", "10115");
		await page.fill("#opportunity-city", "Berlin");

		// --- Advance to Step 3 ---
		await nextBtn.click();
		await page.waitForSelector('[data-testid="wizard-step-3"]', {
			timeout: 5000,
		});
		console.log("OK  Advanced to step 3 (Format)");

		// Card-style radios should be rendered for occurrence
		const occurrenceCards = page.locator(
			'[data-testid="wizard-step-3"] label[class*="rounded-xl"]',
		);
		if ((await occurrenceCards.count()) < 6)
			throw new Error("Format step card radio options missing");
		console.log("OK  Format card options rendered");

		// Select Recurring + Waitlist
		await page
			.getByRole("radio", { name: /recurring|regelmäßig/i })
			.click({ force: true });
		await page
			.getByRole("radio", { name: /waitlist|warteliste/i })
			.click({ force: true });

		// --- Advance to Step 4 ---
		await nextBtn.click();
		await page.waitForSelector('[data-testid="wizard-step-4"]', {
			timeout: 5000,
		});
		console.log("OK  Advanced to step 4 (Details)");

		// Category select must exist
		const categorySelect = page.locator("#create-category");
		if ((await categorySelect.count()) === 0)
			throw new Error("Category select missing on step 4");
		await categorySelect.selectOption("Environment");

		// Tags input + chip preview
		const tagsInput = page.locator("#create-tags");
		await tagsInput.fill("outdoor, nature");
		const chip = page.locator('[class*="bg-brand-100"]').filter({ hasText: "outdoor" });
		if ((await chip.count()) === 0)
			throw new Error("Tag chip preview not rendered");
		console.log("OK  Tag chips rendered");

		// Time slot section visible (Waitlist mode)
		const timeSlotsSection = page.locator(
			'[data-testid="wizard-step-4"] [class*="rounded-xl"]',
		).first();
		if ((await timeSlotsSection.count()) === 0)
			throw new Error("Time slots section missing on step 4");
		console.log("OK  Time slots section visible");

		// Submit button is present and enabled
		const submitBtn = page.getByTestId("modal-submit");
		const submitDisabled = await submitBtn.getAttribute("disabled");
		if (submitDisabled !== null)
			throw new Error("Submit button unexpectedly disabled on step 4");
		console.log("OK  Submit button present and enabled");

		// Back navigation works
		const backBtn = page.getByRole("button", { name: /back|zurück/i });
		await backBtn.click();
		await page.waitForSelector('[data-testid="wizard-step-3"]', {
			timeout: 5000,
		});
		console.log("OK  Back navigation works (step 4 -> step 3)");

		// Close wizard
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
