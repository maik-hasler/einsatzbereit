// Smoke test for #637 (PR #638): HomePage.tsx rendered two independent
// implementations of the #374 onboarding banner whenever showOnboarding was
// true - a plain inline block and the reusable OnboardingBanner component -
// stacking two "Welcome to Einsatzbereit!" boxes, with the inline one
// showing the raw untranslated key "onboarding.message" instead of real
// copy (a duplicate top-level "onboarding" JSON key in en.json/de.json
// meant JSON.parse silently dropped the "message"-keyed one). PR #638
// deleted the dead inline block and the dead locale key. Verifies exactly
// one banner renders with real copy and no raw key text.
//
// No throwaway data is created (the banner is gated purely by the
// "onboarding-dismissed" localStorage key, not server state), so there is
// nothing to clean up (see #630).
// Run: node scripts/smoke-test-637-onboarding-banner.mjs

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const { browser, page } = await launchLiveBrowser();
	try {
		await page.goto(BASE, { waitUntil: "networkidle" });
		await page.click("text=/sign in|anmelden/i");
		await page.waitForURL(/\/realms\//, { timeout: 30000 });
		await loginKeycloak(page, "vera", "vera123");

		await page.goto(BASE, { waitUntil: "networkidle" });

		const banners = page.getByRole("region", { name: "Welcome banner" });
		const count = await banners.count();
		if (count !== 1) {
			throw new Error(`Expected exactly 1 "Welcome banner" region, found ${count}`);
		}
		console.log('OK  Exactly one "Welcome banner" region rendered');

		const bannerText = await banners.innerText();
		const expectedBody =
			"Browse volunteer opportunities near you, sign up with one click, and earn badges as you help your community.";
		if (!bannerText.includes(expectedBody)) {
			throw new Error(`Banner text missing expected real copy. Got: ${bannerText}`);
		}
		console.log("OK  Banner shows real translated body copy");

		if (bannerText.includes("onboarding.message")) {
			throw new Error('Banner text still contains the raw "onboarding.message" key');
		}
		console.log('OK  No raw "onboarding.message" translation key leaked into the page');
	} finally {
		await browser.close();
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
