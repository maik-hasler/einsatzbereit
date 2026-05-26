import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await ctx.newPage();

// Intercept responses
const apiResults = {};
page.on("response", async resp => {
  const url = resp.url();
  if (url.includes("/v1/me/engagements") || url.includes("/v1/volunteer-opportunities")) {
    try {
      const body = await resp.text();
      apiResults[url] = { status: resp.status(), body: body.slice(0, 500) };
    } catch {}
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

await page.goto(`${BASE}/my-engagements`);
await page.waitForLoadState("networkidle");

console.log("API results:");
for (const [url, r] of Object.entries(apiResults)) {
  console.log(`  ${r.status} ${url}`);
  console.log(`  Body: ${r.body}`);
}

// Also navigate to olaf's engagement management
await ctx.close();

const ctx2 = await browser.newContext({ ignoreHTTPSErrors: true });
const page2 = await ctx2.newPage();
const apiResults2 = {};
page2.on("response", async resp => {
  const url = resp.url();
  if (url.includes("/v1/") && url.includes("engagements")) {
    try {
      const body = await resp.text();
      apiResults2[url] = { status: resp.status(), body: body.slice(0, 500) };
    } catch {}
  }
});

await page2.goto(BASE);
await page2.getByRole("button", { name: /sign in|anmelden/i }).first().click();
await page2.waitForURL("**/realms/einsatzbereit/**", { timeout: 20000 });
await page2.locator("#username").fill("olaf");
await page2.locator("#kc-login").click();
await page2.locator("#password").waitFor({ timeout: 10000 });
await page2.locator("#password").fill("olaf123");
await page2.locator("#kc-login").click();
await page2.waitForURL(`${BASE}/`, { timeout: 30000 });

// Go to engagement management for the opportunity
const opLink = page2.locator("main a[href*='/volunteer-opportunities/']").first();
if (await opLink.isVisible({ timeout: 5000 }).catch(() => false)) {
  const href = await opLink.getAttribute("href");
  const opId = href?.match(/volunteer-opportunities\/([^/]+)/)?.[1];
  if (opId) {
    await page2.goto(`${BASE}/organizations/${opId}/engagements`).catch(() => {});
    // try the management page
    await page2.goto(`${BASE}/volunteer-opportunities/${opId}`);
    await page2.waitForLoadState("networkidle");
    const engBtn = page2.getByRole("button", { name: /manage engagements|teilnehmer|manage/i });
    if (await engBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await engBtn.click();
      await page2.waitForLoadState("networkidle");
    }
  }
}

console.log("\nOlaf API results:");
for (const [url, r] of Object.entries(apiResults2)) {
  console.log(`  ${r.status} ${url}`);
  console.log(`  Body: ${r.body}`);
}

await ctx2.close();
await browser.close();
