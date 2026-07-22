// Smoke test for PR #783/#787: the pathless "dashboard" parent route added
// to nest opportunities/members/settings under /app/:organizationId/dashboard/...
// rendered a bare <Outlet /> with no `context` prop, which starts its own
// outlet context instead of forwarding OrgAppLayout's
// <Outlet context={{org, reloadOrg}}>. Every page nested under it got
// undefined from useOutletContext<OrgAppContext>() and crashed on the first
// destructure, caught by the app-wide ErrorBoundary ("Something went wrong").
//
// Verifies the dashboard and its nested opportunities/members/settings pages
// all render real content instead of the ErrorBoundary fallback.
//
// No throwaway data is created (an existing org from seed data is used), so
// there is nothing to clean up (see #630).
// Run: node scripts/smoke-test-783-dashboard-outlet-context.mjs

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function assertNoErrorBoundary(page, label) {
	const crashed = await page
		.getByRole("heading", { name: "Something went wrong" })
		.count();
	if (crashed > 0) {
		throw new Error(`${label}: ErrorBoundary fallback is showing (crash)`);
	}
	console.log(`OK  ${label}: no ErrorBoundary crash`);
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const { browser, page } = await launchLiveBrowser();
	try {
		await page.goto(BASE, { waitUntil: "networkidle" });
		await page.click("text=/sign in|anmelden/i");
		await page.waitForURL(/\/realms\//, { timeout: 30000 });
		await loginKeycloak(page, "olaf", "olaf123");

		await page.waitForURL(`${BASE}/`, { timeout: 30000 });

		const cta = page.getByRole("link", { name: "Organization overview" });
		await cta.first().waitFor({ timeout: 25000 });
		await cta.first().click();

		await page.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });
		console.log("OK  Navigated to org dashboard");
		await assertNoErrorBoundary(page, "Dashboard");

		const createOpportunityBtn = page.getByRole("button", {
			name: "Create opportunity",
		});
		await createOpportunityBtn.first().waitFor({ timeout: 15000 });
		console.log("OK  Dashboard rendered real content (Create opportunity widget)");

		const orgIdMatch = page.url().match(/\/app\/([^/]+)\/dashboard/);
		if (!orgIdMatch) throw new Error("Could not extract organizationId from URL");
		const orgId = orgIdMatch[1];

		for (const tab of ["opportunities", "members", "settings"]) {
			await page.goto(`${BASE}/app/${orgId}/dashboard/${tab}`, {
				waitUntil: "networkidle",
			});
			await assertNoErrorBoundary(page, `dashboard/${tab}`);
		}

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
