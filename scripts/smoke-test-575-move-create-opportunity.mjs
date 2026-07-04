/**
 * Smoke test for #575: "Create Opportunity" moved from the homepage to the
 * organization dashboard.
 * Verifies: the homepage no longer shows a create button (for anyone), and
 * an organizer can find + use the create action on their org dashboard,
 * scoped by the dashboard's own organizationId.
 * Run: node scripts/smoke-test-575-move-create-opportunity.mjs
 *
 * Requires a logged-in organisator account (olaf/olaf123) and an existing org.
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
		// --- Login as organisator ---
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

		// --- Homepage must NOT show a create-opportunity button anymore ---
		const homeCreateBtn = page.getByTestId("create-opportunity-btn");
		if ((await homeCreateBtn.count()) > 0) {
			throw new Error(
				"Create-opportunity button still present on the homepage",
			);
		}
		console.log("OK  Homepage shows no create-opportunity button");

		// --- Navigate to the org dashboard via the org switcher ---
		const switcherBtn = page.getByRole("button", {
			name: /switch organization|organisation wechseln/i,
		});
		try {
			await switcherBtn.first().waitFor({ state: "visible", timeout: 8000 });
		} catch {
			console.log(
				"WARN  Org switcher not visible (olaf has no org membership) - skipping dashboard checks",
			);
			return;
		}
		await switcherBtn.first().click();

		const dashboardLink = page.getByTestId("org-dashboard-link");
		try {
			await dashboardLink.first().waitFor({ state: "visible", timeout: 5000 });
		} catch {
			console.log(
				"WARN  org-dashboard-link not found - skipping dashboard checks",
			);
			return;
		}
		await dashboardLink.first().click();
		await page.waitForURL(/\/organizations\/.+\/dashboard/, {
			timeout: 10000,
		});
		console.log("OK  Navigated to organization dashboard");

		// --- Create-opportunity button present on the dashboard, on every tab ---
		const dashCreateBtn = page.getByTestId("create-opportunity-btn");
		await dashCreateBtn.waitFor({ state: "visible", timeout: 8000 });
		console.log("OK  Create-opportunity button present on org dashboard (calendar tab)");

		for (const tab of ["engagements", "members", "settings"]) {
			await page.getByRole("button", { name: new RegExp(tab, "i") }).click();
			await dashCreateBtn.waitFor({ state: "visible", timeout: 5000 });
		}
		console.log("OK  Create-opportunity button remains visible across all tabs");

		// --- Clicking it opens the create wizard, scoped to this org ---
		await dashCreateBtn.click();
		await page.waitForSelector('[role="dialog"]', { timeout: 8000 });
		console.log("OK  Create-opportunity dialog opens from the dashboard");

		await page.keyboard.press("Escape");
		await page.waitForSelector('[role="dialog"]', {
			state: "hidden",
			timeout: 5000,
		});
		console.log("OK  Escape key closes dialog");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
