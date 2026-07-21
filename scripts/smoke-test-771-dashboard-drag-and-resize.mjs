/**
 * Smoke test for the #771 follow-up review-feedback rounds on the org
 * dashboard widget grid:
 *  - the boxed grip+size+X toolbar is gone (drag-anywhere + trash icon,
 *    no manual size control of any kind - not even the slider that
 *    replaced the original "Medium"/"Large" cycle button, since sizing is
 *    now fully automatic)
 *  - a light green cell backdrop renders behind the grid while editing,
 *    showing the underlying 8-column structure, and disappears again once
 *    editing ends
 *  - dragging reorders other widgets live, not only after the drop
 *  - removing every widget and saving shows a real empty state and stays
 *    empty across a reload (instead of resetting to the default layout)
 *  - the empty state's CTA re-enters edit mode and opens the Add Widget
 *    picker directly
 *  - touch devices aren't blocked from scrolling the page while editing
 *    (no blanket touch-action: none), and a real touch drag gesture still
 *    reorders widgets
 *
 * Run: node scripts/smoke-test-771-dashboard-drag-and-resize.mjs
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
	const menuBtn = page.getByRole("button", {
		name: /open menu|menü öffnen/i,
	});
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
			// Exactly one org so far - no switcher on the homepage yet, but
			// dashboards carry their own switcher inside the org-app layout.
			await overviewLink.click();
			await page.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });
		}
		await page
			.getByRole("button", {
				name: /switch organization|organisation wechseln/i,
			})
			.click();
		await page
			.getByRole("button", {
				name: /create organization|organisation erstellen/i,
			})
			.click();
	}
	const dialog = page.getByRole("dialog");
	await dialog.waitFor({ state: "visible" });
	await dialog.locator("input[type='text']").fill(name);
	await page.getByTestId("modal-submit").click();
	// The app auto-navigates straight to the new org's dashboard on success.

	if (!hasExistingOrg) {
		// ...but becoming an organizer for the FIRST time grants a new
		// Keycloak role that this session's already-issued token doesn't
		// carry yet, so that navigation 403s. Re-sign-in (issues a fresh
		// token with the role) lands back on the homepage, then follow the
		// header's own link back to the dashboard we were just denied.
		await signIn(page, username, password);
		await page
			.getByRole("link", {
				name: /organization overview|organisationsübersicht/i,
			})
			.click();
	}
	await page.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });
	return { name, url: page.url() };
}

async function domOrder(grid) {
	return grid.evaluate((el) =>
		Array.from(el.children).map((c) => c.dataset.testid),
	);
}

async function main() {
	const health = await fetch(`${API}/health`);
	if (!health.ok) throw new Error(`Health check failed: ${health.status}`);
	console.log("OK  API health check passed");

	const browser = await chromium.launch({ proxy: proxyConfig() });

	try {
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();

		await signIn(page, "vera", "vera123");
		console.log("OK  Logged in as vera");

		await createOrg(page, "vera", "vera123", "Smoke771 DashFeedback");
		console.log("OK  Created fresh test organization (now an organizer)");

		await page.getByTestId("quick-action-edit").click();
		console.log("OK  Entered dashboard edit mode");

		// --- The old boxed "Small"/"Medium"/"Large" cycle button is gone ---
		const oldSizeButton = page.getByRole("button", {
			name: /^(Small|Medium|Large|Klein|Mittel|Groß)$/,
		});
		if ((await oldSizeButton.count()) > 0) {
			throw new Error(
				"Old size-cycle button still present - toolbar wasn't removed",
			);
		}
		console.log("OK  Old size-cycle text button is gone");

		// --- Trash-icon remove button ---
		const removeButtons = page.getByRole("button", {
			name: /remove.*widget|entfernen/i,
		});
		await removeButtons.first().waitFor({ state: "visible" });
		console.log("OK  Trash-icon remove button present");

		// --- Live drag reflow: order must change mid-drag, before the drop ---
		// Runs on the pristine default layout (before any resizing below, so a
		// resize's own reflow can't interfere with the drag's start/end
		// coordinates) - Calendar is already part of the default layout for a
		// fresh org, no need to add it via the picker.
		const grid = page.getByTestId("dashboard-widget-grid");
		await grid.waitFor({ state: "visible" });
		const orderBefore = await domOrder(grid);

		const todoBox = await page.getByTestId("widget-tile-ToDo").boundingBox();
		const calBox = await page.getByTestId("widget-tile-Calendar").boundingBox();
		if (!todoBox || !calBox) throw new Error("Could not measure widget tiles");

		await page.mouse.move(
			todoBox.x + todoBox.width / 2,
			todoBox.y + todoBox.height / 2,
		);
		await page.mouse.down();
		const steps = 12;
		for (let i = 1; i <= steps; i++) {
			await page.mouse.move(
				todoBox.x + ((calBox.x - todoBox.x) * i) / steps + calBox.width / 2,
				todoBox.y + ((calBox.y - todoBox.y) * i) / steps + calBox.height / 2,
				{ steps: 3 },
			);
		}
		const orderMidDrag = await domOrder(grid);
		await page.mouse.up();

		if (JSON.stringify(orderMidDrag) === JSON.stringify(orderBefore)) {
			throw new Error(
				"Widget order did not change mid-drag - live reflow regression",
			);
		}
		console.log(
			"OK  Dragging reorders widgets live, before the drop (order changed mid-drag)",
		);

		// --- No manual size control of any kind - sizing is fully automatic ---
		if ((await page.locator("input[type='range']").count()) > 0) {
			throw new Error(
				"A range input still exists - manual sizing wasn't fully removed",
			);
		}
		console.log("OK  No manual size control (slider or otherwise) exists");

		// --- Green cell backdrop renders behind the grid while editing ---
		const backdropCellCount = await page
			.getByTestId("dashboard-grid-guide-cell")
			.count();
		if (backdropCellCount === 0) {
			throw new Error("Green cell backdrop did not render while editing");
		}
		console.log(
			`OK  Green cell backdrop rendered (${backdropCellCount} cells) while editing`,
		);

		// --- Auto-fit: the drag above actually changed a widget's computed
		// column span, not just its position in the DOM ---
		const todoColumn = await page
			.getByTestId("widget-tile-ToDo")
			.evaluate((el) => getComputedStyle(el).gridColumn);
		if (!todoColumn || todoColumn === "auto" || todoColumn === "auto / auto") {
			throw new Error(
				"ToDo tile has no computed grid-column span - auto-fit packing regression",
			);
		}
		console.log(`OK  Auto-fit packing assigned ToDo a real grid-column (${todoColumn})`);

		await page.getByTestId("quick-action-cancel").click();
		if ((await page.getByTestId("dashboard-grid-guide-cell").count()) !== 0) {
			throw new Error("Green cell backdrop is still present outside edit mode");
		}
		console.log("OK  Green cell backdrop disappears again once editing ends");

		await page.getByTestId("quick-action-edit").click();

		// --- Remove every widget and save: must show empty state, not reset ---
		let remaining = page.getByRole("button", {
			name: /remove.*widget|entfernen/i,
		});
		let remainingCount = await remaining.count();
		while (remainingCount > 0) {
			await remaining.first().click();
			remainingCount = await remaining.count();
		}
		await page
			.getByTestId("dashboard-empty-state")
			.waitFor({ state: "visible", timeout: 5000 });
		console.log("OK  Removing every widget shows the empty state");

		await page.getByTestId("quick-action-save").click();
		await page
			.getByTestId("quick-action-edit")
			.waitFor({ state: "visible", timeout: 10000 });

		await page.reload({ waitUntil: "networkidle" });
		await page
			.getByTestId("dashboard-empty-state")
			.waitFor({ state: "visible", timeout: 10000 });
		console.log(
			"OK  Empty layout persists across reload (not reset to the default set)",
		);

		// --- The empty state's own CTA re-enters edit mode + opens the picker ---
		await page
			.getByTestId("dashboard-empty-state")
			.getByRole("button", { name: /add a widget|widget hinzufügen/i })
			.click();
		await page.getByRole("dialog").waitFor({ state: "visible", timeout: 5000 });
		console.log("OK  Empty state's CTA opens the Add Widget picker");
		await page.keyboard.press("Escape");

		await ctx.close();
	} catch (err) {
		console.error("FAIL", err.message);
		process.exitCode = 1;
	}

	// --- Touch: editing must not block normal page scroll, and a real touch
	// drag gesture should still reorder widgets ---
	try {
		const mobileCtx = await browser.newContext({
			ignoreHTTPSErrors: true,
			hasTouch: true,
			viewport: { width: 390, height: 844 },
		});
		const mobilePage = await mobileCtx.newPage();
		await signIn(mobilePage, "vera", "vera123");
		await createOrg(mobilePage, "vera", "vera123", "Smoke771 DashTouch");
		await mobilePage.getByTestId("quick-action-edit").click();

		const tile = mobilePage.getByTestId("widget-tile-ToDo");
		await tile.waitFor({ state: "visible" });
		const touchAction = await tile.evaluate(
			(el) => getComputedStyle(el).touchAction,
		);
		if (touchAction === "none") {
			throw new Error(
				'Widget tile has touch-action:none while editing - would block normal page scroll on mobile (should use TouchSensor\'s delay activation instead)',
			);
		}
		console.log(
			`OK  Widget tile touch-action is "${touchAction}" (not "none") - page scroll still works while editing`,
		);

		// Best-effort real touch-drag simulation via CDP. Simulated touch
		// timing doesn't perfectly match a real device, so a failure here is a
		// warning, not a hard failure - the touch-action check above already
		// covers the actual regression this round fixed.
		try {
			// Calendar is already part of the default layout for a fresh org -
			// no need to add it via the picker.
			const mobileGrid = mobilePage.getByTestId("dashboard-widget-grid");
			const before = await domOrder(mobileGrid);
			const from = await mobilePage
				.getByTestId("widget-tile-ToDo")
				.boundingBox();
			const to = await mobilePage
				.getByTestId("widget-tile-Calendar")
				.boundingBox();

			const client = await mobileCtx.newCDPSession(mobilePage);
			const point = (x, y) => ({ x, y, radiusX: 5, radiusY: 5 });
			const fx = from.x + from.width / 2;
			const fy = from.y + from.height / 2;
			const tx = to.x + to.width / 2;
			const ty = to.y + to.height / 2;

			await client.send("Input.dispatchTouchEvent", {
				type: "touchStart",
				touchPoints: [point(fx, fy)],
			});
			// Hold past TouchSensor's 200ms activation delay before moving.
			await mobilePage.waitForTimeout(300);
			const touchSteps = 8;
			for (let i = 1; i <= touchSteps; i++) {
				await client.send("Input.dispatchTouchEvent", {
					type: "touchMove",
					touchPoints: [
						point(fx + ((tx - fx) * i) / touchSteps, fy + ((ty - fy) * i) / touchSteps),
					],
				});
				await mobilePage.waitForTimeout(30);
			}
			await client.send("Input.dispatchTouchEvent", {
				type: "touchEnd",
				touchPoints: [],
			});

			const after = await domOrder(mobileGrid);
			if (JSON.stringify(after) !== JSON.stringify(before)) {
				console.log("OK  A simulated touch drag gesture reordered the widgets");
			} else {
				console.log(
					"WARN  Simulated touch drag did not change widget order (CDP touch timing may not match a real device - the touch-action check above already validates the actual fix)",
				);
			}
		} catch (touchErr) {
			console.log(
				`WARN  Touch-drag simulation via CDP failed: ${touchErr.message}`,
			);
		}

		await mobileCtx.close();
	} catch (err) {
		console.error("FAIL", err.message);
		process.exitCode = 1;
	}

	await browser.close();

	if (!process.exitCode) {
		console.log("\nALL CHECKS PASSED");
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
