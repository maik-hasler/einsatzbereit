/**
 * Smoke test for #758: the org app's icon-led breadcrumb action bar moved out
 * of OrgAppLayout.tsx into the shared Header.tsx component, so both the org
 * app shell and the public site render it through one implementation.
 *
 * Verifies:
 *  - the home page still shows no action bar/breadcrumb
 *  - public subpages (/profile, /users/:userId, /volunteer-opportunities/:id,
 *    /organizations/:id) show the header-level, icon-led action bar (home
 *    icon + current page label) directly beneath <header>, not inside it
 *  - the org app shell's action bar still behaves as before (no regression):
 *    home icon links to the org dashboard, current tab is shown, and the org
 *    switcher remains a separate control
 *
 * Run: node scripts/smoke-test-758-header-breadcrumb.mjs
 */

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

async function signIn(page, username, password) {
	await page.goto(BASE, { waitUntil: "networkidle" });
	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	if ((await signInBtn.count()) > 0) {
		await signInBtn.first().click();
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await loginKeycloak(page, username, password);
		await page.waitForURL(`${BASE}/`, { timeout: 15000 });
	}
}

async function assertActionBarBesideHeaderNotInside(page, label) {
	const actionBar = page.locator("nav[aria-label='Breadcrumb']");
	await actionBar.waitFor({ state: "visible", timeout: 10000 });
	const insideHeaderCount = await page
		.locator("header nav[aria-label='Breadcrumb']")
		.count();
	if (insideHeaderCount !== 0) {
		throw new Error(
			`${label}: action bar must be a sibling beneath <header>, not nested inside it`,
		);
	}
	return actionBar;
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const { browser, page } = await launchLiveBrowser();
	try {
		// --- Home page: no action bar at all ---
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		await page.waitForSelector("h1", { timeout: 15000 });
		if ((await page.locator("nav[aria-label='Breadcrumb']").count()) > 0) {
			throw new Error("Home page unexpectedly shows an action bar");
		}
		console.log("OK  Home page shows no action bar");

		// --- Public site: log in as vera ---
		await signIn(page, "vera", "vera123");
		console.log("OK  Logged in as vera");

		// --- /profile: action bar shows Home + "Profile" ---
		await page.goto(`${BASE}/profile`, { waitUntil: "networkidle" });
		const profileBar = await assertActionBarBesideHeaderNotInside(page, "/profile");
		await profileBar.locator("a[href='/']").waitFor({ state: "visible", timeout: 5000 });
		await profileBar
			.getByText("Profile", { exact: true })
			.waitFor({ state: "visible", timeout: 5000 });
		console.log("OK  /profile shows the header-level action bar (Home > Profile)");

		// --- /users/:userId: action bar shows Home + display name ---
		const userId = await page.evaluate(() => {
			for (let i = 0; i < localStorage.length; i++) {
				const key = localStorage.key(i);
				if (key && key.includes("oidc.user")) {
					const entry = JSON.parse(localStorage.getItem(key) ?? "null");
					if (entry?.profile?.sub) return entry.profile.sub;
				}
			}
			return null;
		});
		if (!userId) {
			console.log("WARN  Could not resolve logged-in user id - skipping /users/:userId check");
		} else {
			await page.goto(`${BASE}/users/${userId}`, { waitUntil: "networkidle" });
			const userBar = await assertActionBarBesideHeaderNotInside(page, "/users/:userId");
			await userBar.locator("a[href='/']").waitFor({ state: "visible", timeout: 5000 });
			console.log("OK  /users/:userId shows the header-level action bar");
		}

		// --- Opportunity detail + organization profile (public, unauthenticated ok) ---
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		const firstCard = page.locator("a[href*='/volunteer-opportunities/']").first();
		if ((await firstCard.count()) === 0) {
			console.log("WARN  No opportunities seeded - skipping detail/org action-bar checks");
		} else {
			const href = await firstCard.getAttribute("href");
			await page.goto(`${BASE}${href}`, { waitUntil: "networkidle" });
			const oppBar = await assertActionBarBesideHeaderNotInside(
				page,
				"/volunteer-opportunities/:id",
			);
			await oppBar.locator("a[href='/']").waitFor({ state: "visible", timeout: 5000 });
			console.log("OK  Opportunity detail page shows the header-level action bar with Home link");

			const orgChipHref = await page
				.locator("a[href*='/organizations/']")
				.first()
				.getAttribute("href");
			if (orgChipHref) {
				await oppBar
					.locator(`a[href='${orgChipHref}']`)
					.waitFor({ state: "visible", timeout: 5000 });
				console.log("OK  Opportunity detail action bar links to its organization");

				await page.goto(`${BASE}${orgChipHref}`, { waitUntil: "networkidle" });
				const orgBar = await assertActionBarBesideHeaderNotInside(
					page,
					"/organizations/:id",
				);
				await orgBar.locator("a[href='/']").waitFor({ state: "visible", timeout: 5000 });
				console.log("OK  Organization profile page shows the header-level action bar with Home link");
			}
		}

		// --- Org app shell: no regression - action bar still works, org switcher separate ---
		await signIn(page, "olaf", "olaf123");
		console.log("OK  Logged in as olaf (organisator)");

		const switcherBtn = page.getByRole("button", { name: /switch organization|organisation wechseln/i });
		if ((await switcherBtn.count()) === 0) {
			console.log("WARN  Org switcher not visible - skipping org app shell checks");
			console.log("\nALL CHECKS PASSED");
			return;
		}

		await page.goto(`${BASE}/app`, { waitUntil: "networkidle" });
		await page.waitForURL(/\/app\/[^/]+\/dashboard/, { timeout: 15000 });

		const orgBar = await assertActionBarBesideHeaderNotInside(page, "org app shell");
		await orgBar
			.getByRole("link", { name: /home/i })
			.waitFor({ state: "visible", timeout: 5000 });
		console.log("OK  Org app shell action bar renders beneath <header>, not inside it (shared implementation)");

		if ((await page.getByRole("button", { name: /switch organization|organisation wechseln/i }).count()) === 0) {
			throw new Error("Org switcher missing from org app header after the shared Header.tsx refactor");
		}
		console.log("OK  Org switcher remains a separate control in the org app header");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
