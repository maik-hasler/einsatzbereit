/**
 * Smoke test for issue #516: profile tab inline edit mode.
 * Verifies: read-only view shown by default, Edit button present,
 * clicking Edit shows form fields, Cancel resets, Save persists.
 */
import { chromium } from "playwright";

const FRONTEND = "https://einsatzbereit.maik-hasler.de";

async function login(page, username, password) {
	await page.goto(`${FRONTEND}/profile`);
	await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15_000 });
	await page.locator("#username").fill(username);
	await page.locator("#kc-login").click();
	await page.locator("#password").fill(password);
	await page.locator("#kc-login").click();
	await page.waitForURL(/einsatzbereit\.maik-hasler\.de/, { timeout: 15_000 });
}

const browser = await chromium.launch({
	executablePath: "/opt/pw-browsers/chromium_headless_shell-1194/chrome-linux/headless_shell",
	proxy: { server: process.env.HTTPS_PROXY || "http://127.0.0.1:41641" },
});
const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await ctx.newPage();

try {
	console.log("Logging in as vera...");
	await login(page, "vera", "vera123");

	await page.waitForURL(/\/profile/, { timeout: 10_000 });
	console.log("On profile page.");

	// 1. Read-only view shown by default - Edit button must be visible
	const editBtn = page.getByRole("button", { name: /^Edit$|^Bearbeiten$/ });
	await editBtn.waitFor({ state: "visible", timeout: 20_000 });
	console.log("PASS: Edit button visible in read-only view.");

	// 2. Form fields must NOT be visible in view mode
	const usernameInput = page.getByLabel(/Username|Benutzername/);
	const isFormVisible = await usernameInput.isVisible();
	if (isFormVisible) {
		throw new Error("FAIL: Username input visible in view mode (expected hidden).");
	}
	console.log("PASS: Form fields hidden in view mode.");

	// 3. Click Edit - form should appear
	await editBtn.click();
	await page.getByLabel(/Username|Benutzername/).waitFor({ state: "visible", timeout: 5_000 });
	await page.getByLabel(/Email address|E-Mail/).waitFor({ state: "visible", timeout: 5_000 });
	console.log("PASS: Form fields visible after clicking Edit.");

	// 4. Cancel resets to view mode without saving
	const cancelBtn = page.getByRole("button", { name: /^Cancel$|^Abbrechen$/ });
	await cancelBtn.waitFor({ state: "visible", timeout: 5_000 });
	await cancelBtn.click();
	await editBtn.waitFor({ state: "visible", timeout: 5_000 });
	const stillVisible = await usernameInput.isVisible();
	if (stillVisible) {
		throw new Error("FAIL: Form still visible after Cancel.");
	}
	console.log("PASS: Cancel returns to read-only view.");

	// 5. Edit, fill, Save - success message shown and view mode restored
	await editBtn.click();
	await page.getByLabel(/First name|Vorname/).fill("Vera");
	await page.getByLabel(/Last name|Nachname/).fill("Sample");
	await page.getByRole("button", { name: /^Save$|^Speichern$/ }).click();
	await page
		.getByText(/Profile saved|Profil gespeichert/)
		.waitFor({ state: "visible", timeout: 10_000 });
	await editBtn.waitFor({ state: "visible", timeout: 5_000 });
	console.log("PASS: Save shows success message and returns to view mode.");

	console.log("\nAll assertions passed.");
} catch (err) {
	console.error("SMOKE TEST FAILED:", err.message);
	process.exit(1);
} finally {
	await browser.close();
}
