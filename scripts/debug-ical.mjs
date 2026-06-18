import { chromium } from "playwright";

const API = "https://api.maik-hasler.de";
const FRONTEND = "https://einsatzbereit.maik-hasler.de";

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();

// Login first
await page.goto(`${FRONTEND}`, { waitUntil: "domcontentloaded", timeout: 30000 });
const signinBtn = page.getByRole("button", { name: /sign in|anmelden/i });
if (await signinBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
    await signinBtn.click();
}
await page.waitForURL(/login\.maik-hasler\.de|keycloak/, { timeout: 15000 }).catch(() => {});
await page.fill("#username", "vera");
await page.click("#kc-login");
await page.fill("#password", "vera123");
await page.click("#kc-login");
await page.waitForLoadState("networkidle", { timeout: 30000 });
console.log("Logged in, on:", page.url());

// Find opportunity link
const oppLink = page.locator("a[href*='/volunteer-opportunities/']").first();
const oppVisible = await oppLink.isVisible({ timeout: 8000 }).catch(() => false);
console.log("Opportunity link visible:", oppVisible);

if (oppVisible) {
    const href = await oppLink.getAttribute("href");
    console.log("Navigating to:", href);
    await oppLink.click();
    await page.waitForLoadState("networkidle", { timeout: 30000 });
    console.log("On detail page:", page.url());
    
    // Try to find any links
    const allLinks = await page.locator("a").all();
    console.log("Total links on page:", allLinks.length);
    
    for (const link of allLinks.slice(0, 30)) {
        const href2 = await link.getAttribute("href");
        const download = await link.getAttribute("download");
        const visible = await link.isVisible().catch(() => false);
        if (href2 || download) {
            console.log(`  - href="${href2}" download="${download}" visible=${visible}`);
        }
    }
    
    // Check for calendar specifically
    const calLinks = await page.locator("a[href*='calendar'], a[download]").all();
    console.log("Calendar/download links:", calLinks.length);
    for (const l of calLinks) {
        const href2 = await l.getAttribute("href");
        const dl = await l.getAttribute("download");
        console.log(`  - href="${href2}" download="${dl}"`);
    }
}

await browser.close();
