import { chromium } from "playwright";
import fs from "fs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const SCREENSHOTS = "/home/user/einsatzbereit/scripts/screenshots";

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1280, height: 900 } });
const page = await ctx.newPage();

// Login
await page.goto(BASE);
await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
await page.waitForURL("**/realms/einsatzbereit/**", { timeout: 20000 });
await page.locator("#username").fill("vera");
await page.locator("#kc-login").click();
await page.locator("#password").waitFor({ timeout: 10000 });
await page.locator("#password").fill("vera123");
await page.locator("#kc-login").click();
await page.waitForURL(`${BASE}/`, { timeout: 30000 });

// Get vera's JWT roles
const roles = await page.evaluate(() => {
	for (let i = 0; i < sessionStorage.length; i++) {
		const key = sessionStorage.key(i);
		if (key && key.includes("oidc.user")) {
			const val = JSON.parse(sessionStorage.getItem(key) || "{}");
			const profile = val.profile;
			return profile;
		}
	}
	return null;
});
console.log("Vera's JWT profile:", JSON.stringify(roles, null, 2));

// Click notification bell
const bellBtn = page.locator("button[aria-label='Notifications']");
await bellBtn.click();
await page.waitForTimeout(500);
await page.screenshot({ path: `${SCREENSHOTS}/21-notifications-panel.png`, fullPage: false });
const bodyAfterBell = await page.locator("body").textContent();
console.log("Body after bell click (first 300 chars after header):",
	bodyAfterBell?.slice(0, 500));

// Check if any panel/dropdown appeared
const hasDropdown = await page.locator('[role="dialog"], [role="listbox"], [aria-live], .notification-panel, [data-testid*="notif"]').first().isVisible({ timeout: 1000 }).catch(() => false);
console.log("Notification panel appeared:", hasDropdown);

// Check visible elements near the bell
const nearBell = await page.locator("header").innerHTML();
console.log("Header HTML (for notification area):", nearBell?.slice(0, 1000));

// Check opportunity detail - look at vera's roles
const opId = "019e5652-576f-7a50-8df4-9f706b7e50d6";
await page.goto(`${BASE}/volunteer-opportunities/${opId}`);
await page.waitForLoadState("networkidle");

const allButtons = await page.locator("button").all();
console.log("\nAll buttons on opportunity detail page:");
for (const btn of allButtons) {
	const text = await btn.textContent().catch(() => "");
	const visible = await btn.isVisible().catch(() => false);
	console.log(` - "${text?.trim()}" visible=${visible}`);
}

await browser.close();
