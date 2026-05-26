import { chromium } from "playwright";
import fs from "fs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const SCREENSHOTS = "/home/user/einsatzbereit/scripts/screenshots";

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1280, height: 900 } });
const page = await ctx.newPage();

const apiResults = {};
page.on("response", async resp => {
	if (resp.url().includes("api.maik-hasler.de/v1/")) {
		try { apiResults[resp.url()] = `${resp.status()}`; } catch {}
	}
});

// Login as vera
await page.goto(BASE);
await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
await page.waitForURL("**/realms/einsatzbereit/**", { timeout: 20000 });
await page.locator("#username").fill("vera");
await page.locator("#kc-login").click();
await page.locator("#password").waitFor({ timeout: 10000 });
await page.locator("#password").fill("vera123");
await page.locator("#kc-login").click();
await page.waitForURL(`${BASE}/`, { timeout: 30000 });

// Opportunity detail - check sign up area
const opId = "019e5652-576f-7a50-8df4-9f706b7e50d6";
await page.goto(`${BASE}/volunteer-opportunities/${opId}`);
await page.waitForLoadState("networkidle");
const mainText = await page.locator("main").textContent();
console.log("Opportunity detail main content:");
console.log(mainText?.slice(0, 1000));

// Check for sign-up related buttons
const buttons = await page.locator("main button").all();
console.log("\nButtons in main:");
for (const btn of buttons) {
	const text = await btn.textContent().catch(() => "");
	const visible = await btn.isVisible().catch(() => false);
	if (visible) console.log(" -", text?.trim());
}

await page.screenshot({ path: `${SCREENSHOTS}/19-opportunity-detail-signup.png`, fullPage: true });

// Check notification bell
await page.goto(BASE);
await page.waitForLoadState("networkidle");
const bellBtn = page.locator("header button[aria-label*='notif'], header button[aria-label*='bell'], header .bell, header [data-testid*='notif']").first();
const hasBell = await bellBtn.isVisible({ timeout: 2000 }).catch(() => false);
console.log("\nNotification bell found:", hasBell);

// Try clicking the bell icon (the one that looks like a bell in the header)
const headerButtons = await page.locator("header button").all();
console.log("All header buttons:");
for (const btn of headerButtons) {
	const text = await btn.textContent().catch(() => "");
	const ariaLabel = await btn.getAttribute("aria-label").catch(() => "");
	console.log(` - text: "${text?.trim()}" aria-label: "${ariaLabel}"`);
}

// Check if there's a user menu and sign-out
const userMenu = page.locator("header").getByRole("button", { name: /sign out|abmelden|account|user/i }).first();
const hasUserMenu = await userMenu.isVisible({ timeout: 2000 }).catch(() => false);
console.log("\nUser menu/sign-out button visible:", hasUserMenu);

// Try clicking user avatar/initials button
const avatarBtn = page.locator("header button").filter({ hasText: /^[A-Z]{1,2}$/ }).first();
const hasAvatar = await avatarBtn.isVisible({ timeout: 2000 }).catch(() => false);
if (hasAvatar) {
	await avatarBtn.click();
	await page.waitForTimeout(500);
	await page.screenshot({ path: `${SCREENSHOTS}/20-user-menu.png`, fullPage: false });
	const menuText = await page.locator("body").textContent();
	console.log("\nAfter avatar click, body includes:", 
		menuText?.match(/sign out|abmelden|logout|profile|account|settings/i)?.[0] ?? "nothing notable");
}

console.log("\nAPI calls made:", JSON.stringify(apiResults, null, 2));
await browser.close();
