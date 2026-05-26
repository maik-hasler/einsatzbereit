import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await ctx.newPage();

const consoleMsgs = [];
const networkErrors = [];
const apiCalls = [];

page.on("console", msg => consoleMsgs.push(`[${msg.type()}] ${msg.text()}`));
page.on("pageerror", err => networkErrors.push(`PageError: ${err.message}`));
page.on("response", async resp => {
	if (resp.url().includes("api.maik-hasler.de")) {
		let body = "";
		try { body = await resp.text(); } catch {}
		apiCalls.push(`${resp.status()} ${resp.url()} | ${body.slice(0, 300)}`);
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

const bodyText = await page.locator("body").textContent();
const h1 = await page.locator("h1").first().textContent().catch(() => "none");

console.log("H1:", h1);
console.log("Body (500 chars):", bodyText?.slice(0, 500));
console.log("\nConsole:");
consoleMsgs.forEach(m => console.log(" ", m));
console.log("\nPage errors:");
networkErrors.forEach(e => console.log(" ", e));
console.log("\nAPI calls:");
apiCalls.forEach(c => console.log(" ", c));

await browser.close();
