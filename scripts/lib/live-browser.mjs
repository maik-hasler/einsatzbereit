/**
 * Shared helpers for one-off Playwright scripts under scripts/ that verify
 * behaviour on the live staging site (https://einsatzbereit.maik-hasler.de).
 *
 * Import from here instead of copy-pasting the browser launch / Keycloak
 * login boilerplate into a new script - see CLAUDE.md's "Notes on live
 * Playwright scripts" for why the launch args below are required.
 */
import { chromium } from "playwright";
import { existsSync } from "node:fs";

const SANDBOX_CHROMIUM_PATH = "/opt/pw-browsers/chromium";

/**
 * Launches Chromium with the args needed to survive the Claude Code web/cloud
 * sandbox's TLS-reterminating egress proxy, and returns a ready-to-use page.
 * Outside that sandbox (e.g. a local dev machine with direct internet
 * access) the sandbox-only chromium binary this relies on doesn't exist, so
 * this falls back to a plain launch instead.
 */
export async function launchLiveBrowser() {
	const browser = existsSync(SANDBOX_CHROMIUM_PATH)
		? await chromium.launch({
				executablePath: SANDBOX_CHROMIUM_PATH,
				proxy: { server: process.env.HTTPS_PROXY ?? "http://127.0.0.1:42149" },
				args: [
					"--no-sandbox",
					"--disable-setuid-sandbox",
					"--disable-http2",
					"--disable-quic",
					"--ssl-version-max=tls1.2",
					"--disable-features=PostQuantumKyber,EncryptedClientHello",
				],
			})
		: await chromium.launch();
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
