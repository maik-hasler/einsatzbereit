/**
 * Smoke test for #718: restore Organization creation after the DDD refactor
 * (#716) and the organization-creation-fields feature (#715) collided on
 * main.
 *
 * #715 added description/contact email/phone/website/address to org
 * creation via `organization.Update(...)` + `new Address(...)`. #716,
 * merged first, replaced both with `Rename`/`ChangeDescription`/
 * `ChangeContactInfo`/`Relocate` and `Address.Create(...) -> Result`.
 * When #715 was merged on top afterwards it broke the build entirely
 * (CS1729/CS1061), so this exercises the fixed handler end-to-end: fill
 * every new field in the live `CreateOrganizationModal`, submit, and
 * confirm they actually persisted (not just an in-memory response echo).
 *
 * Run: node scripts/smoke-test-718-org-create-fields.mjs
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

async function api(token, method, path) {
	const res = await fetch(`${API}${path}`, {
		method,
		headers: token ? { Authorization: `Bearer ${token}` } : {},
	});
	const text = await res.text();
	const json = text ? JSON.parse(text) : null;
	return { status: res.status, body: json };
}

const fields = {
	description: `Smoke718 automated org description ${Date.now()}`,
	contactEmail: "smoke718@example.com",
	contactPhone: "+49 441 1234567",
	website: "https://example.com/smoke718",
	street: "Smoke718 Street",
	houseNumber: "42a",
	zipCode: "26122",
	city: "Oldenburg",
};

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const olafToken = await getToken("olaf", "olaf123");
	const orgName = `Smoke718 Org ${Date.now()}`;

	const { browser, page } = await launchLiveBrowser();
	let organizationId;
	try {
		await page.goto(`${BASE}/profile`, { waitUntil: "networkidle" });
		const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
		if ((await signInBtn.count()) > 0) {
			await signInBtn.first().click();
			await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
			await loginKeycloak(page, "olaf", "olaf123");
			await page.waitForURL(`${BASE}/profile`, { timeout: 15000 });
		}
		console.log("OK  Logged in as olaf and reached /profile");

		await page.getByTestId("create-org-btn").click();
		await page.waitForSelector('[role="dialog"]', { timeout: 10000 });
		console.log("OK  CreateOrganizationModal opened");

		await page.fill("#create-org-name", orgName);
		await page.fill("#create-org-description", fields.description);
		await page.fill("#create-org-contact-email", fields.contactEmail);
		await page.fill("#create-org-phone", fields.contactPhone);
		await page.fill("#create-org-website", fields.website);
		await page.fill("#create-org-street", fields.street);
		await page.fill("#create-org-house-number", fields.houseNumber);
		await page.fill("#create-org-zip", fields.zipCode);
		await page.fill("#create-org-city", fields.city);

		const [createResponse] = await Promise.all([
			page.waitForResponse(
				(resp) =>
					resp.url().endsWith("/v1/organizations") &&
					resp.request().method() === "POST",
			),
			page.getByTestId("modal-submit").click(),
		]);
		if (createResponse.status() !== 200) {
			throw new Error(
				`CreateOrganization request failed: ${createResponse.status()} ${await createResponse.text()}`,
			);
		}
		const created = await createResponse.json();
		organizationId = created.id?.value ?? created.id;
		if (!organizationId) throw new Error(`No organization id in response: ${JSON.stringify(created)}`);
		console.log(`OK  Submitted form, organization created (${organizationId})`);

		await page.waitForSelector('[role="dialog"]', { state: "detached", timeout: 10000 });
		console.log("OK  Modal closed after successful submit (no ResultFailureException from Update()/new Address(...))");

		// === Confirm the fields actually persisted, not just an in-memory echo ===
		const { status, body } = await api(olafToken, "GET", `/v1/organizations/${organizationId}`);
		if (status !== 200) throw new Error(`GetOrganizationDetails failed: ${status} ${JSON.stringify(body)}`);

		const checks = [
			["name", body.name, orgName],
			["description", body.description, fields.description],
			["contactEmail", body.contactEmail, fields.contactEmail],
			["contactPhone", body.contactPhone, fields.contactPhone],
			["website", body.website, fields.website],
			["address.street", body.address?.street, fields.street],
			["address.houseNumber", body.address?.houseNumber, fields.houseNumber],
			["address.zipCode", body.address?.zipCode, fields.zipCode],
			["address.city", body.address?.city, fields.city],
		];
		for (const [label, actual, expected] of checks) {
			if (actual !== expected)
				throw new Error(`Expected persisted ${label} === ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
		}
		console.log("OK  All fields (description, contact email/phone, website, address) persisted exactly as submitted");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
		if (organizationId) {
			const { status } = await api(olafToken, "DELETE", `/v1/organizations/${organizationId}`);
			console.log(
				status === 204
					? `OK  Cleaned up organization ${organizationId}`
					: `WARN cleanup DELETE returned ${status} for organization ${organizationId}`,
			);
		}
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
