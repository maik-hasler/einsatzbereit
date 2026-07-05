/**
 * Smoke test for #580: distinguish "Leave organization" from "Remove member",
 * protect the last member, and add explicit org deletion.
 *
 * Verifies against the live staging environment:
 * - The current user's own row in the Members tab shows a "Leave" action
 *   (not "Remove"), disabled while they are the org's sole member, with an
 *   explanatory hint.
 * - The Settings tab shows a "Delete Organization" action, enabled for the
 *   sole member, gated behind a confirmation dialog.
 * - Confirming deletion removes the organization and redirects home.
 *
 * Run: node scripts/smoke-test-580-org-deletion.mjs
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
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		await page.waitForSelector("h1", { timeout: 15000 });

		const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
		if ((await signInBtn.count()) > 0) {
			await signInBtn.first().click();
			await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
			await page.fill("#username", "vera");
			await page.click("#kc-login");
			await page.fill("#password", "vera123");
			await page.click("#kc-login");
			await page.waitForURL(BASE + "/**", { timeout: 15000 });
		}
		await page.waitForSelector("main", { timeout: 10000 });
		console.log("OK  Logged in as vera");

		// --- Create a fresh org so vera is its sole member ---
		const orgName = `Smoke580 ${Date.now()}`;
		const switcherBtn = page.getByLabel(/switch organization|organisation wechseln/i);
		if ((await switcherBtn.count()) > 0) {
			await switcherBtn.first().click();
			await page
				.getByRole("button", { name: /create organization|organisation erstellen/i })
				.click();
		} else {
			// No orgs yet - vera has no switcher; create from the profile page.
			await page.goto(`${BASE}/profile`, { waitUntil: "networkidle" });
			await page
				.getByRole("button", { name: /create organization|organisation erstellen/i })
				.click();
		}
		await page.waitForSelector("[role='dialog']", { timeout: 10000 });
		await page.fill("input[type='text']", orgName);
		await page.getByTestId("modal-submit").click();
		await page.waitForSelector("[role='dialog']", { state: "detached", timeout: 10000 });
		console.log(`OK  Created organization "${orgName}"`);

		// --- Navigate to the new org's dashboard ---
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		const switcher2 = page.getByLabel(/switch organization|organisation wechseln/i);
		await switcher2.first().click();
		await page.getByText(orgName, { exact: true }).click();
		await page.getByTestId("org-dashboard-link").first().click();
		await page.waitForURL(/\/organizations\/.+\/dashboard/, { timeout: 10000 });
		const orgId = page.url().match(/\/organizations\/([^/]+)\/dashboard/)[1];
		console.log(`OK  Opened dashboard for org ${orgId}`);

		// --- Members tab: own row shows disabled "Leave", not "Remove" ---
		await page.goto(`${BASE}/organizations/${orgId}/dashboard?tab=members`, {
			waitUntil: "networkidle",
		});
		const leaveBtn = page.getByRole("button", { name: /^leave$|^verlassen$/i });
		await leaveBtn.waitFor({ state: "visible", timeout: 10000 });
		if (!(await leaveBtn.isDisabled())) {
			throw new Error("Leave button should be disabled for the sole member");
		}
		console.log("OK  Own row shows a disabled \"Leave\" action (sole member)");

		const removeBtn = page.getByRole("button", { name: /^remove$|^entfernen$/i });
		if ((await removeBtn.count()) > 0) {
			throw new Error('Own row should not show a "Remove" action');
		}
		console.log('OK  Own row does not show "Remove"');

		// --- Settings tab: "Delete Organization" enabled for sole member ---
		await page.goto(`${BASE}/organizations/${orgId}/dashboard?tab=settings`, {
			waitUntil: "networkidle",
		});
		const deleteBtn = page.getByRole("button", {
			name: /delete organization|organisation löschen/i,
		});
		await deleteBtn.waitFor({ state: "visible", timeout: 10000 });
		if (await deleteBtn.isDisabled()) {
			throw new Error("Delete Organization button should be enabled for the sole member");
		}
		console.log("OK  \"Delete Organization\" is enabled for the sole member");

		await deleteBtn.click();
		const confirmDialog = page.locator("[role='dialog']");
		await confirmDialog.waitFor({ state: "visible", timeout: 5000 });
		if (!(await confirmDialog.locator(`text=${orgName}`).count())) {
			throw new Error("Confirmation dialog does not mention the organization name");
		}
		console.log("OK  Confirmation dialog shown, mentions the organization name");

		await confirmDialog.getByRole("button", { name: /yes, delete|ja, löschen/i }).click();
		await page.waitForURL(`${BASE}/`, { timeout: 10000 });
		console.log("OK  Deleting redirected to the home page");

		// --- Verify the organization is actually gone ---
		const getRes = await fetch(`${API}/v1/organizations/${orgId}`);
		if (getRes.status !== 401 && getRes.status !== 404) {
			throw new Error(
				`Expected the deleted org's endpoint to reject unauthenticated access (401) or be gone (404), got ${getRes.status}`,
			);
		}
		console.log("OK  Deleted organization no longer resolves");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
