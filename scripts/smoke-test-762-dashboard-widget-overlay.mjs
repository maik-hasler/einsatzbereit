/**
 * Smoke test for the #762 follow-up review feedback on the org dashboard
 * widget grid: real widget tiles must render INSIDE the green cell backdrop
 * (while editing), not fall out of CSS Grid's auto-placement into a separate
 * stack of cards below the whole backdrop.
 *
 * Root cause of the bug this guards against: the green backdrop cells claim
 * every single cell of the 8-column grid via explicit gridColumn/gridRow
 * line numbers. A widget tile placed with only `gridColumn: span N` (no
 * explicit start line) has nowhere left to auto-place into, so the browser
 * pushed every widget into new rows generated after the entire backdrop -
 * i.e. below it, not on top of it. The fix gives each tile the same explicit
 * start line the packer already computed for the backdrop.
 *
 * Run: node scripts/smoke-test-762-dashboard-widget-overlay.mjs
 * Requires a plain user account (vera/vera123) - it self-serves organizer
 * access by creating a fresh org via the homepage's own "create
 * organization" entry point, same as any first-time organizer would.
 */

import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

// This machine's own corporate proxy (unlike the Claude-Code-web sandbox's
// TLS-reterminating egress proxy that scripts/lib/live-browser.mjs works
// around) - plain HTTPS through it works fine, no special TLS downgrade
// flags or a pinned executablePath needed.
function proxyConfig() {
	return process.env.HTTPS_PROXY
		? { server: process.env.HTTPS_PROXY }
		: undefined;
}

async function signIn(page, username, password) {
	await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
	// Narrow (mobile) viewports collapse the header into a hamburger menu -
	// "Sign in" only exists once that menu is opened.
	const menuBtn = page.getByRole("button", { name: /open menu|menü öffnen/i });
	if ((await menuBtn.count()) > 0) await menuBtn.click();

	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	if ((await signInBtn.count()) > 0) {
		await signInBtn.first().click();
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		// Single-step Keycloak form (both fields shown at once, unlike the
		// two-step username-then-password flow) - fill both, submit once.
		await page.fill("#username", username);
		await page.fill("#password", password);
		await page.click("#kc-login");
		await page.waitForLoadState("networkidle", { timeout: 30000 });
	}
	await page.waitForSelector("main", { timeout: 15000 });
}

// The homepage header shows one of three things depending on how many orgs
// this account already organizes: nothing org-related + a hero "create
// organization" button (0 orgs), a direct "organization overview" link (1
// org), or a "switch organization" dropdown (2+ orgs) - only the last one
// exposes its own "create organization" entry directly from the homepage.
async function createOrg(page, username, password, namePrefix) {
	const name = `${namePrefix} ${Date.now()}`;
	const switcher = page.getByRole("button", {
		name: /switch organization|organisation wechseln/i,
	});
	const overviewLink = page.getByRole("link", {
		name: /organization overview|organisationsübersicht/i,
	});
	const hasExistingOrg =
		(await switcher.count()) > 0 || (await overviewLink.count()) > 0;

	if (!hasExistingOrg) {
		await page
			.getByRole("button", {
				name: /create organization|organisation erstellen/i,
			})
			.first()
			.click();
	} else {
		if ((await switcher.count()) === 0) {
			await overviewLink.click();
			await page.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });
		}
		await page
			.getByRole("button", { name: /switch organization|organisation wechseln/i })
			.click();
		await page
			.getByRole("button", { name: /create organization|organisation erstellen/i })
			.click();
	}
	const dialog = page.getByRole("dialog");
	await dialog.waitFor({ state: "visible" });
	await dialog.locator("input[type='text']").fill(name);
	await page.getByTestId("modal-submit").click();

	if (!hasExistingOrg) {
		// Becoming an organizer for the FIRST time grants a new Keycloak role
		// this session's already-issued token doesn't carry yet, so the
		// auto-navigation to the new dashboard 403s. Re-sign-in (issues a
		// fresh token with the role) lands back on the homepage, then follow
		// the header's own link back to the dashboard we were just denied.
		await signIn(page, username, password);
		await page
			.getByRole("link", { name: /organization overview|organisationsübersicht/i })
			.click();
	}
	await page.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });
}

async function main() {
	const health = await fetch(`${API}/health`);
	if (!health.ok) throw new Error(`Health check failed: ${health.status}`);
	console.log("OK  API health check passed");

	const browser = await chromium.launch({ proxy: proxyConfig() });
	const context = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await context.newPage();

	try {
		await signIn(page, "vera", "vera123");
		console.log("OK  Logged in as vera");

		await createOrg(page, "vera", "vera123", "Smoke762 DashOverlay");
		console.log("OK  Created fresh test organization (now an organizer)");

		await page.getByTestId("quick-action-edit").click();
		console.log("OK  Entered dashboard edit mode");

		const backdropCells = page.getByTestId("dashboard-grid-guide-cell");
		await backdropCells.first().waitFor({ state: "visible" });
		const cellCount = await backdropCells.count();
		if (cellCount === 0) throw new Error("Green cell backdrop did not render");
		const firstCellBox = await backdropCells.first().boundingBox();
		const lastCellBox = await backdropCells.last().boundingBox();
		if (!firstCellBox || !lastCellBox) {
			throw new Error("Could not measure backdrop cell bounding boxes");
		}
		console.log(`OK  Green cell backdrop rendered (${cellCount} cells)`);

		// CreateOpportunity is the first widget in the default layout, so the
		// packer places it at row 1, col 1 - its tile's top edge should sit
		// right at the first backdrop cell's top edge (within a small
		// tolerance for the backdrop's `-m-1` bleed). Under the bug, the
		// tile was pushed hundreds of pixels below the backdrop's very last
		// row instead, in a separate stack of cards.
		const createBox = await page
			.getByTestId("widget-tile-CreateOpportunity")
			.boundingBox();
		if (!createBox) throw new Error("Could not measure CreateOpportunity tile");

		const topGap = Math.abs(createBox.y - firstCellBox.y);
		if (topGap > 20) {
			throw new Error(
				`CreateOpportunity tile is not aligned with the top of the backdrop ` +
					`(gap ${topGap.toFixed(1)}px) - widgets are falling out of the grid again`,
			);
		}
		console.log(
			`OK  CreateOpportunity tile renders at the top of the grid (${topGap.toFixed(1)}px from first backdrop cell)`,
		);

		if (createBox.y >= lastCellBox.y + lastCellBox.height) {
			throw new Error(
				"CreateOpportunity tile renders below the entire green backdrop - " +
					"the #762 overlay bug has regressed",
			);
		}
		console.log("OK  Widget tile renders within the backdrop's bounds, not below it");

		// ToDo is the second widget in the default layout and packs onto the
		// same shelf row as CreateOpportunity (side by side) - both should
		// start at roughly the same top edge, proving the grid places
		// multiple widgets across a row instead of every widget cascading
		// into its own row below the last.
		const todoBox = await page.getByTestId("widget-tile-ToDo").boundingBox();
		if (!todoBox) throw new Error("Could not measure ToDo tile");
		const sideBySideGap = Math.abs(todoBox.y - createBox.y);
		if (sideBySideGap > 20) {
			throw new Error(
				`ToDo tile is not side-by-side with CreateOpportunity (gap ${sideBySideGap.toFixed(1)}px) - ` +
					"auto-fit packing regression",
			);
		}
		console.log("OK  ToDo tile packs side-by-side with CreateOpportunity on the same row");

		// Explicit grid placement sanity check: the computed gridColumnStart
		// must be a real line number, not "auto" - confirms the fix's
		// explicit `col / span N` syntax actually resolved.
		const columnStart = await page
			.getByTestId("widget-tile-CreateOpportunity")
			.evaluate((el) => getComputedStyle(el).gridColumnStart);
		if (!columnStart || columnStart === "auto") {
			throw new Error(
				`CreateOpportunity tile has no explicit grid-column-start (got "${columnStart}") - ` +
					"the explicit-placement fix regressed",
			);
		}
		console.log(`OK  Widget tile has an explicit grid-column-start (${columnStart})`);

		await context.close();
		console.log("\nALL CHECKS PASSED");
	} catch (err) {
		console.error("FAIL", err.message);
		process.exitCode = 1;
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
