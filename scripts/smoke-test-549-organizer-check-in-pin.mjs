/**
 * Smoke test for #549: let the organizer set the check-in PIN (with a
 * "generate random" option), on both create and edit.
 *
 * Drives the actual create/edit-opportunity modal in the browser:
 *  1. Create an IndividualContact opportunity, select "PIN Code" check-in,
 *     type a custom PIN, publish it, then verify via the organizer-only
 *     check-in-pin endpoint that the custom PIN was actually persisted
 *     (not silently overwritten by a random one).
 *  2. Reopen the same opportunity in edit mode, verify the PIN field
 *     prefills with the existing PIN, click "Generate random", save, and
 *     verify the persisted PIN changed to the newly generated value.
 *
 * Run: node scripts/smoke-test-549-organizer-check-in-pin.mjs
 */

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

async function getCheckInPin(token, opportunityId) {
	const res = await fetch(
		`${API}/v1/volunteer-opportunities/${opportunityId}/check-in-pin`,
		{ headers: { Authorization: `Bearer ${token}` } },
	);
	if (!res.ok)
		throw new Error(`GET check-in-pin failed: ${res.status} ${await res.text()}`);
	return res.json();
}

async function deleteOpportunity(token, opportunityId) {
	const res = await fetch(
		`${API}/v1/volunteer-opportunities/${opportunityId}`,
		{ method: "DELETE", headers: { Authorization: `Bearer ${token}` } },
	);
	if (!res.ok)
		throw new Error(`Delete failed: ${res.status} ${await res.text()}`);
}

async function getFirstOrgId(token) {
	const res = await fetch(`${API}/v1/organizations`, {
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok) throw new Error(`GET /organizations failed: ${res.status}`);
	const orgs = await res.json();
	if (!Array.isArray(orgs) || orgs.length === 0)
		throw new Error("olaf has no organizations - cannot run this smoke test");
	return orgs[0].id;
}

async function loginAsOrganizer(page, orgId) {
	await page.goto(`${BASE}/organizations/${orgId}/dashboard`, {
		waitUntil: "networkidle",
	});
	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	if ((await signInBtn.count()) > 0) {
		await signInBtn.first().click();
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await loginKeycloak(page, "olaf", "olaf123");
		await page.waitForURL(`${BASE}/organizations/${orgId}/dashboard`, {
			timeout: 15000,
		});
	}
	await page.waitForSelector("main", { timeout: 10000 });
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const olafToken = await getToken("olaf", "olaf123");
	const orgId = await getFirstOrgId(olafToken);

	const { browser, page } = await launchLiveBrowser();
	let opportunityId;
	try {
		await loginAsOrganizer(page, orgId);
		console.log("OK  Logged in as olaf (organisator) and opened the org dashboard");

		const createBtn = page.getByTestId("create-opportunity-btn");
		if ((await createBtn.count()) === 0)
			throw new Error("Create-opportunity button not visible for olaf");
		await createBtn.first().click();
		await page.waitForSelector('[role="dialog"]', { timeout: 8000 });
		console.log("OK  Create-opportunity dialog opened");

		const suffix = Date.now();
		await page.waitForSelector('[data-testid="wizard-step-1"]', {
			timeout: 5000,
		});
		await page.fill("#opportunity-title", `Smoke549 CheckInPin ${suffix}`);
		await page.fill(
			"#opportunity-description",
			"Automated smoke test opportunity for #549.",
		);

		await page.getByTestId("wizard-stepper-2").click();
		await page.waitForSelector('[data-testid="wizard-step-2"]', {
			timeout: 5000,
		});
		await page.check("#opportunity-remote");
		console.log("OK  Marked opportunity as remote (skips address requirement)");

		await page.getByTestId("wizard-stepper-3").click();
		await page.waitForSelector('[data-testid="wizard-step-3"]', {
			timeout: 5000,
		});
		await page
			.locator('input[name="participationType"][value="IndividualContact"]')
			.click({ force: true });
		await page
			.locator('input[name="checkInMethod"][value="PINCode"]')
			.click({ force: true });

		const pinInput = page.locator("#create-check-in-pin");
		await pinInput.waitFor({ timeout: 5000 });
		console.log('OK  PIN input appears when "PIN Code" check-in method is selected');

		const customPin = "482170";
		await pinInput.fill(customPin);

		await page.getByTestId("wizard-stepper-4").click();
		await page.waitForSelector('[data-testid="wizard-step-4"]', {
			timeout: 5000,
		});

		const [createResponse] = await Promise.all([
			page.waitForResponse(
				(r) =>
					r.url().includes("/v1/volunteer-opportunities") &&
					r.request().method() === "POST",
			),
			page.getByTestId("modal-submit").click(),
		]);
		if (!createResponse.ok())
			throw new Error(`Create request failed: ${createResponse.status()}`);
		const created = await createResponse.json();
		opportunityId = created.id;
		console.log(`OK  Published opportunity ${opportunityId} with a custom check-in PIN`);

		await page.waitForSelector('[role="dialog"]', {
			state: "hidden",
			timeout: 8000,
		});

		const persistedPin = await getCheckInPin(olafToken, opportunityId);
		if (persistedPin !== customPin) {
			throw new Error(
				`Expected persisted PIN "${customPin}", got "${persistedPin}"`,
			);
		}
		console.log("OK  Custom PIN entered at create-time was persisted exactly");

		// === Edit mode: prefill + "Generate random" ===
		await page.goto(`${BASE}/volunteer-opportunities/${opportunityId}`, {
			waitUntil: "networkidle",
		});
		await page.getByRole("button", { name: /^edit$/i }).click();
		await page.waitForSelector('[role="dialog"]', { timeout: 8000 });
		await page.getByTestId("wizard-stepper-3").click();
		await page.waitForSelector('[data-testid="wizard-step-3"]', {
			timeout: 5000,
		});

		const editPinInput = page.locator("#create-check-in-pin");
		await editPinInput.waitFor({ timeout: 5000 });
		await page.waitForFunction(
			(expected) =>
				document.querySelector("#create-check-in-pin")?.value === expected,
			customPin,
			{ timeout: 8000 },
		);
		console.log("OK  Edit mode prefills the PIN field with the existing PIN");

		await page
			.getByRole("button", { name: /generate random/i })
			.click();
		const generatedPin = await editPinInput.inputValue();
		if (generatedPin === customPin || !/^\d{4}$/.test(generatedPin)) {
			throw new Error(
				`"Generate random" produced an unexpected value: "${generatedPin}"`,
			);
		}
		console.log(`OK  "Generate random" filled in a fresh 4-digit PIN (${generatedPin})`);

		await page.getByTestId("wizard-stepper-4").click();
		await page.waitForSelector('[data-testid="wizard-step-4"]', {
			timeout: 5000,
		});
		await Promise.all([
			page.waitForResponse(
				(r) =>
					r.url().includes(`/v1/volunteer-opportunities/${opportunityId}`) &&
					r.request().method() === "PUT",
			),
			page.getByTestId("modal-submit").click(),
		]);
		await page.waitForSelector('[role="dialog"]', {
			state: "hidden",
			timeout: 8000,
		});

		const updatedPin = await getCheckInPin(olafToken, opportunityId);
		if (updatedPin !== generatedPin) {
			throw new Error(
				`Expected persisted PIN "${generatedPin}" after edit, got "${updatedPin}"`,
			);
		}
		console.log("OK  Regenerated PIN from edit mode was persisted exactly");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
		if (opportunityId) {
			await deleteOpportunity(olafToken, opportunityId);
			console.log(`OK  Cleaned up opportunity ${opportunityId}`);
		}
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
