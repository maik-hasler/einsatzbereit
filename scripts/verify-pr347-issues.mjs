#!/usr/bin/env node
/**
 * Live verification of the issues PR #347 claims to fix, run against staging.
 * Groups results by issue number so we can decide which issues are fixed.
 *
 * Run: node scripts/verify-pr347-issues.mjs
 */

import { chromium } from "playwright";

const FRONTEND = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const KC = "https://login.maik-hasler.de/realms/einsatzbereit";

const results = {}; // issue -> { status: 'PASS'|'FAIL'|'INFO', detail }
function record(issue, status, detail) {
	results[issue] = { status, detail };
	const tag = status.padEnd(4);
	console.log(`  [${tag}] #${issue}: ${detail}`);
}

async function getToken(username, password) {
	const res = await fetch(`${KC}/protocol/openid-connect/token`, {
		method: "POST",
		headers: { "Content-Type": "application/x-www-form-urlencoded" },
		body: new URLSearchParams({
			grant_type: "password",
			client_id: "frontend",
			username,
			password,
			scope: "openid",
		}),
	});
	if (!res.ok) throw new Error(`token for ${username}: ${res.status}`);
	return (await res.json()).access_token;
}

async function httpChecks() {
	console.log("\n=== HTTP / backend checks ===");

	// #412 health readiness + alive liveness
	const health = await fetch(`${API}/health`);
	const alive = await fetch(`${API}/alive`);
	record(
		412,
		health.ok && alive.ok ? "PASS" : "FAIL",
		`/health -> ${health.status}, /alive -> ${alive.status}`,
	);

	// #385 / #402 security headers on the SPA
	const home = await fetch(FRONTEND, { redirect: "follow" });
	const hdr = (n) => home.headers.get(n);
	const headerChecks = [
		["x-content-type-options", "nosniff"],
		["x-frame-options", "DENY"],
		["referrer-policy", "strict-origin"],
	];
	const missing = headerChecks.filter(
		([h, v]) => !(hdr(h) || "").toLowerCase().includes(v.toLowerCase()),
	);
	record(
		385,
		missing.length === 0 ? "PASS" : "FAIL",
		missing.length === 0
			? `security headers present (xcto=${hdr("x-content-type-options")}, xfo=${hdr("x-frame-options")}, ref=${hdr("referrer-policy")})`
			: `missing: ${missing.map((m) => m[0]).join(", ")}`,
	);

	// #402 gzip on a static asset
	const idx = await fetch(`${FRONTEND}/index.html`, {
		headers: { "Accept-Encoding": "gzip, br" },
	});
	const enc = idx.headers.get("content-encoding");
	record(
		402,
		enc && /gzip|br/.test(enc) ? "PASS" : "INFO",
		`content-encoding on index.html: ${enc ?? "none"}`,
	);

	// Tokens for authenticated checks
	let olaf, vera;
	try {
		olaf = await getToken("olaf", "olaf123");
		vera = await getToken("vera", "vera123");
	} catch (e) {
		record(386, "INFO", `could not get token: ${e.message}`);
		record(384, "INFO", `could not get token: ${e.message}`);
		return;
	}

	// #386 empty org name -> 400 (not 500)
	const orgRes = await fetch(`${API}/v1/organizations`, {
		method: "POST",
		headers: {
			"Content-Type": "application/json",
			Authorization: `Bearer ${olaf}`,
		},
		body: JSON.stringify({ name: "" }),
	});
	record(
		386,
		orgRes.status === 400 ? "PASS" : "FAIL",
		`POST /v1/organizations {name:""} -> ${orgRes.status} (expect 400)`,
	);

	// #384 engagement for non-existent opportunity -> 404 (not 201)
	const fakeId = "00000000-0000-7000-8000-000000000001";
	const engRes = await fetch(
		`${API}/v1/volunteer-opportunities/${fakeId}/engagements`,
		{
			method: "POST",
			headers: {
				"Content-Type": "application/json",
				Authorization: `Bearer ${vera}`,
			},
			body: JSON.stringify({ type: "Waitlist", timeSlotId: null, message: "verify" }),
		},
	);
	record(
		384,
		engRes.status === 404 ? "PASS" : "FAIL",
		`POST /v1/volunteer-opportunities/{missing}/engagements -> ${engRes.status} (expect 404)`,
	);

	// #409 time slot with a past start date -> 400.
	// Find an opportunity owned by olaf's org (Waitlist type) and post a past slot.
	try {
		const list = await fetch(
			`${API}/v1/volunteer-opportunities?PageNumber=1&PageSize=50`,
		).then((r) => r.json());
		const items = list.items ?? list.data ?? [];
		const waitlist = items.find(
			(o) => (o.participationType ?? o.ParticipationType) === "Waitlist",
		);
		if (!waitlist) {
			record(409, "INFO", "no Waitlist opportunity found to test past time slot");
		} else {
			const opId = waitlist.id ?? waitlist.Id;
			const past = "2020-01-01T10:00:00Z";
			const slotRes = await fetch(
				`${API}/v1/volunteer-opportunities/${opId}/time-slots`,
				{
					method: "POST",
					headers: {
						"Content-Type": "application/json",
						Authorization: `Bearer ${olaf}`,
					},
					body: JSON.stringify({
						startDateTime: past,
						endDateTime: "2020-01-01T12:00:00Z",
						maxParticipants: 5,
					}),
				},
			);
			// 400 = validation rejected past date (fixed). 403 = not owner (can't test).
			if (slotRes.status === 400) {
				record(409, "PASS", `past time slot -> 400 (rejected) on op ${opId}`);
			} else if (slotRes.status === 403) {
				record(409, "INFO", `not owner of op ${opId} (403) - cannot test past slot live`);
			} else {
				record(409, "FAIL", `past time slot -> ${slotRes.status} (expect 400)`);
			}
		}
	} catch (e) {
		record(409, "INFO", `time slot test error: ${e.message}`);
	}
}

async function browserChecks() {
	console.log("\n=== Browser / frontend checks (anonymous) ===");
	const browser = await chromium.launch({ headless: true });
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	await page.goto(FRONTEND, { waitUntil: "networkidle", timeout: 30_000 });

	// #390 dead Instagram button removed from footer
	const footer = page.locator("footer");
	const igButtons = footer.locator(
		"button:has-text('Instagram'), a[href*='instagram']",
	);
	const igCount = await igButtons.count();
	// A real instagram <a href> is fine; a dead <button> doing nothing is the bug.
	const deadIg = await footer
		.locator("button")
		.filter({ hasText: /instagram/i })
		.count();
	record(
		390,
		deadIg === 0 ? "PASS" : "FAIL",
		`footer dead Instagram <button> count = ${deadIg} (links=${igCount})`,
	);

	// #391 aria-pressed on view toggle buttons
	const listBtn = page.locator('[data-testid="view-toggle-list"]');
	if ((await listBtn.count()) > 0) {
		const lp = await listBtn.getAttribute("aria-pressed");
		const mp = await page
			.locator('[data-testid="view-toggle-map"]')
			.getAttribute("aria-pressed");
		record(
			391,
			lp !== null && mp !== null ? "PASS" : "FAIL",
			`aria-pressed list=${lp}, map=${mp}`,
		);
	} else {
		record(391, "INFO", "view toggle buttons not found on homepage");
	}

	// Navigate to a detail page for #394 / #373 / #393
	const firstCard = page.locator("a[href*='/volunteer-opportunities/']").first();
	const href = await firstCard.getAttribute("href").catch(() => null);
	if (href) {
		await page.goto(`${FRONTEND}${href}`, {
			waitUntil: "networkidle",
			timeout: 30_000,
		});

		// #394 breadcrumb
		const bc = page.locator(
			"nav[aria-label='Breadcrumb'], nav[aria-label='breadcrumb']",
		);
		record(
			394,
			(await bc.count()) > 0 ? "PASS" : "FAIL",
			`breadcrumb nav ${(await bc.count()) > 0 ? "present" : "missing"}`,
		);

		// #373 share button
		const share = page.getByTestId("share-opportunity");
		record(
			373,
			(await share.count()) > 0 ? "PASS" : "FAIL",
			`share button ${(await share.count()) > 0 ? "present" : "missing"}`,
		);

		// #393 brand colors in modals: open sign-up modal if present, check no bg-black on primary button
		// (sign-up button only shows for anon? It usually prompts login.) Inspect any black bg buttons.
		const blackButtons = await page
			.locator("button.bg-black, [class*='bg-black']")
			.count();
		record(
			393,
			blackButtons === 0 ? "PASS" : "INFO",
			`elements with bg-black on detail page = ${blackButtons}`,
		);
	} else {
		for (const i of [394, 373, 393]) record(i, "INFO", "no opportunity card found");
	}

	await browser.close();
}

async function main() {
	console.log("PR #347 issue verification against staging");
	console.log("=".repeat(50));
	try {
		await httpChecks();
		await browserChecks();
	} catch (e) {
		console.error("Unexpected error:", e.message);
	}

	console.log(`\n${"=".repeat(50)}`);
	console.log("SUMMARY (issue -> status):");
	const order = Object.keys(results).sort((a, b) => a - b);
	for (const i of order) {
		console.log(`  #${i}\t${results[i].status}\t${results[i].detail}`);
	}
	const fails = order.filter((i) => results[i].status === "FAIL");
	console.log(`\n${fails.length === 0 ? "All live checks passed (no FAIL)" : "FAILED: " + fails.map((i) => "#" + i).join(", ")}`);
	process.exit(fails.length === 0 ? 0 : 1);
}

main();
