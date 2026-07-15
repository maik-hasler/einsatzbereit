// Smoke test for #694 (PR #697): the opportunity detail page and organization
// profile page wrapped their content in a max-w-2xl container with no
// mx-auto, so it hugged the left edge of the already-centered AppLayout main
// area instead of being centered like every other page.
//
// Verifies both pages now have roughly equal left/right whitespace around
// their max-w-2xl content container (i.e. it is horizontally centered
// within its parent), on a viewport wide enough that the container doesn't
// just fill the full width.
//
// No throwaway data is created - existing seed/smoke-test opportunities and
// organizations are used read-only.
// Run: node scripts/smoke-test-694-centering.mjs

import { launchLiveBrowser } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function assertCentered(page, url, label) {
	await page.goto(url, { waitUntil: "networkidle" });

	const box = await page.evaluate(() => {
		const main = document.querySelector("main");
		if (!main) return null;
		const container = main.querySelector(".max-w-2xl");
		if (!container) return null;
		const mainRect = main.getBoundingClientRect();
		const containerRect = container.getBoundingClientRect();
		return {
			leftGap: containerRect.left - mainRect.left,
			rightGap: mainRect.right - containerRect.right,
		};
	});

	if (!box) throw new Error(`${label}: could not find main > .max-w-2xl`);

	const diff = Math.abs(box.leftGap - box.rightGap);
	console.log(
		`OK  ${label}: leftGap=${box.leftGap.toFixed(1)} rightGap=${box.rightGap.toFixed(1)} diff=${diff.toFixed(1)}`,
	);
	if (diff > 2) {
		throw new Error(
			`${label}: content is not centered (leftGap=${box.leftGap}, rightGap=${box.rightGap})`,
		);
	}
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const oppsRes = await fetch(
		`${API}/v1/volunteer-opportunities?PageNumber=1&PageSize=1`,
	);
	if (!oppsRes.ok)
		throw new Error(`GET /volunteer-opportunities failed: ${oppsRes.status}`);
	const opps = await oppsRes.json();
	const opportunity = opps.items?.[0];
	if (!opportunity)
		throw new Error("No volunteer opportunities found - cannot run this smoke test");
	console.log(`OK  Using opportunity ${opportunity.id}`);
	console.log(`OK  Using organization ${opportunity.organizationId}`);

	const { browser, page } = await launchLiveBrowser();
	try {
		// Wide viewport so max-w-2xl (42rem/672px) sits well inside the
		// max-w-7xl (80rem/1280px) AppLayout main and centering is measurable.
		await page.setViewportSize({ width: 1400, height: 900 });

		await assertCentered(
			page,
			`${BASE}/volunteer-opportunities/${opportunity.id}`,
			"Opportunity detail page",
		);
		await assertCentered(
			page,
			`${BASE}/organizations/${opportunity.organizationId}`,
			"Organization profile page",
		);
	} finally {
		await browser.close();
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
