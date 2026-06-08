/**
 * Smoke test for the opportunities-list redesign (#439 follow-up).
 * Verifies: centered section heading + subtitle, redesigned cards with a
 * category banner and an organisation avatar/link, and the i18next plural
 * fix ("N spots left" agrees in number - no more "23 spot left").
 * Run: node scripts/smoke-test-opportunities-redesign.mjs
 */

import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function main() {
	const apiRes = await fetch(`${API}/health`);
	if (!apiRes.ok) throw new Error(`Health check failed: ${apiRes.status}`);
	console.log("OK  API health check passed");

	const browser = await chromium.launch();
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	try {
		await page.goto(BASE, { waitUntil: "networkidle" });
		await page.waitForSelector("main", { timeout: 15000 });
		console.log("OK  Home page loaded");

		// 1. Section heading is present and centre-aligned (matches "How it works")
		const heading = page
			.getByRole("heading", { name: /current opportunities|aktuelle eins/i })
			.first();
		await heading.waitFor({ timeout: 10000 });
		const align = await heading.evaluate((el) => getComputedStyle(el).textAlign);
		if (align !== "center")
			throw new Error(`Section heading not centred (text-align=${align})`);
		console.log("OK  Section heading present and centred");

		// 2. Subtitle line is rendered below the heading
		const subtitle = page.getByText(
			/lend a hand|pack mit an|few hours|wenige Stunden/i,
		);
		if ((await subtitle.count()) === 0)
			throw new Error("Subtitle under the heading is missing");
		console.log("OK  Section subtitle present");

		// 3. Filter pills still render (redesign must not break them)
		await page.waitForSelector('[data-testid="filter-frequency"]', {
			timeout: 10000,
		});
		console.log("OK  Filter bar intact");

		// 4. Opportunity cards: each must link to an org and carry a gradient banner
		const cards = page.locator('ul li:has(a[href^="/volunteer-opportunities/"])');
		const cardCount = await cards.count();
		if (cardCount === 0) {
			console.log("WARN  No opportunity cards on staging - skipping card checks");
		} else {
			console.log(`OK  ${cardCount} opportunity card(s) rendered`);

			const first = cards.first();
			const orgLink = first.locator('a[href^="/organizations/"]');
			if ((await orgLink.count()) === 0)
				throw new Error("Card is missing a clickable organisation link");
			console.log("OK  Card organisation link present");

			const banner = first.locator('[class*="from-brand-500"]');
			if ((await banner.count()) === 0)
				throw new Error("Card is missing the category banner");
			console.log("OK  Card category banner present");

			// 5. Plural fix: every "spots left" badge must agree in number
			const badgeTexts = await page
				.locator("ul li span")
				.allInnerTexts();
			const spotsRe = /(\d+)\s+(spot|spots)\s+left/i;
			for (const txt of badgeTexts) {
				const m = txt.match(spotsRe);
				if (!m) continue;
				const n = Number(m[1]);
				const word = m[2].toLowerCase();
				const expected = n === 1 ? "spot" : "spots";
				if (word !== expected)
					throw new Error(
						`Plural mismatch: "${txt}" (expected "${expected}" for count ${n})`,
					);
			}
			console.log("OK  Spots-left badges have correct pluralisation");
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
