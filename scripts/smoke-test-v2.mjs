// smoke-test-v2.mjs - Deeper staging smoke test (round 5)
// Focuses on: map/filter UX, org creation, i18n, perf, a11y, auth edge-cases

import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API  = "https://api.maik-hasler.de";
const AUTH = "https://login.maik-hasler.de/realms/einsatzbereit";
const CLIENT_ID = "frontend";

let passed = 0, failed = 0, notes = [];

function ok(label) { console.log(`  [PASS] ${label}`); passed++; }
function ko(label, err) { console.log(`  [FAIL] ${label}: ${err?.message ?? err}`); failed++; }
function note(label) { console.log(`  [NOTE] ${label}`); notes.push(label); }

async function api(method, path, token, body) {
	const res = await fetch(`${API}/v1${path}`, {
		method,
		headers: {
			"Content-Type": "application/json",
			...(token ? { Authorization: `Bearer ${token}` } : {}),
		},
		...(body ? { body: JSON.stringify(body) } : {}),
	});
	let data = null;
	try { data = await res.clone().json(); } catch {}
	return { status: res.status, data };
}

async function getToken(page, user, pass) {
	await page.goto(`${BASE}/`, { waitUntil: "domcontentloaded" });
	const btn = page.locator("button, a").filter({ hasText: /sign in|anmelden/i }).first();
	await btn.click();
	await page.waitForURL(/login\.maik-hasler\.de/);
	await page.fill("#username", user);
	await page.click("#kc-login");
	await page.fill("#password", pass);
	await page.click("#kc-login");
	await page.waitForURL(BASE + "/**", { timeout: 10000 });
	return page.evaluate(([auth, cid]) => {
		const raw = localStorage.getItem(`oidc.user:${auth}:${cid}`);
		return raw ? JSON.parse(raw).access_token : null;
	}, [AUTH, CLIENT_ID]);
}

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await ctx.newPage();

// ── Suite 1: API performance benchmarks ──────────────────────────────────────
console.log("\n=== Suite 1: API performance ===");
try {
	const endpoints = [
		["/volunteer-opportunities?PageNumber=1&PageSize=10", "opportunity list"],
		["/badges", "badge catalog"],
	];
	for (const [path, label] of endpoints) {
		const t0 = Date.now();
		const { status } = await api("GET", path, null);
		const ms = Date.now() - t0;
		if (status === 200 && ms < 2000) ok(`${label} responds in ${ms}ms`);
		else if (status === 200) note(`${label} slow: ${ms}ms (>2s)`);
		else ko(`${label}`, new Error(`HTTP ${status}`));
	}
} catch (e) { ko("Suite 1", e); }

// ── Suite 2: Map view renders and has pins ────────────────────────────────────
console.log("\n=== Suite 2: Map view ===");
try {
	await page.goto(BASE, { waitUntil: "networkidle" });
	// Toggle to map view
	const mapBtn = page.locator("button").filter({ hasText: /map|karte/i }).first();
	if (await mapBtn.count() > 0) {
		await mapBtn.click();
		await page.waitForTimeout(1500);
		const leaflet = await page.locator(".leaflet-container").count();
		if (leaflet > 0) ok("Map container rendered");
		else ko("Map view", new Error("No leaflet-container found after toggle"));
		// Check for map markers
		const markers = await page.locator(".leaflet-marker-icon").count();
		if (markers > 0) ok(`Map has ${markers} marker(s)`);
		else note("Map rendered but no markers visible in current viewport");
		// Switch back to list
		const listBtn = page.locator("button").filter({ hasText: /list|liste/i }).first();
		if (await listBtn.count() > 0) await listBtn.click();
	} else {
		note("No map toggle button found");
	}
} catch (e) { ko("Suite 2", e); }

// ── Suite 3: Filter URL persistence ──────────────────────────────────────────
console.log("\n=== Suite 3: Filter URL persistence ===");
try {
	await page.goto(BASE, { waitUntil: "networkidle" });
	// Type in search box
	const search = page.locator("input[type=text], input[placeholder*=search], input[placeholder*=Suche]").first();
	if (await search.count() > 0) {
		await search.fill("Tier");
		await page.waitForTimeout(600);
		const url1 = page.url();
		const hasQ = url1.includes("Tier") || url1.includes("q=") || url1.includes("search=");
		if (hasQ) ok("Search term reflected in URL");
		else note("Search does not update URL (no deep-link support for search)");

		// Reload and check filter persists
		await page.reload({ waitUntil: "networkidle" });
		const valAfterReload = await search.inputValue().catch(() => "");
		if (valAfterReload.includes("Tier")) ok("Search persists across reload");
		else note("Search term cleared on reload (URL-driven state not restored to input)");
	} else {
		note("No search input found on homepage");
	}
} catch (e) { ko("Suite 3", e); }

// ── Suite 4: Language switch (EN <-> DE) ─────────────────────────────────────
console.log("\n=== Suite 4: Language switch ===");
try {
	await page.goto(BASE, { waitUntil: "networkidle" });
	// Find language selector
	const langBtn = page.locator("button, select").filter({ hasText: /de|en|deutsch|english/i }).first();
	if (await langBtn.count() > 0) {
		const before = await page.textContent("body");
		await langBtn.click();
		await page.waitForTimeout(800);
		// Try clicking DE or EN option
		const deOption = page.locator("button, li, option").filter({ hasText: /^(de|deutsch)$/i }).first();
		const enOption = page.locator("button, li, option").filter({ hasText: /^(en|english)$/i }).first();
		if (await deOption.count() > 0) {
			await deOption.click();
			await page.waitForTimeout(500);
			ok("Language switched to DE");
			// Verify some German text appears
			const bodyDE = await page.textContent("body");
			if (bodyDE !== before) ok("Page text changed after language switch");
			else note("Language switch did not visually change page text");
			// Switch back to EN
			const langBtn2 = page.locator("button, select").filter({ hasText: /de|en|deutsch|english/i }).first();
			if (await langBtn2.count() > 0) {
				await langBtn2.click();
				await page.waitForTimeout(400);
				if (await enOption.count() > 0) await enOption.click();
			}
		} else if (await enOption.count() > 0) {
			await enOption.click();
			ok("Language option (EN) clickable");
		} else {
			ok("Language button exists and is clickable");
		}
	} else {
		note("No language selector found in header");
	}
} catch (e) { ko("Suite 4", e); }

// ── Suite 5: 404 page ────────────────────────────────────────────────────────
console.log("\n=== Suite 5: 404 / Not Found page ===");
try {
	const response = await page.goto(`${BASE}/this-page-does-not-exist-xyz`, { waitUntil: "networkidle" });
	// SPA 404 - the server returns 200 (SPA) but the app renders a not-found state
	const body = (await page.textContent("body") ?? "").toLowerCase();
	if (body.includes("404") || body.includes("not found") || body.includes("nicht gefunden") || body.includes("seite nicht")) {
		ok("404/not-found page renders correctly");
	} else {
		note("Unknown path did not render a clear 404 message (may be redirecting or rendering blank)");
	}
} catch (e) { ko("Suite 5", e); }

// ── Suite 6: Auth - login, protected route, logout ───────────────────────────
console.log("\n=== Suite 6: Auth flow ===");
let veraToken = null;
try {
	const authPage = await ctx.newPage();
	veraToken = await getToken(authPage, "vera", "vera123");
	if (veraToken) ok("vera login produces JWT");
	else ko("vera login", new Error("No token extracted"));

	// Visit protected route while authenticated
	await authPage.goto(`${BASE}/my-engagements`, { waitUntil: "networkidle" });
	const url = authPage.url();
	if (url.includes("my-engagements")) ok("Protected /my-engagements accessible while logged in");
	else note(`Protected route redirected to: ${url}`);

	// Logout
	const logoutBtn = authPage.locator("button, a").filter({ hasText: /log.?out|abmelden/i }).first();
	if (await logoutBtn.count() > 0) {
		await logoutBtn.click();
		await authPage.waitForTimeout(1500);
		const urlAfter = authPage.url();
		if (!urlAfter.includes("my-engagements")) ok("Logout redirects away from protected page");
		else note("Logout did not navigate away from protected page");
	} else {
		note("No logout button found");
	}
	await authPage.close();
} catch (e) { ko("Suite 6", e); }

// ── Suite 7: Protected route unauthenticated redirect ────────────────────────
console.log("\n=== Suite 7: Unauthenticated redirect ===");
try {
	const anonCtx = await browser.newContext({ ignoreHTTPSErrors: true });
	const anonPage = await anonCtx.newPage();
	await anonPage.goto(`${BASE}/my-engagements`, { waitUntil: "networkidle" });
	const url = anonPage.url();
	if (url.includes("login.maik-hasler.de") || url.includes("callback")) {
		ok("Unauthenticated /my-engagements redirects to Keycloak");
	} else if (!url.includes("my-engagements")) {
		ok("Unauthenticated redirect works (redirected elsewhere)");
	} else {
		note(`Protected page accessible without login? URL: ${url}`);
	}
	await anonCtx.close();
} catch (e) { ko("Suite 7", e); }

// ── Suite 8: API auth boundary ───────────────────────────────────────────────
console.log("\n=== Suite 8: API auth boundaries ===");
try {
	// Protected endpoints without token
	const protectedPaths = [
		["/me/engagements", "GET /me/engagements"],
		["/me/achievements", "GET /me/achievements"],
		["/me/notifications", "GET /me/notifications"],
	];
	for (const [path, label] of protectedPaths) {
		const { status } = await api("GET", path, null);
		if (status === 401) ok(`${label} requires auth (401)`);
		else if (status === 403) ok(`${label} requires auth (403)`);
		else ko(label, new Error(`Expected 401/403, got ${status}`));
	}

	// Non-existent resource
	const { status: s404 } = await api("GET", "/volunteer-opportunities/00000000-0000-0000-0000-000000000001", null);
	if (s404 === 404) ok("Non-existent opportunity returns 404");
	else note(`Non-existent opportunity returns ${s404} (expected 404)`);
} catch (e) { ko("Suite 8", e); }

// ── Suite 9: Opportunity detail page completeness ────────────────────────────
console.log("\n=== Suite 9: Opportunity detail page ===");
try {
	const { status, data } = await api("GET", "/volunteer-opportunities?PageNumber=1&PageSize=1", null);
	if (status === 200 && data?.items?.length > 0) {
		const opp = data.items[0];
		await page.goto(`${BASE}/volunteer-opportunities/${opp.id}`, { waitUntil: "networkidle" });
		const body = await page.textContent("body") ?? "";
		// Title should appear
		if (body.includes(opp.title)) ok("Opportunity title rendered on detail page");
		else note("Opportunity title not found in detail page body");
		// Check for sign-up button or status (not logged in = show button)
		const hasSignup = await page.locator("button").filter({ hasText: /join|interest|waitlist|anmelden/i }).count() > 0;
		if (hasSignup) ok("Sign-up button visible for unauthenticated user");
		else note("No sign-up button visible on detail page (unauthenticated)");
		// Check for map (single marker for address)
		const mapPresent = await page.locator(".leaflet-container").count() > 0;
		if (mapPresent) ok("Location map rendered on detail page");
		else note("No location map on detail page");
		// Check category/tags (filed as #351)
		const hasBadges = await page.locator(".rounded-full, .badge, [class*=badge]").count() > 0;
		if (hasBadges) ok("Detail page shows badges/chips");
		else note("No category/tag badges visible on detail page");
	} else {
		note("No opportunities available to test detail page");
	}
} catch (e) { ko("Suite 9", e); }

// ── Suite 10: Organization profile page ──────────────────────────────────────
console.log("\n=== Suite 10: Organization profile page ===");
try {
	const { status, data } = await api("GET", "/organizations?PageNumber=1&PageSize=1", null);
	if (status === 200 && data?.items?.length > 0) {
		const org = data.items[0];
		await page.goto(`${BASE}/organizations/${org.id}`, { waitUntil: "networkidle" });
		const body = await page.textContent("body") ?? "";
		if (body.includes(org.name)) ok(`Org profile renders: "${org.name}"`);
		else note("Org name not found in profile body");
		// Check for opportunities section
		const hasOpps = body.toLowerCase().includes("opportunit") || body.toLowerCase().includes("einsatz");
		if (hasOpps) ok("Org profile shows opportunities section");
		else note("No opportunities section visible on org profile");
	} else {
		note(`Organizations endpoint returned ${status} - may require auth or not exist`);
	}
} catch (e) { ko("Suite 10", e); }

// ── Suite 11: Keyboard navigation (a11y) ─────────────────────────────────────
console.log("\n=== Suite 11: Keyboard navigation (a11y) ===");
try {
	await page.goto(BASE, { waitUntil: "networkidle" });
	// Tab through first 5 focusable elements and check focus is visible
	const focusedElements = [];
	for (let i = 0; i < 6; i++) {
		await page.keyboard.press("Tab");
		const focused = await page.evaluate(() => {
			const el = document.activeElement;
			return el ? el.tagName + (el.textContent?.trim().slice(0, 30) ?? "") : null;
		});
		if (focused && focused !== "BODY") focusedElements.push(focused);
	}
	if (focusedElements.length >= 3) ok(`Tab navigation works: ${focusedElements.slice(0, 3).join(", ")}`);
	else note(`Only ${focusedElements.length} elements received focus via Tab`);

	// Check for skip link (filed as #361)
	await page.keyboard.press("Shift+Tab");
	// Focus the first element
	await page.evaluate(() => (document.body.focus?.(), document.activeElement?.blur()));
	await page.keyboard.press("Tab");
	const firstFocused = await page.evaluate(() => document.activeElement?.textContent?.trim().slice(0, 40));
	if (firstFocused?.toLowerCase().includes("skip") || firstFocused?.toLowerCase().includes("main")) {
		ok("Skip-to-main-content link is the first focusable element");
	} else {
		note(`First focusable element is "${firstFocused}" - no skip link (see #361)`);
	}
} catch (e) { ko("Suite 11", e); }

// ── Suite 12: Org creation (olaf) ────────────────────────────────────────────
console.log("\n=== Suite 12: Organization creation flow ===");
try {
	if (veraToken) {
		// Try to list orgs via API first
		const { status, data } = await api("GET", "/organizations?PageNumber=1&PageSize=5", veraToken);
		if (status === 200) {
			ok(`Organizations endpoint accessible: ${data?.items?.length ?? 0} org(s)`);
		} else {
			note(`Organizations endpoint status: ${status}`);
		}
		// Check org creation endpoint response shape
		const ts = Date.now();
		const { status: s } = await api("POST", "/organizations", veraToken, {
			name: `TestOrg-${ts}`,
			description: "Automated test org",
		});
		if (s === 201) ok("Organization created via API");
		else if (s === 403) note("vera cannot create organizations (no organisator role - expected)");
		else if (s === 400) note("Organization creation returned 400 (validation)");
		else note(`Organization creation returned ${s}`);
	} else {
		note("Skipping org creation - no vera token");
	}
} catch (e) { ko("Suite 12", e); }

// ── Suite 13: Notifications API (vera) ───────────────────────────────────────
console.log("\n=== Suite 13: Notifications ===");
try {
	if (veraToken) {
		const { status, data } = await api("GET", "/me/notifications", veraToken);
		if (status === 200) {
			ok(`Notifications endpoint returns ${data?.length ?? 0} items`);
			// Check structure
			if (Array.isArray(data) && data.length > 0) {
				const n = data[0];
				const hasKind = "kind" in n;
				const hasMsgField = "message" in n;
				const hasActionUrl = "actionUrl" in n;
				if (hasKind) ok("Notification has 'kind' field");
				if (!hasMsgField) note("Notification missing 'message' field (see #367)");
				if (!hasActionUrl) note("Notification missing 'actionUrl' field (see #367)");
			}
			// Mark all as read
			const { status: markStatus } = await api("PUT", "/me/notifications/read-all", veraToken);
			if (markStatus === 204 || markStatus === 200) ok("Mark-all-read works");
			else note(`Mark-all-read returned ${markStatus}`);
		} else {
			ko("Notifications", new Error(`HTTP ${status}`));
		}
	} else {
		note("Skipping notifications - no vera token");
	}
} catch (e) { ko("Suite 13", e); }

// ── Suite 14: Streak + achievements (vera) ───────────────────────────────────
console.log("\n=== Suite 14: Streak + achievements ===");
try {
	if (veraToken) {
		const { status: sStr, data: streak } = await api("GET", "/me/streak", veraToken);
		if (sStr === 200) ok(`Streak: current=${streak?.currentStreak}, longest=${streak?.longestStreak}`);
		else note(`Streak endpoint returned ${sStr}`);

		const { status: sAch, data: ach } = await api("GET", "/me/achievements", veraToken);
		if (sAch === 200) ok(`Achievements: ${Array.isArray(ach) ? ach.length : "?"} earned`);
		else note(`Achievements endpoint returned ${sAch}`);
	}
} catch (e) { ko("Suite 14", e); }

// ── Suite 15: Pagination edge cases ──────────────────────────────────────────
console.log("\n=== Suite 15: Pagination edge cases ===");
try {
	// Last page beyond data (should return empty items, not error)
	const { status: s1, data: d1 } = await api("GET", "/volunteer-opportunities?PageNumber=9999&PageSize=10", null);
	if (s1 === 200) ok(`PageNumber=9999 returns 200 with ${d1?.items?.length ?? "?"} items`);
	else note(`PageNumber=9999 returns ${s1}`);

	// PageNumber=0 should be 400 (filed as #362)
	const { status: s0 } = await api("GET", "/volunteer-opportunities?PageNumber=0&PageSize=10", null);
	if (s0 === 400) ok("PageNumber=0 correctly returns 400 (fix verified)");
	else if (s0 === 500) note("PageNumber=0 still returns 500 (see #362 - not fixed yet)");
	else note(`PageNumber=0 returns ${s0}`);

	// PageSize over 100
	const { status: sBig } = await api("GET", "/volunteer-opportunities?PageNumber=1&PageSize=999", null);
	if (sBig === 400) ok("PageSize=999 correctly capped (fix verified)");
	else if (sBig === 200) note("PageSize=999 accepted without cap (see #363 - not fixed yet)");
	else note(`PageSize=999 returns ${sBig}`);
} catch (e) { ko("Suite 15", e); }

// ── Suite 16: Homepage render performance ────────────────────────────────────
console.log("\n=== Suite 16: Homepage render performance ===");
try {
	const t0 = Date.now();
	await page.goto(BASE, { waitUntil: "networkidle" });
	const loadTime = Date.now() - t0;
	if (loadTime < 3000) ok(`Homepage fully loaded in ${loadTime}ms`);
	else note(`Homepage load time: ${loadTime}ms (>3s - consider optimization)`);

	// Check core web vitals via JS
	const lcp = await page.evaluate(() => {
		return new Promise((resolve) => {
			new PerformanceObserver((list) => {
				const entries = list.getEntries();
				if (entries.length) resolve(entries[entries.length - 1].startTime);
			}).observe({ type: "largest-contentful-paint", buffered: true });
			setTimeout(() => resolve(null), 2000);
		});
	});
	if (lcp && lcp < 2500) ok(`LCP: ${Math.round(lcp)}ms (good)`);
	else if (lcp) note(`LCP: ${Math.round(lcp)}ms (>2.5s - needs improvement)`);
	else note("LCP measurement timed out");
} catch (e) { ko("Suite 16", e); }

// ── Suite 17: OpenAPI spec completeness ──────────────────────────────────────
console.log("\n=== Suite 17: OpenAPI spec ===");
try {
	const r = await fetch(`${API}/openapi/v1.json`);
	const spec = await r.json();
	const paths = Object.keys(spec.paths ?? {});
	ok(`OpenAPI spec has ${paths.length} paths`);

	// Check for undocumented patterns
	const hasMeNotif = paths.some(p => p.includes("notifications"));
	const hasMeStreak = paths.some(p => p.includes("streak"));
	const hasBadges = paths.some(p => p.includes("badges"));
	const hasOrgs = paths.some(p => p.includes("organizations"));
	if (hasMeNotif) ok("Notifications documented in spec");
	else note("Notifications NOT in OpenAPI spec");
	if (hasMeStreak) ok("Streak documented in spec");
	else note("Streak NOT in OpenAPI spec");
	if (hasBadges) ok("Badges documented in spec");
	else note("Badges NOT in OpenAPI spec");
	if (hasOrgs) ok("Organizations documented in spec");
	else note("Organizations NOT in OpenAPI spec");
} catch (e) { ko("Suite 17", e); }

await browser.close();

// ── Summary ──────────────────────────────────────────────────────────────────
console.log(`\n${"=".repeat(60)}`);
console.log(`Results: ${passed} passed, ${failed} failed`);
if (notes.length) {
	console.log(`\nNotes (${notes.length}):`);
	notes.forEach(n => console.log(`  - ${n}`));
}
if (failed > 0) process.exit(1);
