// Smoke test for #644 and #645, both filed by the 2026-07-08 persona-simulation
// cycle and fixed together in the same PR:
//
// #644 - VolunteerOpportunityDetailPage's details-fetch effect only depended
// on opportunityId, so a hard/direct navigation (fresh page load) could race
// the OIDC token restoring from storage and fetch unauthenticated, silently
// losing the volunteer's "already applied" status for the rest of the page's
// lifetime. Separately, SignUpModal's error handling checked
// `err instanceof Error`, but the NSwag client throws a raw ProblemDetails
// object on API errors, so a genuine 409 conflict always fell back to a
// generic "Unknown error" instead of the backend's actual message.
//
// #645 - useAchievementNotifier only seeded the "seen" localStorage set when
// the account had zero achievements, so a fresh browser/device/profile for an
// account that already has achievements re-announced every existing
// achievement as newly unlocked.
//
// Run: node scripts/smoke-test-644-645-detail-and-achievements.mjs

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
			description: "Automated smoke test opportunity for #644.",
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

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok)
		throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	// --- Setup: create two fresh IndividualContact opportunities via olaf's org ---
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

	const oppA = await createOpportunity(
		olafToken,
		orgId,
		`Smoke644 Persistence ${Date.now()}`,
	);
	console.log(`OK  Created opportunity A (persistence test) ${oppA.id}`);
	const oppB = await createOpportunity(
		olafToken,
		orgId,
		`Smoke644 DuplicateError ${Date.now()}`,
	);
	console.log(`OK  Created opportunity B (duplicate-signup test) ${oppB.id}`);

	// === Test 1: "already applied" status survives a hard/direct navigation ===
	{
		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "vera", "vera123");
			console.log("OK  Logged in as vera");

			const detailUrl = `${BASE}/volunteer-opportunities/${oppA.id}`;
			await page.goto(detailUrl, { waitUntil: "networkidle" });
			await page
				.getByRole("button", { name: /express interest|interesse bekunden/i })
				.click();
			await page.fill(
				"textarea",
				"Smoke test signup for #644 persistence check.",
			);
			const [signupRes] = await Promise.all([
				page.waitForResponse(
					(r) =>
						r
							.url()
							.includes(`/volunteer-opportunities/${oppA.id}/engagements`) &&
						r.request().method() === "POST",
				),
				page.getByRole("button", { name: /^sign up$|^anmelden$/i }).click(),
			]);
			if (!signupRes.ok())
				throw new Error(`Sign-up POST failed: ${signupRes.status()}`);
			console.log("OK  Applied to opportunity A");

			// Genuine hard navigation (full reload), not an SPA transition - this is
			// exactly the race the fix addresses: the OIDC token may not be
			// restored from storage by the time the details fetch first fires.
			await page.goto(detailUrl, { waitUntil: "networkidle" });
			await page.waitForSelector("h1", { timeout: 10000 });

			await page
				.getByText(/your application|deine bewerbung/i)
				.waitFor({ timeout: 10000 });
			console.log(
				'OK  "Your application" status still shown after a hard navigation',
			);

			const ctaButton = page.getByRole("button", {
				name: /express interest|interesse bekunden/i,
			});
			if ((await ctaButton.count()) > 0) {
				throw new Error(
					"Apply button re-appeared after a hard navigation, even though vera already applied",
				);
			}
			console.log(
				"OK  Apply button not shown - already-applied state is authoritative",
			);
		} finally {
			await browser.close();
		}
	}

	// === Test 2: sign-up modal shows the real 409 message, not "Unknown error" ===
	{
		const { browser, context, page: page1 } = await launchLiveBrowser();
		try {
			await loginAsUser(page1, "vera", "vera123");

			const detailUrl = `${BASE}/volunteer-opportunities/${oppB.id}`;
			await page1.goto(detailUrl, { waitUntil: "networkidle" });
			await page1
				.getByRole("button", { name: /express interest|interesse bekunden/i })
				.click();
			await page1.fill("textarea", "First sign-up attempt.");

			// Second page in the same context: the OIDC user store is localStorage
			// (see main.tsx), which is shared across pages in one context, so this
			// page is already authenticated - simulates a second tab racing the
			// first to sign up for the same opportunity.
			const page2 = await context.newPage();
			await page2.goto(detailUrl, { waitUntil: "networkidle" });
			await page2
				.getByRole("button", { name: /express interest|interesse bekunden/i })
				.click();
			await page2.fill("textarea", "Second (duplicate) sign-up attempt.");

			const [firstRes] = await Promise.all([
				page1.waitForResponse(
					(r) =>
						r
							.url()
							.includes(`/volunteer-opportunities/${oppB.id}/engagements`) &&
						r.request().method() === "POST",
				),
				page1.getByRole("button", { name: /^sign up$|^anmelden$/i }).click(),
			]);
			if (!firstRes.ok())
				throw new Error(`First sign-up POST failed: ${firstRes.status()}`);
			console.log("OK  First tab's sign-up succeeded");

			const [secondRes] = await Promise.all([
				page2.waitForResponse(
					(r) =>
						r
							.url()
							.includes(`/volunteer-opportunities/${oppB.id}/engagements`) &&
						r.request().method() === "POST",
				),
				page2.getByRole("button", { name: /^sign up$|^anmelden$/i }).click(),
			]);
			if (secondRes.status() !== 409) {
				throw new Error(
					`Expected the second tab's duplicate sign-up to be rejected with 409, got ${secondRes.status()}`,
				);
			}
			console.log("OK  Second tab's duplicate sign-up correctly rejected (409)");

			const errorText = await page2
				.locator("p.text-red-600")
				.first()
				.textContent();
			if (!errorText || /unknown error|unbekannter fehler/i.test(errorText)) {
				throw new Error(
					`Sign-up modal showed a generic error instead of the backend's message: "${errorText}"`,
				);
			}
			if (!/already signed up/i.test(errorText)) {
				throw new Error(
					`Expected the modal to surface the backend's specific conflict message, got: "${errorText}"`,
				);
			}
			console.log(`OK  Modal shows the real backend error: "${errorText}"`);
		} finally {
			await browser.close();
		}
	}

	// === Test 3: achievement toasts don't re-fire on a fresh browser context ===
	{
		const { browser, page } = await launchLiveBrowser();
		try {
			// A brand new context has no einsatzbereit:seen-achievements
			// localStorage entry, simulating a new device/browser/profile for an
			// account (olaf) that already has achievements from prior activity.
			await loginAsUser(page, "olaf", "olaf123");
			console.log("OK  Logged in as olaf (fresh browser context)");

			// Confirm the precondition: olaf's account actually has achievements
			// already (otherwise this test would trivially pass for the wrong
			// reason).
			const achievementsRes = await fetch(`${API}/v1/me/achievements`, {
				headers: { Authorization: `Bearer ${olafToken}` },
			});
			if (!achievementsRes.ok)
				throw new Error(
					`GET /me/achievements failed: ${achievementsRes.status}`,
				);
			const achievements = await achievementsRes.json();
			if (!Array.isArray(achievements) || achievements.length === 0) {
				throw new Error(
					"olaf has no existing achievements - cannot verify the re-fire fix",
				);
			}
			console.log(
				`OK  Precondition: olaf already has ${achievements.length} achievement(s)`,
			);

			// The notifier's first check fires on mount; give it a moment, then
			// assert no "New badge unlocked" toast appeared for already-earned
			// badges.
			await page.waitForTimeout(3000);
			const badgeToast = page
				.getByRole("alert")
				.filter({ hasText: /new badge unlocked|neues abzeichen freigeschaltet/i });
			if ((await badgeToast.count()) > 0) {
				const text = await badgeToast.first().textContent();
				throw new Error(
					`Achievement toast re-fired for an already-earned badge on a fresh browser context: "${text}"`,
				);
			}
			console.log(
				"OK  No achievement toast re-fired for already-earned badges on a fresh context",
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
