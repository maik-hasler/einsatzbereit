/**
 * Smoke test for #710: revive and finish feature/ddd-improvements
 * (Result pattern, domain events, value objects).
 *
 * The Result-pattern migration changed how domain failures map to HTTP
 * status codes. CI's IntegrationTests caught two mismatches against the
 * pre-refactor behaviour (both now fixed to return 400, matching what the
 * old DomainException-based handler produced):
 *   - Checking in an engagement that isn't Confirmed yet.
 *   - Checking in via PIN using someone else's engagement.
 * This script reproduces both against live staging, plus a positive-path
 * check that the custom check-in PIN flow (VolunteerOpportunity.Create /
 * ChangeCheckInMethod, also touched by this refactor) still works
 * end-to-end.
 *
 * Run: node scripts/smoke-test-710-ddd-refactor.mjs
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

async function api(token, method, path, body) {
	const res = await fetch(`${API}${path}`, {
		method,
		headers: {
			...(token ? { Authorization: `Bearer ${token}` } : {}),
			...(body ? { "Content-Type": "application/json" } : {}),
		},
		body: body ? JSON.stringify(body) : undefined,
	});
	const text = await res.text();
	const json = text ? JSON.parse(text) : null;
	return { status: res.status, body: json };
}

async function getFirstOrgId(token) {
	const { status, body } = await api(token, "GET", "/v1/organizations");
	if (status !== 200 || !Array.isArray(body) || body.length === 0)
		throw new Error("olaf has no organizations - cannot run this smoke test");
	return body[0].id;
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const olafToken = await getToken("olaf", "olaf123");
	const veraToken = await getToken("vera", "vera123");
	const orgId = await getFirstOrgId(olafToken);

	let opportunityId;
	try {
		// === Create an IndividualContact / PINCode opportunity with a custom PIN ===
		const customPin = "482170";
		const created = await api(olafToken, "POST", "/v1/volunteer-opportunities", {
			title: `Smoke710 DDD refactor ${Date.now()}`,
			description: "Automated smoke test opportunity for #710.",
			organizationId: orgId,
			isRemote: true,
			occurrence: "OneTime",
			participationType: "IndividualContact",
			checkInMethod: "PINCode",
			checkInPin: customPin,
			isDraft: false,
		});
		if (created.status !== 200)
			throw new Error(`Create opportunity failed: ${created.status} ${JSON.stringify(created.body)}`);
		opportunityId = created.body.id;
		console.log(`OK  Created opportunity ${opportunityId} with a custom check-in PIN (VolunteerOpportunity.Create)`);

		const pinCheck = await api(
			olafToken,
			"GET",
			`/v1/volunteer-opportunities/${opportunityId}/check-in-pin`,
		);
		if (pinCheck.status !== 200 || pinCheck.body !== customPin)
			throw new Error(`Expected persisted PIN "${customPin}", got ${JSON.stringify(pinCheck.body)}`);
		console.log("OK  Custom PIN was persisted exactly (IPinGenerator override path)");

		// === vera signs up (Pending) ===
		const engagement = await api(
			veraToken,
			"POST",
			`/v1/volunteer-opportunities/${opportunityId}/engagements`,
			{ type: "IndividualContact", message: "I'd like to help with this!" },
		);
		if (engagement.status !== 201)
			throw new Error(`Create engagement failed: ${engagement.status} ${JSON.stringify(engagement.body)}`);
		const engagementId = engagement.body.id;
		console.log(`OK  vera signed up (engagement ${engagementId}, status Pending)`);

		// === Regression #1: checking in a not-yet-confirmed engagement must be 400 ===
		const earlyCheckIn = await api(
			olafToken,
			"POST",
			`/v1/engagements/${engagementId}/check-in`,
		);
		if (earlyCheckIn.status !== 400) {
			throw new Error(
				`Expected 400 checking in a Pending engagement (Engagement.CheckIn NotConfirmed), got ${earlyCheckIn.status}: ${JSON.stringify(earlyCheckIn.body)}`,
			);
		}
		console.log("OK  Checking in a not-yet-confirmed engagement correctly returns 400 (regression fixed)");

		// === Confirm it ===
		const confirmed = await api(
			olafToken,
			"POST",
			`/v1/engagements/${engagementId}/confirm`,
		);
		if (confirmed.status !== 200 || confirmed.body.status !== "Confirmed")
			throw new Error(`Confirm failed: ${confirmed.status} ${JSON.stringify(confirmed.body)}`);
		console.log("OK  Engagement confirmed");

		// === Regression #2: checking in via PIN with someone else's engagement must be 400 ===
		const wrongOwnerCheckIn = await api(
			olafToken,
			"POST",
			`/v1/me/engagements/${engagementId}/check-in`,
			{ pin: customPin },
		);
		if (wrongOwnerCheckIn.status !== 400) {
			throw new Error(
				`Expected 400 checking in someone else's engagement via PIN (CheckInWithPin NotOwner), got ${wrongOwnerCheckIn.status}: ${JSON.stringify(wrongOwnerCheckIn.body)}`,
			);
		}
		console.log("OK  Checking in someone else's engagement via PIN correctly returns 400 (regression fixed)");

		// === Positive path: vera checks herself in with the correct PIN ===
		const ownCheckIn = await api(
			veraToken,
			"POST",
			`/v1/me/engagements/${engagementId}/check-in`,
			{ pin: customPin },
		);
		if (ownCheckIn.status !== 200 || ownCheckIn.body.status !== "Confirmed")
			throw new Error(`Own check-in failed: ${ownCheckIn.status} ${JSON.stringify(ownCheckIn.body)}`);
		console.log("OK  vera successfully checked herself in with the correct PIN");

		// === Browser check: the org's engagement management page still renders ===
		const { browser, page } = await launchLiveBrowser();
		try {
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
			const consoleErrors = [];
			page.on("pageerror", (err) => consoleErrors.push(err.message));
			await page.goto(`${BASE}/organizations/${orgId}/engagements`, {
				waitUntil: "networkidle",
			});
			await page.waitForSelector("main", { timeout: 10000 });
			if (consoleErrors.length > 0)
				throw new Error(`EngagementManagementPage threw: ${consoleErrors.join("; ")}`);
			console.log("OK  EngagementManagementPage renders without errors (nullable volunteerId fix)");
		} finally {
			await browser.close();
		}

		console.log("\nALL CHECKS PASSED");
	} finally {
		if (opportunityId) {
			await api(olafToken, "DELETE", `/v1/volunteer-opportunities/${opportunityId}`);
			console.log(`OK  Cleaned up opportunity ${opportunityId}`);
		}
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
