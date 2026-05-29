#!/usr/bin/env node
/**
 * Smoke test for PR #347 batch 3 against staging.
 * Tests: /health readiness (#412), opportunity detail breadcrumb (#394)
 * and share button (#373).
 *
 * Run: node scripts/smoke-test-detail-share.mjs
 */

import { chromium } from "playwright";

const FRONTEND = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

let passed = 0;
let failed = 0;

function ok(label) {
	console.log(`  PASS  ${label}`);
	passed++;
}

function fail(label, detail) {
	console.error(`  FAIL  ${label}: ${detail}`);
	failed++;
}

async function checkHealth() {
	console.log("\n[Health readiness (#412)]");
	const res = await fetch(`${API}/health`);
	if (res.ok) {
		ok(`GET /health -> ${res.status} (deps reachable)`);
	} else {
		fail("GET /health", `expected 200, got ${res.status}`);
	}

	const alive = await fetch(`${API}/alive`);
	if (alive.ok) {
		ok(`GET /alive -> ${alive.status} (liveness)`);
	} else {
		fail("GET /alive", `expected 200, got ${alive.status}`);
	}
}

async function checkDetailPage() {
	console.log("\n[Opportunity detail page (#394 / #373)]");
	const browser = await chromium.launch({ headless: true });
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	await page.goto(FRONTEND, { waitUntil: "networkidle", timeout: 30_000 });

	const firstCard = page.locator("a[href*='/volunteer-opportunities/']").first();
	const href = await firstCard.getAttribute("href");
	if (!href) {
		fail("opportunity link", "no opportunity card found on home page");
		await browser.close();
		return;
	}

	await page.goto(`${FRONTEND}${href}`, {
		waitUntil: "networkidle",
		timeout: 30_000,
	});

	// #394 breadcrumb (case-insensitive on the aria-label).
	const breadcrumb = page.locator(
		"nav[aria-label='Breadcrumb'], nav[aria-label='breadcrumb']",
	);
	if ((await breadcrumb.count()) > 0) {
		ok("breadcrumb nav present on detail page");
	} else {
		fail("breadcrumb", "no nav[aria-label=Breadcrumb] found");
	}

	// #373 share button.
	const shareButton = page.getByRole("button", { name: /share/i });
	if ((await shareButton.count()) > 0) {
		ok("share button present on detail page");
	} else {
		fail("share button", "no share button found");
	}

	await browser.close();
}

async function main() {
	console.log("Smoke test: detail page + health (batch 3)");
	console.log("=".repeat(44));

	try {
		await checkHealth();
		await checkDetailPage();
	} catch (e) {
		console.error("Unexpected error:", e.message);
		failed++;
	}

	console.log(`\n${"=".repeat(44)}`);
	console.log(`Results: ${passed} passed, ${failed} failed`);

	if (failed > 0) process.exit(1);
}

main();
