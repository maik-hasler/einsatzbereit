// Live verification for #676 Pitch 2: the create/edit opportunity wizard was
// rewritten with react-hook-form + zod, a plain header, fail-fast per-step
// validation, a chip-based tags input, and an unsaved-changes guard.
// Exercises the whole flow without creating any real opportunity (discards
// via the new unsaved-changes guard at the end), so no cleanup is needed.
import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const SITE = "https://einsatzbereit.maik-hasler.de";

const { browser, page } = await launchLiveBrowser();

try {
	await page.goto(SITE, { waitUntil: "networkidle" });

	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	if ((await signInBtn.count()) > 0) {
		await signInBtn.first().click();
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await loginKeycloak(page, "olaf", "olaf123");
	}
	await page.waitForSelector("main", { timeout: 15000 });
	console.log("PASS: logged in as olaf");

	const switcherBtn = page.getByRole("button", { name: "Switch organization" });
	await switcherBtn.first().click();
	await page.getByTestId("org-dashboard-link").first().click();
	await page.waitForLoadState("networkidle");

	const createBtn = page.getByRole("button", { name: "Create opportunity" });
	await createBtn.first().waitFor({ state: "visible", timeout: 15000 });
	await createBtn.first().click();

	const dialog = page.locator("[role='dialog']");
	await dialog.first().waitFor({ state: "visible", timeout: 5000 });
	console.log("PASS: create-opportunity modal opened");

	// Plain header: no leftover gradient accent bar.
	const accentCount = await dialog
		.first()
		.locator("[class*='from-brand-600']")
		.count();
	if (accentCount !== 0) throw new Error("Gradient accent bar still present");
	console.log("PASS: plain header (no gradient accent bar)");

	// Fail-fast validation: Next is blocked with empty required fields.
	const nextBtn = page.getByTestId("modal-next");
	await nextBtn.click();
	await page.getByTestId("wizard-step-1").waitFor({ state: "visible" });
	await page
		.locator("#opportunity-title-error")
		.waitFor({ state: "visible", timeout: 5000 });
	console.log("PASS: Next blocked on step 1 with empty required fields, error shown");

	// Fill required fields, advance.
	await page.locator("#opportunity-title").fill("Pitch2 Smoke Test");
	await page
		.locator("#opportunity-description")
		.fill("Live verification for #676 - never submitted, discarded at the end.");
	await nextBtn.click();
	await page.getByTestId("wizard-step-2").waitFor({ state: "visible" });
	console.log("PASS: advanced to step 2 after filling required fields");

	// Mark remote so address fields aren't required, advance.
	await page.locator("#opportunity-remote").check();
	await nextBtn.click();
	await page.getByTestId("wizard-step-3").waitFor({ state: "visible" });
	console.log("PASS: advanced to step 3 (remote, address not required)");

	// Step 3: PIN Code check-in with generated PIN.
	await page.locator("input[name='checkInMethod'][value='PINCode']").check();
	const pinInput = page.locator("#create-check-in-pin");
	await pinInput.waitFor({ state: "visible" });
	await page.getByRole("button", { name: "Generate random" }).click();
	const generatedPin = await pinInput.inputValue();
	if (!/^\d{4}$/.test(generatedPin))
		throw new Error(`Expected a 4-digit generated PIN, got "${generatedPin}"`);
	console.log(`PASS: generated a valid check-in PIN (${generatedPin})`);

	await nextBtn.click();
	await page.getByTestId("wizard-step-4").waitFor({ state: "visible" });
	console.log("PASS: advanced to step 4 (valid PIN accepted)");

	// Tags chip input: add, case-insensitive dedupe, remove.
	const tagsInput = page.locator("#create-tags");
	await tagsInput.fill("outdoor");
	await tagsInput.press("Enter");
	await tagsInput.fill("Outdoor");
	await tagsInput.press("Enter");
	const chipCount = await page.getByText("outdoor", { exact: true }).count();
	if (chipCount !== 1)
		throw new Error(`Expected exactly 1 "outdoor" chip after dedupe, found ${chipCount}`);
	console.log("PASS: tags chip input adds and case-insensitively dedupes");

	await page.getByRole("button", { name: /remove tag outdoor/i }).click();
	if ((await page.getByText("outdoor", { exact: true }).count()) !== 0)
		throw new Error("Tag chip was not removed");
	console.log("PASS: tag chip removable");

	// Unsaved-changes guard: Escape prompts, "Keep" dismisses the prompt only.
	await page.keyboard.press("Escape");
	const discardBtn = page.getByRole("button", { name: "Discard changes" });
	await discardBtn.waitFor({ state: "visible", timeout: 5000 });
	console.log("PASS: Escape on a dirty form shows the discard-changes confirmation");

	await page.getByRole("button", { name: "Keep" }).click();
	await discardBtn.waitFor({ state: "hidden", timeout: 5000 });
	await dialog.first().waitFor({ state: "visible" });
	console.log("PASS: \"Keep\" dismisses the confirmation, wizard stays open");

	// Escape again, actually discard this time.
	await page.keyboard.press("Escape");
	await discardBtn.waitFor({ state: "visible", timeout: 5000 });
	await discardBtn.click();
	await dialog.first().waitFor({ state: "hidden", timeout: 5000 });
	console.log("PASS: \"Discard changes\" closes both dialogs - no opportunity created");

	console.log("\nAll checks passed.");
} finally {
	await browser.close();
}
