/**
 * Shared helpers for one-off Playwright scripts under scripts/ that verify
 * behaviour on the live staging site (https://einsatzbereit.maik-hasler.de).
 *
 * Import from here instead of copy-pasting the browser launch / Keycloak
 * login boilerplate into a new script - see CLAUDE.md's "Notes on live
 * Playwright scripts" for why the launch args below are required.
 */
import { chromium } from "playwright";

/**
 * Launches Chromium with the args needed to survive this sandbox's
 * TLS-reterminating egress proxy, and returns a ready-to-use page.
 * Not needed outside this sandboxed runner.
 */
export async function launchLiveBrowser() {
	const browser = await chromium.launch({
		executablePath: "/opt/pw-browsers/chromium",
		proxy: { server: process.env.HTTPS_PROXY ?? "http://127.0.0.1:42149" },
		args: [
			"--no-sandbox",
			"--disable-setuid-sandbox",
			"--disable-http2",
			"--disable-quic",
			"--ssl-version-max=tls1.2",
			"--disable-features=PostQuantumKyber,EncryptedClientHello",
		],
	});
	const context = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await context.newPage();
	return { browser, context, page };
}

/**
 * Fills the live Keycloak login form's two steps (username, then password).
 * Call this once the page has already navigated to the Keycloak login page
 * (e.g. after clicking the app's "Sign in"/"Anmelden" button).
 */
export async function loginKeycloak(page, username, password) {
	await page.fill("#username", username);
	await page.click("#kc-login");
	await page.fill("#password", password);
	await page.click("#kc-login");
	await page.waitForLoadState("networkidle", { timeout: 30000 });
}
