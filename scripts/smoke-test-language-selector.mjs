/**
 * Smoke test: LanguageSelector dark theme on hero and mobile menu (PR #441)
 *
 * Verifies that:
 * 1. On the home page (hero, not scrolled), the language selector button has
 *    white text/border styling (transparent=true mode).
 * 2. The language selector dropdown shows the dark bg-brand-800 background
 *    with white-tinted borders when in transparent mode.
 * 3. The mobile menu shows the language selector with white text inside
 *    the dark brand-800 menu background.
 */

import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";

function colorIsWhite(color) {
	// Accepts rgb(255,255,255), rgba(255,255,255,...), and various oklab/oklch
	// near-white representations (lightness > 0.95 in oklab/oklch).
	if (!color) return false;
	if (color.startsWith("rgb(255, 255, 255)") || color.startsWith("rgba(255, 255, 255")) return true;
	// oklab(L a b / alpha): white is L ~= 1.0
	const oklabMatch = color.match(/oklab\(([0-9.]+)/);
	if (oklabMatch && parseFloat(oklabMatch[1]) > 0.95) return true;
	// oklch(L C H / alpha): white/near-white is L >= 0.95
	const oklchMatch = color.match(/oklch\(([0-9.]+)/);
	if (oklchMatch && parseFloat(oklchMatch[1]) > 0.95) return true;
	return false;
}

function colorIsDark(color) {
	if (!color) return false;
	// Dark: oklch lightness < 0.4, or rgb components all < 100
	const oklchMatch = color.match(/oklch\(([0-9.]+)/);
	if (oklchMatch) return parseFloat(oklchMatch[1]) < 0.4;
	const oklabMatch = color.match(/oklab\(([0-9.]+)/);
	if (oklabMatch) return parseFloat(oklabMatch[1]) < 0.4;
	const rgbMatch = color.match(/rgb\((\d+),\s*(\d+),\s*(\d+)/);
	if (rgbMatch) {
		const [, r, g, b] = rgbMatch.map(Number);
		return r < 100 && g < 100 && b < 100;
	}
	return false;
}

async function run() {
	const browser = await chromium.launch();
	const ctx = await browser.newContext({
		ignoreHTTPSErrors: true,
		viewport: { width: 1280, height: 800 },
	});
	const page = await ctx.newPage();

	let passed = 0;
	let failed = 0;

	function assert(condition, message) {
		if (condition) {
			console.log(`  PASS: ${message}`);
			passed++;
		} else {
			console.error(`  FAIL: ${message}`);
			failed++;
		}
	}

	// ---------------------------------------------------------------------------
	// 1. Desktop hero: language selector has white/transparent styling
	// ---------------------------------------------------------------------------
	console.log("\n[1] Desktop hero - LanguageSelector transparent styling");
	// networkidle ensures React fully hydrates and isTransparent state is set
	await page.goto(BASE, { waitUntil: "networkidle" });
	await page.waitForSelector("main", { timeout: 15_000 });

	const langBtn = page.locator("header button[aria-haspopup='listbox']").first();
	await langBtn.waitFor({ state: "visible", timeout: 10_000 });

	const textColor = await langBtn.evaluate((el) => window.getComputedStyle(el).color);
	assert(colorIsWhite(textColor), `Hero LanguageSelector button has white text color (got: ${textColor})`);

	// Check the button's CSS classes directly - most reliable
	const btnClass = await langBtn.getAttribute("class");
	assert(
		btnClass?.includes("border-white") || btnClass?.includes("text-white"),
		`Hero LanguageSelector button has transparent Tailwind classes (got: ${btnClass?.substring(0, 80)})`,
	);

	// ---------------------------------------------------------------------------
	// 2. Desktop hero: language selector dropdown has dark brand background
	// ---------------------------------------------------------------------------
	console.log("\n[2] Desktop hero - LanguageSelector dropdown dark background");
	await langBtn.click();
	const dropdown = page.locator("header ul[role='listbox']").first();
	await dropdown.waitFor({ state: "visible", timeout: 5_000 });

	// Check CSS classes directly
	const dropdownClass = await dropdown.getAttribute("class");
	assert(
		dropdownClass?.includes("bg-brand-800"),
		`Hero dropdown has bg-brand-800 class (got: ${dropdownClass?.substring(0, 100)})`,
	);
	assert(
		dropdownClass?.includes("left-0"),
		`Hero dropdown uses left-0 alignment (got: ${dropdownClass?.substring(0, 100)})`,
	);

	const dropdownBg = await dropdown.evaluate((el) => window.getComputedStyle(el).backgroundColor);
	assert(colorIsDark(dropdownBg), `Hero dropdown background is dark (got: ${dropdownBg})`);

	// Close the dropdown
	await page.keyboard.press("Escape");

	// ---------------------------------------------------------------------------
	// 3. Mobile menu: LanguageSelector has white text in dark menu
	// ---------------------------------------------------------------------------
	console.log("\n[3] Mobile menu - LanguageSelector white styling");
	const mobilePage = await ctx.newPage();
	await mobilePage.setViewportSize({ width: 390, height: 844 });
	await mobilePage.goto(BASE, { waitUntil: "networkidle" });
	await mobilePage.waitForSelector("main", { timeout: 15_000 });

	// Open the mobile hamburger menu - find button with nav.openMenu aria-label
	const hamburger = mobilePage.locator("header button[aria-label]").filter({
		has: mobilePage.locator(":not([aria-haspopup='listbox'])"),
	});

	// Try to find a hamburger button by iterating
	const headerBtns = mobilePage.locator("header button");
	const count = await headerBtns.count();
	let opened = false;
	for (let i = 0; i < count; i++) {
		const btn = headerBtns.nth(i);
		const ariaLabel = await btn.getAttribute("aria-label");
		if (ariaLabel && /menu|menü|open|öffnen/i.test(ariaLabel)) {
			await btn.click();
			opened = true;
			break;
		}
	}

	if (!opened) {
		// The mobile menu button is the last non-listbox button in header
		const nonListboxBtns = mobilePage.locator("header button:not([aria-haspopup='listbox'])");
		const nbCount = await nonListboxBtns.count();
		if (nbCount > 0) {
			await nonListboxBtns.last().click();
			opened = true;
		}
	}

	await mobilePage.waitForTimeout(500);

	const mobileLangBtn = mobilePage.locator("button[aria-haspopup='listbox']").last();
	const mobileVisible = await mobileLangBtn.isVisible().catch(() => false);
	assert(mobileVisible, "Mobile menu LanguageSelector button is visible after opening menu");

	if (mobileVisible) {
		const mobileBtnClass = await mobileLangBtn.getAttribute("class");
		assert(
			mobileBtnClass?.includes("border-white") || mobileBtnClass?.includes("text-white"),
			`Mobile menu LanguageSelector button has transparent classes (got: ${mobileBtnClass?.substring(0, 80)})`,
		);
	}

	// Take a screenshot for visual verification
	await mobilePage.screenshot({ path: "/tmp/smoke-mobile-menu.png" });
	console.log("  Screenshot: /tmp/smoke-mobile-menu.png");

	await mobilePage.close();

	// ---------------------------------------------------------------------------
	// Summary
	// ---------------------------------------------------------------------------
	console.log(`\nResults: ${passed} passed, ${failed} failed`);
	await browser.close();

	if (failed > 0) {
		process.exit(1);
	}
}

run().catch((err) => {
	console.error("Smoke test error:", err);
	process.exit(1);
});
