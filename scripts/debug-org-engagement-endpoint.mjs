import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await ctx.newPage();

const apiResults = {};
page.on("response", async resp => {
	const url = resp.url();
	if (url.includes("api.maik-hasler.de/v1/")) {
		try {
			const body = await resp.text();
			apiResults[url] = `${resp.status()} ${body.slice(0, 200)}`;
		} catch {}
	}
});

// Login as olaf (organisator)
await page.goto(BASE);
await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
await page.waitForURL("**/realms/einsatzbereit/**", { timeout: 20000 });
await page.locator("#username").fill("olaf");
await page.locator("#kc-login").click();
await page.locator("#password").waitFor({ timeout: 10000 });
await page.locator("#password").fill("olaf123");
await page.locator("#kc-login").click();
await page.waitForURL(`${BASE}/`, { timeout: 30000 });

// Navigate to engagement management
const opId = "019e5652-576f-7a50-8df4-9f706b7e50d6"; // from earlier
await page.goto(`${BASE}/volunteer-opportunities/${opId}/engagements`);
await page.waitForLoadState("networkidle");

const bodyText = await page.locator("body").textContent();
console.log("Body:", bodyText?.slice(0, 400));
console.log("\nAPI calls:");
for (const [url, r] of Object.entries(apiResults)) {
	console.log(`  ${url}: ${r}`);
}

await browser.close();
