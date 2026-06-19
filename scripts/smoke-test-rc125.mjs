// Smoke test for rc.125: header dropdown transparent styles (#478/#477)
// and rc.126: iCal export (#371)
import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const KEYCLOAK = "https://login.maik-hasler.de";
const CLIENT_ID = "frontend";
const REALM = "einsatzbereit";

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
	const data = await res.json();
	return data.access_token;
}

async function loginDesktop(page) {
	await page.setViewportSize({ width: 1280, height: 800 });
	await page.goto(BASE, { waitUntil: "networkidle" });
	const signinBtn = page.getByRole("button", { name: /sign in|anmelden/i }).first();
	await signinBtn.click({ timeout: 10000 });
	await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
	await page.fill("#username", "vera");
	await page.getByRole("button", { name: /sign in|anmelden/i }).click();
	await page.fill("#password", "vera123");
	await page.getByRole("button", { name: /sign in|anmelden/i }).click();
	await page.waitForURL(`${BASE}/**`, { timeout: 15000 });
	await page.waitForTimeout(500);
	console.log("  Logged in as vera (desktop)");
}

async function main() {
	const browser = await chromium.launch();
	let passed = 0;
	let failed = 0;

	function assert(cond, msg) {
		if (cond) { console.log(`  PASS: ${msg}`); passed++; }
		else { console.error(`  FAIL: ${msg}`); failed++; }
	}

	// ── Test 1: Desktop transparent header dropdown (#478) ────────────────────
	console.log("\n[1] Desktop - transparent header dropdowns (#478)");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			await loginDesktop(page);
			await page.goto(BASE, { waitUntil: "networkidle" });
			await page.waitForTimeout(600);

			const header = page.locator("header").first();
			const cls = await header.getAttribute("class") ?? "";
			assert(cls.includes("bg-transparent"), `Header is transparent on homepage hero (class: ${cls})`);

			// Open desktop notification bell
			const notifBell = page.locator('button[data-testid="notification-bell"]').first();
			const bellVis = await notifBell.isVisible().catch(() => false);
			if (bellVis) {
				await notifBell.click();
				await page.waitForTimeout(300);
				// Dropdown uses bg-brand-800 border-brand-700 in transparent mode
				const darkDropdown = page.locator(
					'div[class*="bg-brand-800"][class*="border-brand-700"]'
				).first();
				const darkVis = await darkDropdown.isVisible().catch(() => false);
				assert(darkVis, "Desktop notification dropdown uses bg-brand-800 in transparent mode (#478)");
				await page.keyboard.press("Escape");
			} else {
				console.log("  SKIP: No desktop notification bell visible");
			}
		} catch (e) { console.error("  ERROR:", e.message); failed++; }
		finally { await ctx.close(); }
	}

	// ── Test 2: Mobile notification bell in header (#477) ─────────────────────
	console.log("\n[2] Mobile - notification bell in header (#477)");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			// Log in on desktop, then switch to mobile viewport
			await loginDesktop(page);
			await page.setViewportSize({ width: 390, height: 844 });
			await page.goto(BASE, { waitUntil: "networkidle" });
			await page.waitForTimeout(800);

			// Check mobile notification bell is in the header bar
			const mobileBell = page.locator('button[data-testid="mobile-notification-bell"]');
			const count = await mobileBell.count();
			console.log(`  mobile-notification-bell DOM count: ${count}`);
			assert(count > 0, "mobile-notification-bell element exists in DOM (#477)");

			const vis = await mobileBell.first().isVisible().catch(() => false);
			assert(vis, "Mobile notification bell is visible in the header bar");

			// Tap it - notification overlay should appear
			if (vis) {
				await mobileBell.first().click();
				await page.waitForTimeout(400);
				const overlay = page.locator(
					'div[class*="md:hidden"][class*="absolute"][class*="border-t"]'
				).first();
				const overlayVis = await overlay.isVisible().catch(() => false);
				console.log(`  Notification overlay visible: ${overlayVis}`);
				assert(overlayVis, "Mobile notification overlay shown when bell tapped (#477)");
			}
		} catch (e) { console.error("  ERROR:", e.message); failed++; }
		finally { await ctx.close(); }
	}

	// ── Test 3: iCal export endpoint (#371) ───────────────────────────────────
	console.log("\n[3] iCal export endpoint (#371)");
	{
		try {
			const token = await getToken("vera", "vera123");
			const listRes = await fetch(
				`${API}/v1/volunteer-opportunities?pageNumber=1&pageSize=5`,
				{ headers: { Authorization: `Bearer ${token}` } },
			);
			assert(listRes.ok, `GET /v1/volunteer-opportunities 2xx (${listRes.status})`);

			if (listRes.ok) {
				const body = await listRes.json();
				const items = Array.isArray(body) ? body : (body.items ?? body.data ?? []);
				console.log(`  Found ${items.length} opportunities`);

				if (items.length > 0) {
					const id = items[0].id;
					const calRes = await fetch(`${API}/v1/volunteer-opportunities/${id}/calendar`);
					assert(calRes.ok, `GET /calendar returns 2xx (${calRes.status})`);

					const ct = calRes.headers.get("content-type") ?? "";
					assert(ct.includes("text/calendar"), `Content-Type: ${ct}`);

					const text = await calRes.text();
					assert(text.includes("BEGIN:VCALENDAR"), "Body contains BEGIN:VCALENDAR");
					assert(text.includes("BEGIN:VEVENT"), "Body contains BEGIN:VEVENT");
					console.log(`  iCal preview:\n${text.slice(0, 300)}`);
				}
			}
		} catch (e) { console.error("  ERROR:", e.message); failed++; }
	}

	await browser.close();
	console.log(`\nResults: ${passed} passed, ${failed} failed`);
	process.exit(failed > 0 ? 1 : 0);
}

main();
