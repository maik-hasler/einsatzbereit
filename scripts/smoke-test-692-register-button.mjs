// Smoke test for #692: the header's "Register" button called the same
// auth.signinRedirect() as "Sign in", so it always landed on Keycloak's
// login form instead of the registration form. The fix points "Register"
// at a dedicated UserManager (frontend/src/lib/keycloakRegistration.ts)
// that targets Keycloak's /protocol/openid-connect/registrations endpoint.
//
// Verifies clicking "Register" (logged out) lands on the registrations
// endpoint with the registration form visible, distinct from "Sign in"
// which must still land on the plain login form.
//
// No data is created, nothing to clean up.
// Run: node scripts/smoke-test-692-register-button.mjs

import { launchLiveBrowser } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const { browser, page } = await launchLiveBrowser();
	try {
		await page.goto(BASE, { waitUntil: "networkidle" });

		await page.getByRole("button", { name: /^register$/i }).first().click();
		await page.waitForURL(/\/realms\/einsatzbereit\/protocol\/openid-connect\/registrations/, {
			timeout: 30000,
		});
		console.log("OK  Register button navigated to the registrations endpoint");

		await page.locator("#kc-register-form").waitFor({ state: "visible", timeout: 15000 });
		console.log("OK  Keycloak registration form is visible");

		await page.goto(BASE, { waitUntil: "networkidle" });
		await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
		await page.waitForURL(/\/realms\/einsatzbereit\/protocol\/openid-connect\/auth/, {
			timeout: 30000,
		});
		console.log("OK  Sign in button still navigates to the plain login endpoint (unaffected)");
	} finally {
		await browser.close();
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
