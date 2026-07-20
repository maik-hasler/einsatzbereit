/**
 * Smoke test for the color-contrast a11y fix that came out of PR #772's CI
 * (issue #763's org directory work): axe-core flagged text-gray-400 meta
 * text on white cards (~2.54:1) as a "serious" WCAG AA violation. Fixed by
 * switching to text-gray-500 (~4.83:1), matching the convention already
 * used elsewhere (e.g. SettingsWidget.tsx, smoke-test-659-contrast.mjs).
 *
 * Verifies both spots that were changed, driving the UI the same way a real
 * user (and backend/tests/VisualTests/OrganizationTests.cs's
 * Directory_ShowsOpenOpportunityCount_ForOrgWithPublishedOpportunity test)
 * would - the `frontend` client has ROPC disabled, so unlike some older
 * scripts in this directory this can't shortcut data setup through a
 * password-grant token request:
 *   1. Engagement management page's "Received: {date}" meta line.
 *   2. The public organization directory card's city line (added by #763,
 *      same defect).
 *
 * Run: node scripts/smoke-test-772-a11y-contrast.mjs
 */

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function loginAsUser(page, username, password) {
	// The app's LanguageDetector falls back to the browser's locale when
	// nothing is in localStorage yet; force English so the text selectors
	// below (which mirror the English-locale VisualTests C# suite) match
	// regardless of the machine this script runs on.
	await page.addInitScript(() => localStorage.setItem("i18nextLng", "en"));
	await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	if ((await signInBtn.count()) > 0) {
		await signInBtn.first().click();
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await loginKeycloak(page, username, password);
	}
	await page.waitForSelector("main", { timeout: 15000 });
}

async function assertContrastFixed(locator, label) {
	await locator.waitFor({ timeout: 15000 });
	const cls = await locator.getAttribute("class");
	if (cls?.includes("text-gray-400")) {
		throw new Error(
			`${label} still uses the low-contrast text-gray-400 class="${cls}"`,
		);
	}
	if (!cls?.includes("text-gray-500")) {
		throw new Error(
			`Expected ${label} to use text-gray-500, got class="${cls}"`,
		);
	}
	const color = await locator.evaluate((el) => getComputedStyle(el).color);
	console.log(`OK  ${label} uses text-gray-500 (computed color: ${color})`);
}

// Mirrors OrganizationTests.cs's CreateOrganizationAsync helper: create via
// the org switcher's "Create organization" entry, reachable from within any
// org the caller already organizes (olaf's seed data always has one).
async function createOrganizationViaUi(page, namePrefix, city) {
	const orgName = `${namePrefix} ${Date.now()}`;

	const overviewCta = page.getByRole("link", { name: "Organization overview" });
	await overviewCta.first().waitFor({ timeout: 15000 });
	await overviewCta.first().click();
	await page.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });

	await page.getByRole("button", { name: "Switch organization" }).click();
	await page.getByRole("button", { name: "Create organization" }).click();

	const dialog = page.getByRole("dialog");
	await dialog.waitFor({ state: "visible", timeout: 10000 });
	await dialog.locator("input[type='text']").first().fill(orgName);
	// The address sub-fields are all required together once any one of them
	// is filled in (CreateAddressRequest has no optional fields) - a city
	// alone triggers "Street must not be empty."
	await page.locator("#create-org-street").fill("Teststrasse");
	await page.locator("#create-org-house-number").fill("1");
	await page.locator("#create-org-zip").fill("12345");
	await page.locator("#create-org-city").fill(city);
	await page.getByTestId("modal-submit").click();

	await page
		.getByRole("button", { name: "Switch organization" })
		.filter({ hasText: orgName })
		.waitFor({ timeout: 15000 });
	await page.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });

	const match = page.url().match(/\/app\/([^/]+)\/dashboard/);
	if (!match) throw new Error(`Could not extract org id from URL: ${page.url()}`);
	return { orgId: match[1], orgName };
}

// Mirrors the wizard steps OrganizationTests.cs's
// Directory_ShowsOpenOpportunityCount test drives: IndividualContact +
// remote, so the opportunity can publish with no time slots required.
async function createOpportunityViaUi(page, title) {
	const createBtn = page.getByRole("button", { name: "Create opportunity" });
	await createBtn.first().waitFor({ timeout: 15000 });
	await createBtn.first().click();

	await page.waitForSelector("[role='dialog']", { timeout: 5000 });
	await page.locator("#opportunity-title").fill(title);
	await page
		.locator("#opportunity-description")
		.fill("Automated smoke test opportunity for the a11y contrast fix.");

	await page.getByTestId("wizard-stepper-2").click();
	await page.locator("#opportunity-remote").check();

	await page.getByTestId("wizard-stepper-3").click();
	await page
		.locator("label:has(input[name='participationType'][value='IndividualContact'])")
		.click();

	await page.getByTestId("wizard-stepper-4").click();
	await page.getByTestId("modal-submit").click();
	await page.locator("[role='dialog']").waitFor({ state: "hidden", timeout: 30000 });
}

async function findManageApplicationsHref(page, orgId, title) {
	await page.goto(`${BASE}/app/${orgId}/opportunities`, {
		waitUntil: "networkidle",
	});
	const row = page.locator("li").filter({ hasText: title }).first();
	await row.waitFor({ timeout: 15000 });
	const link = row.getByRole("link", { name: "Manage applications" });
	await link.waitFor({ timeout: 10000 });
	const href = await link.getAttribute("href");
	const match = href?.match(/\/opportunities\/([^/]+)\/engagements/);
	if (!match) throw new Error(`Could not extract opportunity id from href: ${href}`);
	return match[1];
}

// Deleting the opportunity first also cancels its active engagement
// (#548) - deleting the organization directly would otherwise be rejected
// with "Organization.HasBlockingOpportunities".
async function cleanupViaUi(page, orgId, opportunityId) {
	await page.goto(`${BASE}/app/${orgId}/opportunities`, { waitUntil: "networkidle" });
	const row = page
		.locator("li")
		.filter({ has: page.locator(`[href*="${opportunityId}"]`) })
		.first();
	await row.getByTestId("opportunity-delete").click();
	await page.getByRole("button", { name: "Yes, delete" }).click();
	await page.getByRole("dialog").waitFor({ state: "hidden", timeout: 10000 });

	await page.goto(`${BASE}/app/${orgId}/settings`, { waitUntil: "networkidle" });
	await page.getByRole("button", { name: "Delete Organization" }).click();
	await page.getByRole("button", { name: "Yes, delete" }).click();
	await page.getByRole("dialog").waitFor({ state: "hidden", timeout: 10000 });
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const { browser: olafBrowser, page: olafPage } = await launchLiveBrowser();
	let orgId, orgName, opportunityId;
	const title = `Smoke772Contrast Opportunity ${Date.now()}`;

	try {
		await loginAsUser(olafPage, "olaf", "olaf123");
		({ orgId, orgName } = await createOrganizationViaUi(
			olafPage,
			"Smoke772Contrast Org",
			"Smoketestdorf",
		));
		console.log(`OK  Created organization "${orgName}" with city "Smoketestdorf"`);

		await createOpportunityViaUi(olafPage, title);
		opportunityId = await findManageApplicationsHref(olafPage, orgId, title);
		console.log(`OK  Created opportunity ${opportunityId}`);
	} finally {
		await olafBrowser.close();
	}

	{
		const { browser: veraBrowser, page: veraPage } = await launchLiveBrowser();
		try {
			await loginAsUser(veraPage, "vera", "vera123");
			await veraPage.goto(`${BASE}/volunteer-opportunities/${opportunityId}`, {
				waitUntil: "networkidle",
			});
			await veraPage.getByRole("button", { name: "Express interest" }).click();
			await veraPage.locator("#sign-up-message").fill(
				"Smoke test application for the a11y contrast fix.",
			);
			await veraPage.getByRole("button", { name: "Sign up" }).click();
			await veraPage
				.getByRole("dialog")
				.waitFor({ state: "hidden", timeout: 15000 });
			console.log("OK  Vera applied - produced an engagement with a Received date");
		} finally {
			await veraBrowser.close();
		}
	}

	{
		const { browser, page } = await launchLiveBrowser();
		try {
			// --- 1. Engagement management page "Received:" meta line ---
			await loginAsUser(page, "olaf", "olaf123");
			await page.goto(
				`${BASE}/app/${orgId}/opportunities/${opportunityId}/engagements`,
				{ waitUntil: "networkidle" },
			);
			const receivedLine = page.getByText(/Received:/).first();
			await assertContrastFixed(receivedLine, '"Received" meta line');

			// --- 2. Organization directory card's city line ---
			await page.goto(`${BASE}/organizations`, { waitUntil: "networkidle" });
			const searchBox = page.locator("#organizations-search");
			await searchBox.waitFor({ timeout: 15000 });
			await searchBox.fill(orgName);
			const orgCard = page.locator("li").filter({ hasText: orgName });
			await orgCard.waitFor({ timeout: 15000 });
			const cityLine = orgCard.getByText("Smoketestdorf", { exact: true });
			await assertContrastFixed(cityLine, "Organization directory city line");

			await cleanupViaUi(page, orgId, opportunityId);
			console.log("OK  Cleaned up throwaway opportunity and organization");
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
