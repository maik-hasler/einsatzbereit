/**
 * Smoke test for opportunity drafts + banner images (#439 follow-up).
 * End-to-end on staging: saves a draft with a banner via the wizard, checks
 * it appears on the org dashboard (and NOT in the public list), opens it,
 * verifies the banner is served, publishes it, and finally deletes it.
 * Run: node scripts/smoke-test-drafts-banner.mjs
 */

import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

const TITLE = `Draft Smoke Test ${Date.now()}`;
const PNG_1X1 = Buffer.from(
	"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
	"base64",
);

async function main() {
	const apiRes = await fetch(`${API}/health`);
	if (!apiRes.ok) throw new Error(`Health check failed: ${apiRes.status}`);
	console.log("OK  API health check passed");

	const browser = await chromium.launch();
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
		await page.waitForFunction(
			() => document.cookie.includes("active-org="),
			{ timeout: 10000 },
		);
		const orgId = await page.evaluate(() => {
			const m = document.cookie.match(/(?:^|;\s*)active-org=([^;]*)/);
			return m ? decodeURIComponent(m[1]) : null;
		});
		if (!orgId) throw new Error("active-org cookie not set");
		await page.reload({ waitUntil: "networkidle" });

		// --- Save a draft with a banner via the wizard ---
		const createBtn = page.getByTestId("create-opportunity-btn");
		if ((await createBtn.count()) === 0)
			throw new Error("Create button not visible");
		await createBtn.first().click();
		await page.waitForSelector('[role="dialog"]', { timeout: 8000 });

		await page.fill("#opportunity-title", TITLE);
		await page.fill(
			"#opportunity-description",
			"Draft created by the automated drafts/banner smoke test.",
		);
		await page.setInputFiles("#opportunity-banner", {
			name: "banner.png",
			mimeType: "image/png",
			buffer: PNG_1X1,
		});
		console.log("OK  Banner file selected (preview)");

		// Fill the address so the draft can be published later.
		await page.getByTestId("wizard-stepper-2").click();
		await page.waitForSelector('[data-testid="wizard-step-2"]', {
			timeout: 5000,
		});
		await page.fill("#opportunity-street", "Musterstrasse");
		await page.fill("#opportunity-house", "42");
		await page.fill("#opportunity-zip", "10115");
		await page.fill("#opportunity-city", "Berlin");

		await page.getByTestId("modal-save-draft").click();
		await page.waitForSelector('[role="dialog"]', {
			state: "hidden",
			timeout: 15000,
		});
		console.log("OK  Draft saved (wizard closed)");

		// --- Draft must NOT appear in the public list ---
		const listRes = await fetch(
			`${API}/v1/volunteer-opportunities?pageNumber=1&pageSize=50`,
		);
		const listJson = await listRes.json();
		const listItems = listJson.items ?? listJson;
		if (
			Array.isArray(listItems) &&
			listItems.some((o) => o.title === TITLE)
		)
			throw new Error("Draft leaked into the public opportunity list");
		console.log("OK  Draft hidden from the public list");

		// --- Draft visible on the org dashboard ---
		await page.goto(`${BASE}/organizations/${orgId}/dashboard`, {
			waitUntil: "networkidle",
		});
		await page.waitForSelector('[data-testid="drafts-section"]', {
			timeout: 10000,
		});
		const draftCard = page
			.locator('[data-testid="drafts-section"] li')
			.filter({ hasText: TITLE });
		if ((await draftCard.count()) === 0)
			throw new Error("Draft not listed on the org dashboard");
		console.log("OK  Draft listed on the org dashboard");

		// --- Open the draft: notice + banner + publish ---
		await draftCard
			.first()
			.locator("a")
			.first()
			.click();
		await page.waitForURL(/\/volunteer-opportunities\//, { timeout: 10000 });
		await page.waitForLoadState("networkidle");

		const opportunityId = page
			.url()
			.split("/volunteer-opportunities/")[1]
			.split(/[/?#]/)[0];

		const publishBtn = page.getByTestId("publish-opportunity");
		if ((await publishBtn.count()) === 0)
			throw new Error("Draft notice / publish button missing on detail page");
		console.log("OK  Draft notice with publish button shown");

		const bannerImg = page.locator(`img[src*="${opportunityId}/banner"]`);
		if ((await bannerImg.count()) === 0)
			throw new Error("Banner image not rendered on detail page");
		console.log("OK  Banner image rendered on detail page");

		const bannerRes = await fetch(
			`${API}/v1/volunteer-opportunities/${opportunityId}/banner`,
		);
		if (
			!bannerRes.ok ||
			!(bannerRes.headers.get("content-type") ?? "").startsWith("image/")
		)
			throw new Error(`Banner endpoint failed: ${bannerRes.status}`);
		console.log("OK  Banner endpoint serves the image");

		// --- Publish the draft ---
		await publishBtn.click();
		await page.waitForSelector('[data-testid="publish-opportunity"]', {
			state: "detached",
			timeout: 15000,
		});
		console.log("OK  Draft published (notice gone)");

		// --- Cleanup: delete the test opportunity ---
		await page
			.getByRole("button", { name: /^(delete|löschen)$/i })
			.first()
			.click();
		await page
			.locator('[role="dialog"], [role="alertdialog"]')
			.getByRole("button", { name: /delete|löschen/i })
			.last()
			.click();
		await page.waitForURL(`${BASE}/**`, { timeout: 15000 });
		console.log("OK  Test opportunity deleted (cleanup)");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
