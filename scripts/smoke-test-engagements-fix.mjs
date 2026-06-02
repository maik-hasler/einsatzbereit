import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

const browser = await chromium.launch({ headless: true });
let passed = 0, failed = 0;
function pass(m) { console.log(`  PASS  ${m}`); passed++; }
function fail(m, e) { console.error(`  FAIL  ${m}${e ? " - " + (e.message ?? e) : ""}`); failed++; }
async function assert(label, fn) {
	try { await fn(); pass(label); } catch (e) { fail(label, e); }
}

// Test 1: /v1/me/engagements returns 200 for vera
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	const apiCalls = {};
	page.on("response", async r => {
		if (r.url().includes("api.maik-hasler.de/v1/")) {
			try { apiCalls[r.url()] = { status: r.status(), body: (await r.text()).slice(0, 100) }; } catch {}
		}
	});

	await page.goto(BASE);
	await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
	await page.waitForURL("**/realms/einsatzbereit/**", { timeout: 20000 });
	await page.locator("#username").fill("vera");
	await page.locator("#kc-login").click();
	await page.locator("#password").waitFor({ timeout: 10000 });
	await page.locator("#password").fill("vera123");
	await page.locator("#kc-login").click();
	await page.waitForURL(`${BASE}/`, { timeout: 30000 });

	await page.goto(`${BASE}/my-engagements`);
	await page.waitForLoadState("networkidle");

	const engStatus = apiCalls[Object.keys(apiCalls).find(k => k.includes("/me/engagements")) ?? ""]?.status;
	await assert("GET /v1/me/engagements returns 200", async () => {
		if (engStatus !== 200) throw new Error(`status=${engStatus ?? "not called"}`);
	});
	await assert("My Engagements page: no error toast visible", async () => {
		const errorText = page.locator("text=/unexpected error/i").first();
		const visible = await errorText.isVisible({ timeout: 2000 }).catch(() => false);
		if (visible) throw new Error("Error toast visible");
	});
	await assert("My Engagements page: no error text in body", async () => {
		const errEl = page.locator("text=/error|fehler/i").first();
		const visible = await errEl.isVisible({ timeout: 1000 }).catch(() => false);
		if (visible) throw new Error(await errEl.textContent() ?? "error text visible");
	});
	console.log("  API calls:");
	for (const [url, r] of Object.entries(apiCalls)) {
		if (url.includes("engagements")) console.log(`    ${r.status} ${url}`);
	}
	await ctx.close();
}

// Test 2: /v1/volunteer-opportunities/{id}/engagements returns 200 for olaf
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	const apiCalls = {};
	page.on("response", async r => {
		if (r.url().includes("api.maik-hasler.de/v1/")) {
			try { apiCalls[r.url()] = { status: r.status(), body: (await r.text()).slice(0, 200) }; } catch {}
		}
	});

	await page.goto(BASE);
	await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
	await page.waitForURL("**/realms/einsatzbereit/**", { timeout: 20000 });
	await page.locator("#username").fill("olaf");
	await page.locator("#kc-login").click();
	await page.locator("#password").waitFor({ timeout: 10000 });
	await page.locator("#password").fill("olaf123");
	await page.locator("#kc-login").click();
	await page.waitForURL(`${BASE}/`, { timeout: 30000 });

	const opId = "019e5652-576f-7a50-8df4-9f706b7e50d6";
	await page.goto(`${BASE}/volunteer-opportunities/${opId}/engagements`);
	await page.waitForLoadState("networkidle");

	const engUrl = Object.keys(apiCalls).find(k => k.includes(`${opId}/engagements`));
	const engStatus = apiCalls[engUrl ?? ""]?.status;
	await assert("GET /v1/volunteer-opportunities/{id}/engagements returns 200", async () => {
		if (engStatus !== 200) throw new Error(`status=${engStatus ?? "not called"}, body=${apiCalls[engUrl ?? ""]?.body}`);
	});
	await assert("Engagement management page: no error toast", async () => {
		const errText = page.locator("text=/unexpected error/i").first();
		const visible = await errText.isVisible({ timeout: 2000 }).catch(() => false);
		if (visible) throw new Error("Error toast visible");
	});
	console.log("  API calls:");
	for (const [url, r] of Object.entries(apiCalls)) {
		if (url.includes("engagements")) console.log(`    ${r.status} ${url}`);
	}
	await ctx.close();
}

await browser.close();
console.log(`\nResults: ${passed} passed, ${failed} failed`);
if (failed > 0) process.exit(1);
