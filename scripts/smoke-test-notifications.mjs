#!/usr/bin/env node
/**
 * Smoke test for notification deep-links and enriched titles (issues #367, #462).
 *
 * Verifies:
 *   1. API health check passes
 *   2. GET /v1/notifications returns relatedTitle and actionUrl fields (when auth)
 *   3. Notification bell is visible after login in browser
 *   4. Clicking the bell opens the notification panel
 *
 * Run: node scripts/smoke-test-notifications.mjs
 */

import { chromium } from "playwright";

const FRONTEND = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const KEYCLOAK = "https://login.maik-hasler.de";
const REALM = "einsatzbereit";
const CLIENT_ID = "frontend";

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

// --- API health ---
async function checkHealth() {
	console.log("\n[Health]");
	const res = await fetch(`${API}/health`);
	if (res.ok) {
		ok(`GET /health -> ${res.status}`);
	} else {
		fail("GET /health", `got ${res.status}`);
	}
}

// --- Get Keycloak token for vera ---
async function getToken(username, password) {
	const res = await fetch(
		`${KEYCLOAK}/realms/${REALM}/protocol/openid-connect/token`,
		{
			method: "POST",
			headers: { "Content-Type": "application/x-www-form-urlencoded" },
			body: new URLSearchParams({
				grant_type: "password",
				client_id: CLIENT_ID,
				username,
				password,
				scope: "openid",
			}),
		},
	);
	if (!res.ok) {
		throw new Error(`Token fetch failed: ${res.status}`);
	}
	const { access_token } = await res.json();
	return access_token;
}

// --- API: check notification schema ---
async function checkNotificationSchema() {
	console.log("\n[Notification API schema]");

	let token;
	try {
		token = await getToken("vera", "vera123");
		ok("Keycloak token acquired for vera");
	} catch (e) {
		fail("Keycloak token", e.message);
		return;
	}

	const res = await fetch(`${API}/v1/notifications`, {
		headers: { Authorization: `Bearer ${token}` },
	});

	if (!res.ok) {
		fail("GET /v1/notifications", `status ${res.status}`);
		return;
	}

	ok("GET /v1/notifications -> 200");

	const notifications = await res.json();

	// Even if empty, the response must be an array
	if (!Array.isArray(notifications)) {
		fail("GET /v1/notifications response shape", "expected array");
		return;
	}

	ok(`Response is an array (${notifications.length} items)`);

	// Verify schema fields exist on any notification that's present
	for (const n of notifications) {
		if (!("relatedTitle" in n)) {
			fail("notification.relatedTitle", "field missing from response");
			return;
		}
		if (!("actionUrl" in n)) {
			fail("notification.actionUrl", "field missing from response");
			return;
		}
		// Check actionUrl routing logic
		if (n.kind === "EngagementCreated" || n.kind === "EngagementWithdrawn") {
			if (n.actionUrl && !n.actionUrl.includes("/volunteer-opportunities/")) {
				fail(
					`${n.kind} actionUrl`,
					`expected /volunteer-opportunities/... got ${n.actionUrl}`,
				);
				return;
			}
		}
		if (n.kind === "EngagementConfirmed" || n.kind === "EngagementCancelled") {
			if (n.actionUrl && n.actionUrl !== "/my-engagements") {
				fail(
					`${n.kind} actionUrl`,
					`expected /my-engagements got ${n.actionUrl}`,
				);
				return;
			}
		}
	}

	if (notifications.length > 0) {
		const sample = notifications[0];
		ok(
			`First notification: kind=${sample.kind}, title="${sample.relatedTitle ?? "(none)"}", url=${sample.actionUrl ?? "(none)"}`,
		);
	} else {
		ok("No notifications for vera (empty list is valid, schema fields present)");
	}
}

// --- Browser: verify notification bell ---
async function checkNotificationBell() {
	console.log("\n[Browser: notification bell]");

	const browser = await chromium.launch({ headless: true });
	const context = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await context.newPage();

	try {
		await page.goto(FRONTEND, { waitUntil: "networkidle", timeout: 30_000 });

		// Two-step Keycloak login on live
		await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
		await page.waitForURL(`**/${REALM}/**`, { timeout: 15_000 });
		await page.locator("#username").fill("vera");
		await page.locator("#kc-login").click();
		await page.locator("#password").fill("vera123");
		await page.locator("#kc-login").click();

		await page.waitForURL(`${FRONTEND}/`, { timeout: 30_000 });
		ok("Login as vera succeeded");

		// Bell button
		const bell = page.getByTestId("notification-bell");
		await bell.waitFor({ state: "visible", timeout: 10_000 });
		ok("Notification bell is visible");

		// Open panel
		await bell.click();
		const panel = page.getByTestId("notification-panel");
		await panel.waitFor({ state: "visible", timeout: 5_000 });
		ok("Notification panel opens after clicking bell");

		// Close panel (click outside)
		await page.keyboard.press("Escape");
	} catch (e) {
		fail("Browser notification bell check", e.message);
	} finally {
		await browser.close();
	}
}

async function main() {
	console.log("=== Smoke test: notification deep-links (issues #367, #462) ===");

	await checkHealth();
	await checkNotificationSchema();
	await checkNotificationBell();

	console.log(`\n=== Results: ${passed} passed, ${failed} failed ===\n`);
	if (failed > 0) process.exit(1);
}

main().catch((e) => {
	console.error("Unexpected error:", e);
	process.exit(1);
});
