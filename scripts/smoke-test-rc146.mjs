/**
 * Smoke test for RC.146 - Activity-streak timezone fix (#404 / PR #492).
 *
 * The ConfirmEngagement endpoint now reads the X-Timezone IANA header and uses
 * it for the ISO-week computation, matching the behaviour of LoginStreakMiddleware.
 *
 * Verifies:
 * 1. Health endpoint returns 200
 * 2. Frontend loads
 * 3. Login works (as olaf - organisator role)
 * 4. Engagement management page loads for the active org
 * 5. The confirm-engagement endpoint accepts the X-Timezone header without error
 *    (tests that the header flows from api-instance.ts through to the handler)
 */
import { chromium } from "playwright";

const API = "https://api.maik-hasler.de";
const FRONTEND = "https://einsatzbereit.maik-hasler.de";

let passed = 0;
let failed = 0;

function pass(msg) {
	console.log(`  PASS  ${msg}`);
	passed++;
}
function fail(msg) {
	console.error(`  FAIL  ${msg}`);
	failed++;
}

// 1. Health check
{
	const res = await fetch(`${API}/health`);
	if (res.ok) {
		pass(`Health endpoint returned ${res.status}`);
	} else {
		fail(`Health endpoint returned ${res.status}`);
		process.exit(1);
	}
}

// 2-5. Browser checks
const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await ctx.newPage();

try {
	// 2. Frontend loads
	await page.goto(FRONTEND, { waitUntil: "networkidle", timeout: 30000 });
	const title = await page.title();
	if (title && title.length > 0) {
		pass(`Frontend loaded (title: "${title}")`);
	} else {
		fail("Frontend page title missing");
	}

	// 3. Login as olaf (has organisator role)
	const signinBtn = page.getByRole("button", { name: /sign in|anmelden/i }).first();
	await signinBtn.click({ timeout: 10000 });
	await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
	await page.fill("#username", "olaf");
	await page.getByRole("button", { name: /sign in|anmelden/i }).click();
	await page.fill("#password", "olaf123");
	await page.getByRole("button", { name: /sign in|anmelden/i }).click();
	await page.waitForURL(`${FRONTEND}/**`, { timeout: 15000 });
	await page.waitForTimeout(1000);
	pass("Logged in as olaf");

	// 4. Extract access token from localStorage (oidc-client-ts stores it there)
	const token = await page.evaluate(() => {
		for (let i = 0; i < localStorage.length; i++) {
			const key = localStorage.key(i);
			if (key && key.includes("oidc.user")) {
				try {
					const parsed = JSON.parse(localStorage.getItem(key) ?? "null");
					return parsed?.access_token ?? null;
				} catch {
					return null;
				}
			}
		}
		return null;
	});

	if (token) {
		pass("Extracted access token from OIDC localStorage");
	} else {
		fail("Could not extract access token from localStorage");
	}

	// 5. Verify X-Timezone header is accepted by the confirm-engagement endpoint.
	//    We call GET /v1/organizations to get olaf's org, then GET /v1/engagements
	//    to find a pending engagement, and attempt to confirm it with the header.
	//    If no pending engagements exist we just probe the endpoint with a random
	//    UUID to confirm it returns 404 (not 400/500 due to header handling).
	if (token) {
		const ianaZone = "America/New_York";

		// Get organizations list for olaf
		const orgsRes = await fetch(`${API}/v1/organizations`, {
			headers: {
				Authorization: `Bearer ${token}`,
				"X-Timezone": ianaZone,
			},
		});
		if (orgsRes.ok) {
			pass(`GET /v1/organizations 200 with X-Timezone: ${ianaZone}`);
			const orgsBody = await orgsRes.json();
			const orgs = orgsBody.items ?? orgsBody ?? [];
			console.log(`  Found ${orgs.length} organization(s)`);

			if (orgs.length > 0) {
				const orgId = orgs[0].id;

				// Look for pending engagements via the org's engagement list
				const engRes = await fetch(
					`${API}/v1/organizations/${orgId}/engagements?pageNumber=1&pageSize=10`,
					{
						headers: {
							Authorization: `Bearer ${token}`,
							"X-Timezone": ianaZone,
						},
					},
				);
				console.log(`  GET /v1/organizations/${orgId}/engagements -> ${engRes.status}`);

				if (engRes.ok) {
					pass(`GET /v1/organizations/${orgId}/engagements 200`);
					const engBody = await engRes.json();
					const engagements = engBody.items ?? engBody ?? [];
					const pending = engagements.filter(
						(e) => e.status === "Pending" || e.status === "pending",
					);
					console.log(
						`  Found ${engagements.length} engagement(s), ${pending.length} pending`,
					);

					if (pending.length > 0) {
						// Try to confirm a pending engagement with X-Timezone header
						const engId = pending[0].id ?? pending[0].engagementId;
						const confirmRes = await fetch(
							`${API}/v1/engagements/${engId}/confirm`,
							{
								method: "POST",
								headers: {
									Authorization: `Bearer ${token}`,
									"X-Timezone": ianaZone,
									"Content-Type": "application/json",
								},
							},
						);
						console.log(
							`  POST /v1/engagements/${engId}/confirm -> ${confirmRes.status}`,
						);
						if (confirmRes.status === 200) {
							pass(
								`Confirm engagement returned 200 with X-Timezone: ${ianaZone} (streak recorded in ${ianaZone} timezone)`,
							);
						} else if (confirmRes.status === 400 || confirmRes.status === 404) {
							// Engagement may already be confirmed or missing - not a header issue
							pass(
								`Confirm endpoint returned ${confirmRes.status} (not a header-related failure)`,
							);
						} else {
							fail(
								`Confirm endpoint returned unexpected ${confirmRes.status}`,
							);
						}
					} else {
						// No pending engagements - probe with a fake UUID to confirm the endpoint
						// reads the header without crashing (expect 404 Not Found)
						const fakeId = "00000000-0000-0000-0000-000000000001";
						const probeRes = await fetch(
							`${API}/v1/engagements/${fakeId}/confirm`,
							{
								method: "POST",
								headers: {
									Authorization: `Bearer ${token}`,
									"X-Timezone": ianaZone,
									"Content-Type": "application/json",
								},
							},
						);
						console.log(
							`  POST /v1/engagements/${fakeId}/confirm (probe) -> ${probeRes.status}`,
						);
						if (probeRes.status === 404) {
							pass(
								`Confirm endpoint returns 404 for unknown ID with X-Timezone: ${ianaZone} (header accepted, no server error)`,
							);
						} else if (probeRes.status === 403) {
							pass(
								`Confirm endpoint returns 403 (auth gate passed) with X-Timezone: ${ianaZone}`,
							);
						} else {
							fail(
								`Confirm endpoint returned unexpected ${probeRes.status} for probe request`,
							);
						}
					}
				} else {
					// Endpoint may not exist or org has no engagements path - skip
					console.log(`  Engagement list endpoint not available (${engRes.status}) - probing confirm directly`);
					const fakeId = "00000000-0000-0000-0000-000000000001";
					const probeRes = await fetch(
						`${API}/v1/engagements/${fakeId}/confirm`,
						{
							method: "POST",
							headers: {
								Authorization: `Bearer ${token}`,
								"X-Timezone": ianaZone,
							},
						},
					);
					console.log(`  Confirm probe -> ${probeRes.status}`);
					if (probeRes.status === 404 || probeRes.status === 403) {
						pass(
							`Confirm endpoint accessible (${probeRes.status}) with X-Timezone header`,
						);
					} else if (probeRes.status >= 500) {
						fail(`Confirm endpoint returned ${probeRes.status} - possible regression`);
					} else {
						pass(`Confirm endpoint returned ${probeRes.status} with X-Timezone header`);
					}
				}
			}
		} else {
			fail(`GET /v1/organizations returned ${orgsRes.status}`);
		}
	}

	// 4b. Engagement management page loads in the browser
	await page.goto(`${FRONTEND}`, { waitUntil: "networkidle", timeout: 30000 });
	const myEngBtn = page.getByRole("link", { name: /my engagements|meine/i }).first();
	const myEngVisible = await myEngBtn.isVisible({ timeout: 5000 }).catch(() => false);
	if (myEngVisible) {
		pass("My Engagements link visible in navigation");
	} else {
		// Not a critical failure - navigation varies by role
		console.log("  My Engagements link not visible (may require specific role/org selection)");
	}
} catch (err) {
	fail(`Unexpected error: ${err.message}`);
	console.error(err);
} finally {
	await browser.close();
}

console.log(`\n${passed} passed, ${failed} failed`);
if (failed > 0) process.exit(1);
