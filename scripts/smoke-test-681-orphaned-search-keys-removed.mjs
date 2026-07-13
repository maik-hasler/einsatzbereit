// Live verification for #681: the repository owner clarified that keyword
// search was intentionally removed, and asked for the orphaned i18n keys
// left behind (opportunities.searchPlaceholder, .noResultsWithSearch,
// .clearSearch, .filterLabelSearch) to be deleted. This is a pure
// dead-code removal with no user-visible behavior change - this script
// confirms the homepage still loads cleanly (no i18n/render regressions)
// and that the existing filter bar still renders, since the removed keys
// used to live in the same locale namespace.
import { launchLiveBrowser } from "./lib/live-browser.mjs";

const SITE = "https://einsatzbereit.maik-hasler.de";

const { browser, page } = await launchLiveBrowser();
const consoleErrors = [];
page.on("console", (msg) => {
	if (msg.type() === "error") consoleErrors.push(msg.text());
});
page.on("pageerror", (err) => consoleErrors.push(String(err)));

try {
	await page.goto(SITE, { waitUntil: "networkidle" });

	const heading = page.getByRole("heading", { name: "Current Opportunities" });
	await heading.first().waitFor({ state: "visible", timeout: 15000 });
	console.log("PASS: homepage loaded, opportunities heading visible");

	// The filter bar (which shares the same locale namespace as the removed
	// keys) still renders and translates correctly.
	const frequencyFilter = page.getByTestId("filter-frequency");
	await frequencyFilter.waitFor({ state: "visible", timeout: 10000 });
	console.log("PASS: frequency filter still renders");

	const typeFilter = page.getByTestId("filter-type");
	await typeFilter.waitFor({ state: "visible", timeout: 10000 });
	console.log("PASS: type filter still renders");

	// No leftover search input anywhere on the page.
	const searchInputCount = await page
		.locator('input[placeholder*="Search" i], input[placeholder*="Suche" i]')
		.count();
	if (searchInputCount !== 0) {
		throw new Error(
			`Expected no search input on the page, found ${searchInputCount}`,
		);
	}
	console.log("PASS: no leftover keyword-search input present");

	if (consoleErrors.length > 0) {
		throw new Error(`Console errors detected:\n${consoleErrors.join("\n")}`);
	}
	console.log(
		"PASS: no console errors (i18n key removal didn't break anything)",
	);

	console.log("\nAll checks passed.");
} finally {
	await browser.close();
}
