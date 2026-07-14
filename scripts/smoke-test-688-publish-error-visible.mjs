// Smoke test for #688 (PR #689): in the create-opportunity wizard, publishing
// a "Scheduled slots" (Waitlist) opportunity with no time slot appeared to do
// nothing - the blocking error ("A Scheduled slots opportunity must have at
// least one time slot before it can be published.") was real but rendered
// below the fold with no role="alert" and nothing to scroll/focus it into
// view. Verifies:
//   1. Step 4's subtitle now reads the Waitlist-specific copy instead of the
//      unconditional "Optional: ..." text.
//   2. Clicking Publish with no time slot added surfaces the error with
//      role="alert", scrolled into the viewport, and focused.
//
// Run: node scripts/smoke-test-688-publish-error-visible.mjs

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const KEYCLOAK = "https://login.maik-hasler.de";
const CLIENT_ID = "frontend";
const REALM = "einsatzbereit";

async function getToken(username, password) {
	const res = await fetch(
		`${KEYCLOAK}/realms/${REALM}/protocol/openid-connect/token`,
		{
			method: "POST",
			headers: { "Content-Type": "application/x-www-form-urlencoded" },
			body: new URLSearchParams({
				grant_type: "password",
				client_id: CLIENT_ID,
				username,
				password,
				scope: "openid",
			}),
		},
	);
	if (!res.ok) throw new Error(`Token request failed: ${res.status}`);
	const data = await res.json();
	if (!data.access_token) throw new Error("No access_token in response");
	return data.access_token;
}

async function loginAsUser(page, username, password) {
	await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	if ((await signInBtn.count()) > 0) {
		await signInBtn.first().click();
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await loginKeycloak(page, username, password);
	}
	await page.waitForSelector("main", { timeout: 10000 });
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok)
		throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const olafToken = await getToken("olaf", "olaf123");
	const authHeaders = {
		Authorization: `Bearer ${olafToken}`,
		"Content-Type": "application/json",
	};

	const orgsRes = await fetch(`${API}/v1/organizations`, {
		headers: authHeaders,
	});
	if (!orgsRes.ok)
		throw new Error(`GET /organizations failed: ${orgsRes.status}`);
	const orgs = await orgsRes.json();
	if (!Array.isArray(orgs) || orgs.length === 0)
		throw new Error("olaf has no organizations - cannot run this smoke test");
	const orgId = orgs[0].id;
	console.log(`OK  Using organization ${orgId}`);

	const { browser, page } = await launchLiveBrowser();
	try {
		await loginAsUser(page, "olaf", "olaf123");
		console.log("OK  Logged in as olaf");

		await page.goto(`${BASE}/organizations/${orgId}/dashboard`, {
			waitUntil: "networkidle",
		});

		await page
			.getByRole("button", { name: /\+ create opportunity/i })
			.click();
		const dialog = page.getByRole("dialog");
		await dialog.waitFor({ state: "visible", timeout: 15000 });
		console.log("OK  Create-opportunity wizard opened");

		// Step 1 - Basics
		await dialog.locator("#opportunity-title").fill(`Smoke688 ${Date.now()}`);
		await dialog
			.locator("#opportunity-description")
			.fill("Automated smoke test opportunity for #688 (never published).");
		await dialog.getByTestId("modal-next").click();

		// Step 2 - Location: mark Remote so no address is required
		await dialog.locator("#opportunity-remote").check();
		await dialog.getByTestId("modal-next").click();

		// Step 3 - Format: leave every default ("Scheduled slots"/Waitlist
		// participation type is pre-selected, matching the issue's repro steps)
		await dialog.getByTestId("modal-next").click();

		// Step 4 - Details: subtitle must reflect the Waitlist-specific copy
		await dialog.waitFor({ state: "visible" });
		const waitlistSubtitle = dialog.getByText(
			/also needs at least one time slot before it can be published/i,
		);
		await waitlistSubtitle.waitFor({ state: "visible", timeout: 10000 });
		console.log(
			"OK  Step 4 subtitle shows the Waitlist-specific copy (time slot not optional)",
		);

		// Leave everything default (no time slot added) and click Publish.
		await dialog.getByTestId("modal-submit").click();

		const errorAlert = dialog.getByRole("alert").filter({
			hasText:
				/scheduled slots opportunity must have at least one time slot/i,
		});
		await errorAlert.waitFor({ state: "visible", timeout: 10000 });
		console.log('OK  Publish-blocking error rendered with role="alert"');

		const box = await errorAlert.boundingBox();
		const viewport = page.viewportSize();
		if (!box) throw new Error("Could not read error element's bounding box");
		if (!viewport) throw new Error("Could not read page viewport size");
		const inViewport =
			box.y >= 0 &&
			box.y + box.height <= viewport.height &&
			box.x >= 0 &&
			box.x + box.width <= viewport.width;
		if (!inViewport)
			throw new Error(
				`Error element is outside the viewport: box=${JSON.stringify(box)} viewport=${JSON.stringify(viewport)}`,
			);
		console.log("OK  Error element is scrolled into the visible viewport");

		const isFocused = await errorAlert.evaluate(
			(el) => el === document.activeElement,
		);
		if (!isFocused)
			throw new Error("Error element did not receive focus after Publish");
		console.log("OK  Error element received focus");

		// Re-submitting the identical failure must re-fire the scroll/focus
		// effect (the errorToken fix) instead of silently no-oping on a repeat.
		await page.evaluate(() => (document.activeElement)?.blur());
		await dialog.getByTestId("modal-submit").click();
		await errorAlert.waitFor({ state: "visible", timeout: 10000 });
		const isFocusedAgain = await errorAlert.evaluate(
			(el) => el === document.activeElement,
		);
		if (!isFocusedAgain)
			throw new Error(
				"Error element did not regain focus on a repeat identical submit failure",
			);
		console.log("OK  Repeat submit with the same error re-focuses the alert");
	} finally {
		await browser.close();
	}

	console.log(
		"\nNo opportunity was created - Publish was blocked by validation both times, as expected.",
	);
	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
