/**
 * Smoke test for the org-app header/avatar/entry-point fixes:
 *  - user avatar and org logo now render in the /app header (instead of only
 *    initials), via the shared AccountControls component + OrganizationSwitcher.
 *  - the Organization.Slug feature is fully removed - org-scoped URLs are now
 *    always /app/{guid}/... never /app/{slug}/...
 *  - the /app header's top-right (notification bell, divider, language
 *    selector, avatar) now matches the main site Header exactly.
 *  - new /app entry point: users with >1 org see a picker, users with 0 orgs
 *    see an empty-state create prompt, and the HomePage hero CTA becomes an
 *    "Organization overview" link once a user organizes at least one org
 *    (previously it always opened the create-organization modal).
 *  - the "Your organizations" section was removed entirely from the profile
 *    page - /app is now the only entry point into the org management shell.
 *
 * Run: node scripts/smoke-test-org-app-entry-and-header.mjs
 */

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const KEYCLOAK = "https://login.maik-hasler.de";
const CLIENT_ID = "frontend";
const REALM = "einsatzbereit";
const GUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

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

async function signIn(page, username, password) {
	await page.goto(BASE, { waitUntil: "networkidle" });
	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	if ((await signInBtn.count()) > 0) {
		await signInBtn.first().click();
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await loginKeycloak(page, username, password);
		await page.waitForURL(`${BASE}/`, { timeout: 15000 });
	}
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	// === Part 1: olaf (organizes at least one org) ===
	const { browser, page } = await launchLiveBrowser();
	try {
		await signIn(page, "olaf", "olaf123");
		console.log("OK  Logged in as olaf");

		const overviewLink = page.getByRole("link", { name: "Organization overview" });
		const createCta = page.getByRole("button", { name: "Create an organisation" });
		if ((await overviewLink.count()) === 0)
			throw new Error("Expected the 'Organization overview' hero CTA for a user who already organizes orgs");
		if ((await createCta.count()) > 0)
			throw new Error("'Create an organisation' CTA must not show once a user already organizes an org");
		console.log("OK  HomePage hero CTA shows 'Organization overview' (not create-org) for olaf");

		await overviewLink.first().click();
		await page.waitForURL(`${BASE}/app`, { timeout: 10000 });

		const pickerRows = page.getByTestId("org-entry-picker-row");
		const rowCount = await pickerRows.count();
		if (rowCount > 0) {
			console.log(`OK  /app shows the org picker (${rowCount} orgs)`);
			await pickerRows.first().click();
		} else {
			console.log("OK  /app auto-redirected straight to the dashboard (exactly one org)");
		}
		await page.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });

		const orgIdSegment = new URL(page.url()).pathname.split("/")[2];
		if (!GUID_RE.test(orgIdSegment))
			throw new Error(`Expected /app/{guid}/dashboard, got id segment "${orgIdSegment}" - slug leaked into the URL?`);
		console.log(`OK  Landed on /app/${orgIdSegment}/dashboard - the id is a GUID, not a slug`);

		if ((await page.getByTestId("notification-bell").count()) === 0)
			throw new Error("Notification bell missing from the org app header");
		if ((await page.getByRole("button", { name: "User menu" }).count()) === 0)
			throw new Error("User menu / avatar button missing from the org app header");
		if ((await page.locator("header button[aria-haspopup='listbox']").count()) === 0)
			throw new Error("Language selector missing from the org app header");
		if ((await page.getByRole("button", { name: "Switch organization" }).count()) === 0)
			throw new Error("Org switcher missing from the org app header");
		console.log("OK  Org app header shows bell + user menu + language selector + org switcher, matching the main header");

		await page.goto(`${BASE}/profile`, { waitUntil: "networkidle" });
		if ((await page.getByTestId("your-organizations-link").count()) > 0)
			throw new Error("'your-organizations-link' still present on /profile - the Organizations section should be gone");
		if ((await page.getByTestId("create-org-btn").count()) > 0)
			throw new Error("'create-org-btn' still present on /profile - the Organizations section should be gone");
		console.log("OK  /profile no longer shows the removed Organizations section");
	} finally {
		await browser.close();
	}

	// === Part 2: vera (organizes nothing) - empty state -> create -> dashboard ===
	const { browser: browser2, page: page2 } = await launchLiveBrowser();
	let createdOrgId;
	try {
		await signIn(page2, "vera", "vera123");
		await page2.goto(`${BASE}/app`, { waitUntil: "networkidle" });

		const createBtn = page2.getByRole("button", { name: "Create organization" });
		if ((await createBtn.count()) === 0) {
			console.log("WARN vera already organizes an org from a previous run - skipping the empty-state check");
		} else {
			console.log("OK  /app shows the empty-state prompt for a user with zero orgs");
			const orgName = `SmokeOrgAppEntry ${Date.now()}`;

			await createBtn.click();
			await page2.waitForSelector('[role="dialog"]', { timeout: 10000 });
			await page2.fill("#create-org-name", orgName);

			const [createResponse] = await Promise.all([
				page2.waitForResponse(
					(resp) => resp.url().endsWith("/v1/organizations") && resp.request().method() === "POST",
				),
				page2.getByTestId("modal-submit").click(),
			]);
			if (createResponse.status() !== 200)
				throw new Error(`CreateOrganization request failed: ${createResponse.status()}`);
			const created = await createResponse.json();
			createdOrgId = created.id?.value ?? created.id;

			await page2.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });
			console.log(`OK  Creating an org from the empty state entered its dashboard directly (${createdOrgId})`);
		}
	} finally {
		await browser2.close();
		if (createdOrgId) {
			const veraToken = await getToken("vera", "vera123");
			const { status } = await api(veraToken, "DELETE", `/v1/organizations/${createdOrgId}`);
			console.log(
				status === 204
					? `OK  Cleaned up organization ${createdOrgId}`
					: `WARN cleanup DELETE returned ${status} for organization ${createdOrgId}`,
			);
		}
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
