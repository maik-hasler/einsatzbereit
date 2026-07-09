// Smoke test for the #644 follow-up found during the 2026-07-08
// persona-simulation cycle after PR #646 shipped: VolunteerOpportunityDetailPage's
// load() re-fires as the OIDC token resolves after a hard/direct navigation,
// but had no request-ordering guard. An earlier-sent, unauthenticated GET
// could resolve *after* a later, authenticated GET and silently overwrite the
// correct currentUserEngagement with null via setOpportunity - making the
// "already applied" status intermittently revert to "Express interest".
//
// Fixed in PR #647 with a latestRequestRef counter: any .then()/.catch()/
// .finally() callback whose request id no longer matches the latest issued
// request is dropped, so only the most-recently-*sent* request's result is
// ever applied, regardless of resolution order.
//
// This script verifies the fix two ways:
//   1. Deterministically, by delaying unauthenticated GET responses to the
//      details endpoint via Playwright route interception, forcing the exact
//      out-of-order race described above.
//   2. As a black-box repro of the original persona-sim finding: several
//      consecutive hard navigations to the same URL, same as the manual repro
//      that caught this on rc.210.
//
// Run: node scripts/smoke-test-644-followup-race-condition.mjs

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

async function createOpportunity(token, orgId, title) {
	const res = await fetch(`${API}/v1/volunteer-opportunities`, {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify({
			title,
			description: "Automated smoke test opportunity for #644 follow-up.",
			organizationId: orgId,
			isRemote: true,
			occurrence: "OneTime",
			participationType: "IndividualContact",
			checkInMethod: "None",
			isDraft: false,
		}),
	});
	if (!res.ok)
		throw new Error(
			`Create opportunity failed: ${res.status} ${await res.text()}`,
		);
	return res.json();
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

async function applyToOpportunity(page, opportunityId, message) {
	const detailUrl = `${BASE}/volunteer-opportunities/${opportunityId}`;
	await page.goto(detailUrl, { waitUntil: "networkidle" });
	await page
		.getByRole("button", { name: /express interest|interesse bekunden/i })
		.click();
	await page.fill("textarea", message);
	const [signupRes] = await Promise.all([
		page.waitForResponse(
			(r) =>
				r
					.url()
					.includes(`/volunteer-opportunities/${opportunityId}/engagements`) &&
				r.request().method() === "POST",
		),
		page.getByRole("button", { name: /^sign up$|^anmelden$/i }).click(),
	]);
	if (!signupRes.ok())
		throw new Error(`Sign-up POST failed: ${signupRes.status()}`);
	return detailUrl;
}

async function assertAlreadyAppliedState(page, detailUrl, label) {
	await page.goto(detailUrl, { waitUntil: "networkidle" });
	await page.waitForSelector("h1", { timeout: 10000 });
	await page
		.getByText(/your application|deine bewerbung/i)
		.waitFor({ timeout: 10000 });
	const ctaButton = page.getByRole("button", {
		name: /express interest|interesse bekunden/i,
	});
	if ((await ctaButton.count()) > 0) {
		throw new Error(
			`${label}: apply button re-appeared even though the volunteer already applied`,
		);
	}
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok)
		throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const olafToken = await getToken("olaf", "olaf123");
	const orgsRes = await fetch(`${API}/v1/organizations`, {
		headers: { Authorization: `Bearer ${olafToken}` },
	});
	if (!orgsRes.ok)
		throw new Error(`GET /organizations failed: ${orgsRes.status}`);
	const orgs = await orgsRes.json();
	if (!Array.isArray(orgs) || orgs.length === 0)
		throw new Error("olaf has no organizations - cannot run this smoke test");
	const orgId = orgs[0].id;

	// === Test 1: deterministic race - delay unauthenticated GET responses so
	// they resolve after a later, authenticated GET ===
	{
		const oppRace = await createOpportunity(
			olafToken,
			orgId,
			`Smoke644Followup RaceGuard ${Date.now()}`,
		);
		console.log(`OK  Created opportunity for race-guard test ${oppRace.id}`);

		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "vera", "vera123");
			console.log("OK  Logged in as vera");

			const detailUrl = await applyToOpportunity(
				page,
				oppRace.id,
				"Applying via race-guard smoke check.",
			);
			console.log("OK  Applied to race-guard opportunity");

			await page.route(
				`**/v1/volunteer-opportunities/${oppRace.id}`,
				async (route) => {
					const headers = route.request().headers();
					if (route.request().method() === "GET" && !headers.authorization) {
						await new Promise((resolve) => setTimeout(resolve, 1500));
					}
					await route.continue();
				},
			);

			await assertAlreadyAppliedState(
				page,
				detailUrl,
				"Deterministic race guard",
			);
			console.log(
				"OK  Already-applied state survives an out-of-order unauthenticated response",
			);
		} finally {
			await browser.close();
		}
	}

	// === Test 2: black-box repro - several consecutive hard navigations, same
	// as the manual repro that caught this on rc.210 ===
	{
		const oppRepeat = await createOpportunity(
			olafToken,
			orgId,
			`Smoke644Followup RepeatedNav ${Date.now()}`,
		);
		console.log(
			`OK  Created opportunity for repeated-navigation test ${oppRepeat.id}`,
		);

		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "vera", "vera123");

			const detailUrl = await applyToOpportunity(
				page,
				oppRepeat.id,
				"Applying via repeated-navigation smoke check.",
			);
			console.log("OK  Applied to repeated-navigation opportunity");

			const attempts = 5;
			for (let i = 1; i <= attempts; i++) {
				await assertAlreadyAppliedState(
					page,
					detailUrl,
					`Hard navigation attempt ${i}/${attempts}`,
				);
			}
			console.log(
				`OK  Already-applied state survived ${attempts} consecutive hard navigations`,
			);
		} finally {
			await browser.close();
		}
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
