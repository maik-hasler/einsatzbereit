import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

const browser = await chromium.launch({ headless: true });

// Test 1: Check /v1/me/engagements as vera (no engagements)
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	await page.goto(BASE);
	await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
	await page.waitForURL("**/realms/einsatzbereit/**", { timeout: 20000 });
	await page.locator("#username").fill("vera");
	await page.locator("#kc-login").click();
	await page.locator("#password").waitFor({ timeout: 10000 });
	await page.locator("#password").fill("vera123");
	await page.locator("#kc-login").click();
	await page.waitForURL(`${BASE}/`, { timeout: 30000 });
	
	// Get token from storage
	const token = await page.evaluate(() => {
		for (let i = 0; i < sessionStorage.length; i++) {
			const key = sessionStorage.key(i);
			if (key && key.includes("oidc.user")) {
				const val = JSON.parse(sessionStorage.getItem(key) || "{}");
				return val.access_token;
			}
		}
		return null;
	});
	
	if (token) {
		// Call /v1/me/engagements directly
		const resp = await ctx.request.get(`${API}/v1/me/engagements`, {
			headers: { Authorization: `Bearer ${token}` }
		});
		console.log(`GET /v1/me/engagements (vera): ${resp.status()}`);
		const body = await resp.text();
		console.log("Response:", body.slice(0, 300));
	}
	
	await ctx.close();
}

// Test 2: Check /v1/volunteer-opportunities/{id}/engagements as olaf
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	await page.goto(BASE);
	await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
	await page.waitForURL("**/realms/einsatzbereit/**", { timeout: 20000 });
	await page.locator("#username").fill("olaf");
	await page.locator("#kc-login").click();
	await page.locator("#password").waitFor({ timeout: 10000 });
	await page.locator("#password").fill("olaf123");
	await page.locator("#kc-login").click();
	await page.waitForURL(`${BASE}/`, { timeout: 30000 });
	
	const token = await page.evaluate(() => {
		for (let i = 0; i < sessionStorage.length; i++) {
			const key = sessionStorage.key(i);
			if (key && key.includes("oidc.user")) {
				const val = JSON.parse(sessionStorage.getItem(key) || "{}");
				return val.access_token;
			}
		}
		return null;
	});
	
	// First get an opportunity ID
	const opResp = await ctx.request.get(`${API}/v1/volunteer-opportunities?PageNumber=1&PageSize=1`);
	const opData = await opResp.json();
	const opId = opData.items?.[0]?.id;
	console.log(`\nOpportunity ID: ${opId}`);
	
	if (token && opId) {
		const resp = await ctx.request.get(`${API}/v1/volunteer-opportunities/${opId}/engagements`, {
			headers: { Authorization: `Bearer ${token}` }
		});
		console.log(`GET /v1/volunteer-opportunities/${opId}/engagements (olaf): ${resp.status()}`);
		const body = await resp.text();
		console.log("Response:", body.slice(0, 300));
	}
	
	await ctx.close();
}

await browser.close();
