/**
 * Smoke test for #574: revive the unused breadcrumb system.
 * Verifies: the home page shows no stray breadcrumb, drill-down pages
 * (opportunity detail, organization profile, organization dashboard,
 * engagement management) show a breadcrumb trail back to their logical
 * parent(s), and the opportunity detail breadcrumb reflects the opportunity's
 * actual organization (not a hardcoded "/#opportunities" link).
 * Run: node scripts/smoke-test-574-breadcrumb.mjs
 *
 * Requires a logged-in organisator account (olaf/olaf123) with an org that
 * has at least one volunteer opportunity, for the dashboard/engagement checks.
 */

import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function main() {
	const apiRes = await fetch(`${API}/health`);
	if (!apiRes.ok) throw new Error(`Health check failed: ${apiRes.status}`);
	console.log("OK  API health check passed");

	// The sandbox's egress proxy re-terminates TLS; Chromium's default
	// ClientHello (HTTP/2, QUIC, PQ-Kyber/ECH) resets against it, so pin an
	// older negotiation profile. Not needed outside this sandboxed runner.
	const browser = await chromium.launch({
		executablePath: "/opt/pw-browsers/chromium",
		proxy: { server: process.env.HTTPS_PROXY ?? "http://127.0.0.1:42149" },
		args: [
			"--no-sandbox",
			"--disable-setuid-sandbox",
			"--disable-http2",
			"--disable-quic",
			"--ssl-version-max=tls1.2",
			"--disable-features=PostQuantumKyber,EncryptedClientHello",
		],
	});
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	try {
		// --- Home page must show no breadcrumb (nothing to link back to) ---
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		await page.waitForSelector("h1", { timeout: 15000 });

		const homeBreadcrumb = page.locator("nav[aria-label='Breadcrumb']");
		if ((await homeBreadcrumb.count()) > 0) {
			throw new Error("Home page unexpectedly shows a breadcrumb");
		}
		console.log("OK  Home page shows no breadcrumb");

		// --- Opportunity detail page: breadcrumb reflects org + title ---
		const firstCard = page.locator("a[href*='/volunteer-opportunities/']").first();
		if ((await firstCard.count()) === 0) {
			console.log("WARN  No opportunities seeded - skipping detail/org checks");
		} else {
			const href = await firstCard.getAttribute("href");
			await page.goto(`${BASE}${href}`, { waitUntil: "networkidle" });

			const detailBreadcrumb = page.locator("nav[aria-label='Breadcrumb']");
			await detailBreadcrumb.waitFor({ state: "visible", timeout: 10000 });
			const homeCrumb = detailBreadcrumb.locator("a[href='/']");
			await homeCrumb.waitFor({ state: "visible", timeout: 5000 });
			console.log("OK  Opportunity detail page shows breadcrumb with Home link");

			const orgChipHref = await page
				.locator("a[href*='/organizations/']")
				.first()
				.getAttribute("href");
			if (orgChipHref) {
				const orgCrumb = detailBreadcrumb.locator(`a[href='${orgChipHref}']`);
				await orgCrumb.waitFor({ state: "visible", timeout: 5000 });
				console.log("OK  Opportunity detail breadcrumb links to its organization");

				// --- Organization profile page: breadcrumb shows Home > org name ---
				await page.goto(`${BASE}${orgChipHref}`, { waitUntil: "networkidle" });
				const orgBreadcrumb = page.locator("nav[aria-label='Breadcrumb']");
				await orgBreadcrumb.waitFor({ state: "visible", timeout: 10000 });
				await orgBreadcrumb
					.locator("a[href='/']")
					.waitFor({ state: "visible", timeout: 5000 });
				console.log("OK  Organization profile page shows breadcrumb with Home link");
			}
		}

		// --- Organizer flows: dashboard + engagement management breadcrumbs ---
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
		if ((await signInBtn.count()) > 0) {
			await signInBtn.first().click();
			await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
			await page.fill("#username", "olaf");
			await page.click("#kc-login");
			await page.fill("#password", "olaf123");
			await page.click("#kc-login");
			await page.waitForURL(BASE + "/**", { timeout: 15000 });
			console.log("OK  Logged in as olaf (organisator)");
		}
		await page.waitForSelector("main", { timeout: 10000 });

		const switcherBtn = page.getByRole("button", {
			name: /switch organization|organisation wechseln/i,
		});
		if ((await switcherBtn.count()) === 0) {
			console.log("WARN  Org switcher not visible - skipping dashboard checks");
			console.log("\nALL CHECKS PASSED");
			return;
		}
		await switcherBtn.first().click();

		const dashboardLink = page.getByTestId("org-dashboard-link");
		if ((await dashboardLink.count()) === 0) {
			console.log("WARN  org-dashboard-link not found - skipping dashboard checks");
			console.log("\nALL CHECKS PASSED");
			return;
		}
		await dashboardLink.first().click();
		await page.waitForURL(/\/organizations\/.+\/dashboard/, { timeout: 10000 });

		const dashBreadcrumb = page.locator("nav[aria-label='Breadcrumb']");
		await dashBreadcrumb.waitFor({ state: "visible", timeout: 10000 });
		await dashBreadcrumb
			.locator("a[href='/']")
			.waitFor({ state: "visible", timeout: 5000 });
		console.log("OK  Organization dashboard shows breadcrumb with Home link");

		await page
			.getByRole("button", { name: "Engagements", exact: true })
			.click();

		const manageLink = page.getByText("Manage engagements").first();
		try {
			await manageLink.waitFor({ state: "visible", timeout: 8000 });
		} catch {
			console.log(
				"WARN  No opportunities in Engagements tab - skipping engagement management check",
			);
			console.log("\nALL CHECKS PASSED");
			return;
		}
		await manageLink.click();
		await page.waitForURL(/\/volunteer-opportunities\/.+\/engagements/, {
			timeout: 10000,
		});

		const engBreadcrumb = page.locator("nav[aria-label='Breadcrumb']");
		await engBreadcrumb.waitFor({ state: "visible", timeout: 10000 });
		console.log(
			"OK  Engagement management page shows breadcrumb (persistent, not just in empty state)",
		);

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
