import { chromium } from "playwright";

const FRONTEND = "https://einsatzbereit.maik-hasler.de";

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await ctx.newPage();

await page.goto(FRONTEND, { waitUntil: "networkidle", timeout: 30000 });

// Find all buttons and print their text
const buttons = await page.getByRole("button").all();
console.log("All visible buttons before opening filter:");
for (const btn of buttons) {
	const text = (await btn.textContent())?.trim().slice(0, 80);
	const ariaLabel = await btn.getAttribute("aria-label");
	const visible = await btn.isVisible();
	if (visible) console.log(`  text="${text}" aria-label="${ariaLabel}"`);
}

// Click the location filter
const locationBtn = page.getByRole("button").filter({ hasText: /location|standort/i }).first();
await locationBtn.click();
await page.waitForTimeout(500);

console.log("\nAll visible buttons after opening Location filter:");
const buttons2 = await page.getByRole("button").all();
for (const btn of buttons2) {
	const text = (await btn.textContent())?.trim().slice(0, 80);
	const ariaLabel = await btn.getAttribute("aria-label");
	const visible = await btn.isVisible();
	if (visible) console.log(`  text="${text}" aria-label="${ariaLabel}"`);
}

await page.screenshot({ path: "/home/user/einsatzbereit/scripts/debug-location-filter.png" });
await browser.close();
