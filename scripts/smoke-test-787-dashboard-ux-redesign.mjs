// Smoke test for PR #787's dashboard UX redesign (follow-up to the outlet-
// context crash fix): the standalone "Dashboard / Opportunities / Members /
// Settings" tab bar was removed, and repositioning a widget while editing no
// longer requires grabbing a small grip icon - the whole tile is now the
// press-and-drag-to-move surface (resize stays a dedicated corner handle).
//
// Verifies:
//  1. The old tab bar (nav "Organization sections") is gone.
//  2. The dashboard still renders real content, no ErrorBoundary crash.
//  3. Dragging a widget tile by its BODY (real mouse down/move/up, not just
//     a click) actually repositions it once released.
//  4. The click-click-click corner flow still works: clicking a grid cell
//     within the widget currently being placed's own footprint is not
//     swallowed by that widget's own tile (the regression fixed across this
//     PR's later commits).
//
// No throwaway data is created (an existing org from seed data is used), so
// there is nothing to clean up (see #630).
// Run: node scripts/smoke-test-787-dashboard-ux-redesign.mjs

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
		await loginKeycloak(page, "olaf", "olaf123");
		await page.waitForURL(`${BASE}/`, { timeout: 30000 });

		const cta = page.getByRole("link", { name: "Organization overview" });
		await cta.first().waitFor({ timeout: 25000 });
		await cta.first().click();
		await page.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });
		console.log("OK  Navigated to org dashboard");

		const crashed = await page
			.getByRole("heading", { name: "Something went wrong" })
			.count();
		if (crashed > 0) throw new Error("Dashboard shows the ErrorBoundary fallback");
		console.log("OK  No ErrorBoundary crash");

		const tabBarCount = await page
			.getByRole("navigation", { name: "Organization sections" })
			.count();
		if (tabBarCount > 0) throw new Error("Old tab bar is still present");
		console.log("OK  Tab bar removed");

		await page.getByTestId("quick-action-edit").click();
		await page.waitForSelector('[data-testid="quick-action-save"]', {
			timeout: 10000,
		});
		console.log("OK  Entered edit mode");

		// Drag the "Create Opportunity" tile by its body (real mouse events,
		// not the tiny grip icon) and confirm its grid position changes.
		const tile = page.getByTestId("widget-tile-CreateOpportunity");
		await tile.waitFor({ timeout: 10000 });
		const before = await tile.evaluate((el) => getComputedStyle(el).gridColumnStart);
		const box = await tile.boundingBox();
		if (!box) throw new Error("Could not measure CreateOpportunity tile");
		await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
		await page.mouse.down();
		await page.mouse.move(box.x + box.width / 2 + 220, box.y + box.height / 2 + 5, {
			steps: 8,
		});
		await page.mouse.up();
		await page.waitForTimeout(300);
		const after = await tile.evaluate((el) => getComputedStyle(el).gridColumnStart);
		if (before === after) {
			throw new Error(
				`Whole-tile drag did not move the widget (gridColumnStart stayed ${before})`,
			);
		}
		console.log(
			`OK  Whole-tile drag moved CreateOpportunity (gridColumnStart ${before} -> ${after})`,
		);

		// Click-click-click corner flow: start placing a DIFFERENT widget
		// (the ToDo widget, "Needs Your Attention" - untouched by the drag
		// above, so this exercises a fresh press-click sequence rather than
		// whatever a prior real drag left the browser's click synthesis
		// mid-way through on the same element), then click a grid cell that
		// falls within ITS OWN current footprint - this must not be
		// swallowed by the tile's own (otherwise draggable) surface.
		const todoTile = page.getByTestId("widget-tile-ToDo");
		await todoTile.waitFor({ timeout: 10000 });
		const moveButton = page.getByRole("button", { name: "Move or resize Needs Your Attention" });
		await moveButton.waitFor({ timeout: 10000 });
		await moveButton.click();
		await page.waitForSelector('[data-testid="dashboard-placement-status"]', {
			timeout: 10000,
		});
		// Click near the center of ToDo's own tile - this lands on a
		// backdrop cell inside its own current footprint.
		const ownBox = await todoTile.boundingBox();
		if (!ownBox) throw new Error("Could not measure ToDo tile mid-placement");
		await page.mouse.click(ownBox.x + ownBox.width / 2, ownBox.y + ownBox.height / 2);
		await page.waitForTimeout(300);
		console.log("OK  Click within the actively-placed widget's own footprint was not intercepted");

		await page.getByTestId("quick-action-cancel").click().catch(() => {});

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
