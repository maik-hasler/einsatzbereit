import { chromium } from "playwright";

const FRONTEND = "https://einsatzbereit.maik-hasler.de";

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({
	ignoreHTTPSErrors: true,
	geolocation: { latitude: 48.1351, longitude: 11.582 },
	permissions: ["geolocation"],
});
const page = await ctx.newPage();

// Log console messages
page.on("console", (msg) => console.log(`[browser] ${msg.type()}: ${msg.text()}`));
page.on("pageerror", (err) => console.log(`[browser error] ${err.message}`));

// Inject geolocation debug
await page.addInitScript(() => {
	const orig = navigator.geolocation.getCurrentPosition.bind(navigator.geolocation);
	navigator.geolocation.getCurrentPosition = (success, error, opts) => {
		console.log("[geolocation] getCurrentPosition called");
		return orig(
			(pos) => { console.log(`[geolocation] success: ${pos.coords.latitude}, ${pos.coords.longitude}`); success(pos); },
			(err) => { console.log(`[geolocation] error: ${err.code} ${err.message}`); error?.(err); },
			opts
		);
	};
});

await page.goto(FRONTEND, { waitUntil: "networkidle", timeout: 30000 });

const locationBtn = page.getByRole("button").filter({ hasText: /location/i }).first();
await locationBtn.click();
await page.waitForTimeout(500);

const nearMeBtn = page.getByRole("button", { name: /use my current location/i });
console.log("Near me visible:", await nearMeBtn.isVisible());
await nearMeBtn.click();
console.log("Clicked near me button");
await page.waitForTimeout(5000);

console.log("URL after 5s:", page.url());
await page.screenshot({ path: "/home/user/einsatzbereit/scripts/debug-nearme-after-click.png" });
await browser.close();
