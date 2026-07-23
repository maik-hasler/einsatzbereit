// Smoke test for PR #798, a follow-up round-trip on #783's review feedback
// (https://github.com/maik-hasler/einsatzbereit/pull/783#issuecomment-5049781309):
// "You added more buttons, I can't move anything else - basically its just
// not working at all anymore. When I move it to a different sized monitor,
// everything becomes a weird size."
//
// Verifies:
//  1. The dedicated width-only/height-only edge resize handles added in
//     918119c are gone - only the corner handle remains, alongside the
//     grip (move) and remove buttons.
//  2. Whole-tile drag-to-move still works (regression guard - #798 touched
//     the same pointer-drag code path the edge handles used to share).
//  3. A widget tile's on-screen shape (row height relative to column width)
//     stays consistent across two different viewport widths, instead of
//     row height staying frozen at a flat pixel value while column width
//     scales with the viewport - the actual "weird size on a different
//     monitor" bug.
//
// No throwaway data is created (an existing org from seed data is used), so
// there is nothing to clean up (see #630).
// Run: node scripts/smoke-test-798-dashboard-widget-roundtrip.mjs

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const { browser, page } = await launchLiveBrowser();
	try {
		await page.setViewportSize({ width: 1400, height: 900 });
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

		await page.getByTestId("quick-action-edit").click();
		await page.waitForSelector('[data-testid="quick-action-save"]', {
			timeout: 10000,
		});
		console.log("OK  Entered edit mode");

		// 1. Fewer buttons: the two edge-only resize handles must be gone, the
		// corner handle must still be there.
		const widthHandles = await page.getByTestId("widget-resize-handle-width").count();
		const heightHandles = await page.getByTestId("widget-resize-handle-height").count();
		if (widthHandles > 0 || heightHandles > 0) {
			throw new Error(
				`Edge resize handles still present (width=${widthHandles}, height=${heightHandles})`,
			);
		}
		const cornerHandles = await page.getByTestId("widget-resize-handle-corner").count();
		if (cornerHandles < 1) throw new Error("Corner resize handle is missing");
		console.log(
			`OK  Edge resize handles removed, corner handle present (${cornerHandles} tiles)`,
		);

		// 2. Whole-tile drag still moves a widget (regression guard).
		const tile = page.getByTestId("widget-tile-CreateOpportunity");
		await tile.waitFor({ timeout: 10000 });
		const gridColBefore = await tile.evaluate((el) => getComputedStyle(el).gridColumnStart);
		let box = await tile.boundingBox();
		if (!box) throw new Error("Could not measure CreateOpportunity tile");
		await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
		await page.mouse.down();
		await page.mouse.move(box.x + box.width / 2 + 220, box.y + box.height / 2 + 5, {
			steps: 8,
		});
		await page.mouse.up();
		await page.waitForTimeout(300);
		const gridColAfter = await tile.evaluate((el) => getComputedStyle(el).gridColumnStart);
		if (gridColBefore === gridColAfter) {
			throw new Error(
				`Whole-tile drag did not move the widget (gridColumnStart stayed ${gridColBefore})`,
			);
		}
		console.log(
			`OK  Whole-tile drag moved CreateOpportunity (gridColumnStart ${gridColBefore} -> ${gridColAfter})`,
		);
		await page.getByTestId("quick-action-cancel").click();
		await page.waitForSelector('[data-testid="quick-action-edit"]', { timeout: 10000 });
		await page.getByTestId("quick-action-edit").click();
		await page.waitForSelector('[data-testid="quick-action-save"]', { timeout: 10000 });

		// 3. Grid cell shape stays consistent across viewport widths - the
		// "different sized monitor" regression. Settings is full-width
		// (x=1, width=8) in DEFAULT_LAYOUT, so its tile's per-column width is
		// simple to derive from its own bounding box at any viewport.
		const settingsTile = page.getByTestId("widget-tile-Settings");
		await settingsTile.waitFor({ timeout: 10000 });

		async function perCellShape() {
			const b = await settingsTile.boundingBox();
			if (!b) throw new Error("Could not measure Settings tile");
			// DEFAULT_LAYOUT: Settings is width=8, height=2 (widgetCatalog.ts).
			return { w: b.width / 8, h: b.height / 2 };
		}

		const wide = await perCellShape();
		await page.setViewportSize({ width: 1024, height: 900 });
		await page.waitForTimeout(300);
		const narrow = await perCellShape();

		if (Math.abs(wide.w - narrow.w) < 5) {
			throw new Error(
				"Column width did not actually change between viewports - test setup is not meaningful",
			);
		}
		const widthRatio = narrow.w / wide.w;
		const heightRatio = narrow.h / wide.h;
		const drift = Math.abs(widthRatio - heightRatio);
		if (drift > 0.15) {
			throw new Error(
				`Row height is not tracking column width across viewports (width scaled by ${widthRatio.toFixed(2)}x, height by ${heightRatio.toFixed(2)}x) - a widget's shape would look different on a different monitor`,
			);
		}
		console.log(
			`OK  Grid cell shape stays consistent across viewport widths (width x${widthRatio.toFixed(2)}, height x${heightRatio.toFixed(2)})`,
		);

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
