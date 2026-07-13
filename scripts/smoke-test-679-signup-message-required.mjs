// Smoke test for #679: SignUpModal's "Message" textarea on a non-waitlist
// ("Express interest") sign-up was silently HTML-required with no visual or
// accessible indication - clicking "Sign up" with it empty produced only the
// browser's native validation popup. Fixed by adding "(required)" to the
// label text and associating the label with the textarea via htmlFor/id.
//
// Run: node scripts/smoke-test-679-signup-message-required.mjs

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

	const createRes = await fetch(`${API}/v1/volunteer-opportunities`, {
		method: "POST",
		headers: authHeaders,
		body: JSON.stringify({
			title: `Smoke679 MessageRequired ${Date.now()}`,
			description: "Automated smoke test opportunity for #679.",
			organizationId: orgId,
			isRemote: true,
			occurrence: "OneTime",
			participationType: "IndividualContact",
			checkInMethod: "None",
			isDraft: false,
		}),
	});
	if (!createRes.ok)
		throw new Error(
			`Create opportunity failed: ${createRes.status} ${await createRes.text()}`,
		);
	const opportunity = await createRes.json();
	console.log(`OK  Created throwaway opportunity ${opportunity.id}`);

	try {
		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "vera", "vera123");
			console.log("OK  Logged in as vera");

			await page.goto(`${BASE}/volunteer-opportunities/${opportunity.id}`, {
				waitUntil: "networkidle",
			});
			await page
				.getByRole("button", { name: /express interest|interesse bekunden/i })
				.click();
			const dialog = page.getByRole("dialog");
			await dialog.waitFor({ state: "visible", timeout: 15000 });
			console.log("OK  Sign-up modal opened for a non-waitlist opportunity");

			const messageField = dialog.getByLabel(/message \(required\)/i);
			await messageField.waitFor({ state: "visible", timeout: 10000 });
			console.log(
				'OK  Message field has an accessible label including "(required)"',
			);

			const labelText = (
				await dialog.locator('label[for="sign-up-message"]').textContent()
			)?.trim();
			if (!labelText || !/required/i.test(labelText)) {
				throw new Error(
					`Expected the visible label text to mention "required", got: "${labelText}"`,
				);
			}
			console.log(`OK  Visible label text: "${labelText}"`);

			const isRequired = await messageField.evaluate((el) => el.required);
			if (!isRequired)
				throw new Error(
					"Message textarea lost its native required attribute",
				);
			console.log("OK  Message textarea still carries the required attribute");

			// Submitting empty must not silently no-op - the browser's native
			// validation should keep the dialog open without an app-side error.
			await dialog.getByRole("button", { name: /^sign up$|^anmelden$/i }).click();
			await page.waitForTimeout(500);
			await dialog.waitFor({ state: "visible", timeout: 5000 });
			console.log(
				"OK  Submitting with an empty (but now visibly-required) message keeps the dialog open",
			);

			await messageField.fill("Smoke test message for #679.");
			const [signupRes] = await Promise.all([
				page.waitForResponse(
					(r) =>
						r
							.url()
							.includes(
								`/volunteer-opportunities/${opportunity.id}/engagements`,
							) && r.request().method() === "POST",
				),
				dialog.getByRole("button", { name: /^sign up$|^anmelden$/i }).click(),
			]);
			if (!signupRes.ok())
				throw new Error(`Sign-up POST failed: ${signupRes.status()}`);
			console.log("OK  Sign-up succeeds once the required message is filled in");
		} finally {
			await browser.close();
		}
	} finally {
		await fetch(`${API}/v1/volunteer-opportunities/${opportunity.id}`, {
			method: "DELETE",
			headers: authHeaders,
		});
		console.log("OK  Cleaned up throwaway opportunity");
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
