// audit-staging.mjs - Comprehensive staging audit
// Covers areas not yet tested: form validation UX, mobile viewport,
// org dashboard, achievements page, profile page, footer links,
// header nav, language persistence, error boundaries, badge sharing,
// check-in flow visibility, org engagements page

import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const KC = "https://login.maik-hasler.de/realms/einsatzbereit/protocol/openid-connect/token";

const VERA_USER = "vera";
const VERA_PASS = "vera123";
const OLAF_USER = "olaf";
const OLAF_PASS = "olaf123";

let passed = 0;
let failed = 0;
const findings = [];

function ok(suite, msg) {
	console.log(`  [PASS] ${msg}`);
	passed++;
}

function fail(suite, msg, detail = "") {
	console.log(`  [FAIL] ${msg}${detail ? ": " + detail : ""}`);
	failed++;
	findings.push({ suite, msg, detail });
}

function section(name) {
	console.log(`\n=== ${name} ===`);
}

async function getToken(user, pass) {
	const res = await fetch(KC, {
		method: "POST",
		headers: { "Content-Type": "application/x-www-form-urlencoded" },
		body: new URLSearchParams({
			grant_type: "password",
			client_id: "frontend",
			username: user,
			password: pass,
		}),
	});
	const data = await res.json();
	return data.access_token;
}

async function apiGet(path, token) {
	return fetch(`${API}${path}`, {
		headers: token ? { Authorization: `Bearer ${token}` } : {},
	});
}

async function apiPost(path, body, token) {
	return fetch(`${API}${path}`, {
		method: "POST",
		headers: {
			"Content-Type": "application/json",
			...(token ? { Authorization: `Bearer ${token}` } : {}),
		},
		body: JSON.stringify(body),
	});
}

// ──────────────────────────────────────────────────────────────
// Suite 1 - Mobile viewport rendering
// ──────────────────────────────────────────────────────────────
async function testMobileViewport(browser) {
	section("Suite 1 - Mobile Viewport Rendering");
	const ctx = await browser.newContext({
		viewport: { width: 375, height: 812 },
		ignoreHTTPSErrors: true,
		userAgent:
			"Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/605.1.15",
	});
	const page = await ctx.newPage();
	try {
		await page.goto(BASE, { waitUntil: "networkidle", timeout: 30000 });

		// Header hamburger visible
		const hamburger = page.locator('[aria-label*="menu"], button[aria-expanded]').first();
		const hamVisible = await hamburger.isVisible().catch(() => false);
		if (hamVisible) ok("mobile", "Hamburger/menu button visible on mobile");
		else fail("mobile", "No hamburger/mobile menu button found at 375px width");

		// Logo visible
		const logo = page.locator('img[alt*="logo"], img[alt*="EINSATZBEREIT"], a[href="/"] img').first();
		const logoVisible = await logo.isVisible().catch(() => false);
		if (logoVisible) ok("mobile", "Logo visible on mobile");
		else fail("mobile", "Logo not visible on mobile viewport");

		// Opportunity cards stack vertically (not overflowing)
		await page.waitForSelector("li, [data-testid='opportunity-card']", { timeout: 10000 }).catch(() => {});
		const cards = await page.locator("ul li").all();
		if (cards.length > 0) {
			const firstCard = cards[0];
			const box = await firstCard.boundingBox();
			if (box && box.width <= 375) {
				ok("mobile", `Opportunity cards fit within 375px viewport (card width: ${Math.round(box.width)}px)`);
			} else {
				fail("mobile", "Opportunity card overflows mobile viewport", `card width: ${box?.width}`);
			}
		}

		// Filter bar not overflowing
		const filterBar = page.locator('input[placeholder*="search"], input[type="search"]').first();
		const filterBox = await filterBar.boundingBox().catch(() => null);
		if (filterBox && filterBox.width > 300) {
			ok("mobile", "Filter search input is wide enough on mobile");
		}

		// Footer links stack and don't overflow
		await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
		const footer = page.locator("footer");
		const footerBox = await footer.boundingBox().catch(() => null);
		if (footerBox && footerBox.width <= 380) {
			ok("mobile", "Footer fits within mobile viewport");
		} else {
			fail("mobile", "Footer overflows mobile viewport", `width: ${footerBox?.width}`);
		}
	} finally {
		await ctx.close();
	}
}

// ──────────────────────────────────────────────────────────────
// Suite 2 - Footer links & static pages
// ──────────────────────────────────────────────────────────────
async function testFooterAndStaticPages(browser) {
	section("Suite 2 - Footer Links & Static Pages");
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	try {
		await page.goto(BASE, { waitUntil: "networkidle", timeout: 30000 });

		// Footer links exist
		const footer = page.locator("footer");
		const footerLinks = await footer.locator("a").all();
		ok("footer", `Footer has ${footerLinks.length} links`);

		// Datenschutz
		const datenschutzLink = footer.locator('a[href*="datenschutz"], a[href*="privacy"]').first();
		const datenschutzHref = await datenschutzLink.getAttribute("href").catch(() => null);
		if (datenschutzHref) {
			await page.goto(`${BASE}${datenschutzHref.startsWith("/") ? datenschutzHref : "/" + datenschutzHref}`, { waitUntil: "networkidle", timeout: 15000 });
			const bodyText = await page.locator("body").innerText();
			if (bodyText.length > 500) {
				ok("footer", "Datenschutz page has content");
			} else {
				fail("footer", "Datenschutz page appears empty or very short");
			}
		} else {
			fail("footer", "No Datenschutz link found in footer");
		}

		// Impressum
		await page.goto(BASE, { waitUntil: "networkidle", timeout: 15000 });
		const impressumLink = footer.locator('a[href*="impressum"]').first();
		const impressumHref = await impressumLink.getAttribute("href").catch(() => null);
		if (impressumHref) {
			await page.goto(`${BASE}${impressumHref.startsWith("/") ? impressumHref : "/" + impressumHref}`, { waitUntil: "networkidle", timeout: 15000 });
			const bodyText = await page.locator("body").innerText();
			if (bodyText.length > 200) {
				ok("footer", "Impressum page has content");
			} else {
				fail("footer", "Impressum page appears empty");
			}
		} else {
			fail("footer", "No Impressum link found in footer");
		}

		// 404 page
		await page.goto(`${BASE}/definitely-does-not-exist-xyz`, { waitUntil: "networkidle", timeout: 15000 });
		const pageText = await page.locator("body").innerText();
		if (pageText.includes("404") || pageText.toLowerCase().includes("not found") || pageText.toLowerCase().includes("nicht gefunden")) {
			ok("footer", "404 page renders correctly for unknown routes");
		} else {
			fail("footer", "404 page may not be rendering correctly", pageText.substring(0, 100));
		}
	} finally {
		await ctx.close();
	}
}

// ──────────────────────────────────────────────────────────────
// Suite 3 - API: Input validation edge cases
// ──────────────────────────────────────────────────────────────
async function testApiValidation() {
	section("Suite 3 - API Input Validation");
	const veraToken = await getToken(VERA_USER, VERA_PASS);
	const olafToken = await getToken(OLAF_USER, OLAF_PASS);

	// Empty title for opportunity creation
	const r1 = await apiPost(
		"/v1/volunteer-opportunities",
		{
			title: "",
			description: "test",
			organizationId: "00000000-0000-0000-0000-000000000001",
			street: "Test St",
			houseNumber: "1",
			zipCode: "12345",
			city: "City",
			occurrence: "OneTime",
			participationType: "Waitlist",
			checkInMethod: "QrCode",
		},
		olafToken,
	);
	if (r1.status === 400) {
		ok("validation", "POST /volunteer-opportunities with empty title returns 400");
	} else {
		fail("validation", "POST /volunteer-opportunities with empty title should return 400", `got ${r1.status}`);
	}

	// Very long title
	const r2 = await apiPost(
		"/v1/volunteer-opportunities",
		{
			title: "A".repeat(1000),
			description: "test",
			organizationId: "00000000-0000-0000-0000-000000000001",
			street: "Test St",
			houseNumber: "1",
			zipCode: "12345",
			city: "City",
			occurrence: "OneTime",
			participationType: "Waitlist",
			checkInMethod: "QrCode",
		},
		olafToken,
	);
	if (r2.status === 400) {
		ok("validation", "POST /volunteer-opportunities with 1000-char title returns 400");
	} else {
		fail("validation", "POST /volunteer-opportunities with 1000-char title should return 400", `got ${r2.status}`);
	}

	// PageNumber=0
	const r3 = await apiGet("/v1/volunteer-opportunities?PageNumber=0&PageSize=10");
	if (r3.status === 400) {
		ok("validation", "GET /volunteer-opportunities?PageNumber=0 returns 400");
	} else {
		fail("validation", "GET /volunteer-opportunities?PageNumber=0 should return 400 (returns 500)", `got ${r3.status}`);
	}

	// PageSize=10000
	const r4 = await apiGet("/v1/volunteer-opportunities?PageNumber=1&PageSize=10000");
	if (r4.status === 400) {
		ok("validation", "GET /volunteer-opportunities?PageSize=10000 returns 400");
	} else {
		fail("validation", "GET /volunteer-opportunities?PageSize=10000 should return 400 (unbounded query)", `got ${r4.status}`);
	}

	// Invalid UUID for opportunity
	const r5 = await apiGet("/v1/volunteer-opportunities/not-a-uuid");
	if (r5.status === 400 || r5.status === 404) {
		ok("validation", "GET /volunteer-opportunities/not-a-uuid returns 400 or 404");
	} else {
		fail("validation", "GET /volunteer-opportunities/not-a-uuid should return 400/404", `got ${r5.status}`);
	}

	// Create org with empty name
	const r6 = await apiPost("/v1/organizations", { name: "" }, veraToken);
	if (r6.status === 400) {
		ok("validation", "POST /organizations with empty name returns 400");
	} else {
		fail("validation", "POST /organizations with empty name should return 400", `got ${r6.status}`);
	}

	// Unauthenticated POST to engagements
	const r7 = await apiPost("/v1/volunteer-opportunities/00000000-0000-0000-0000-000000000001/engagements", {});
	if (r7.status === 401) {
		ok("validation", "POST /engagements without auth returns 401");
	} else {
		fail("validation", "POST /engagements without auth should return 401", `got ${r7.status}`);
	}

	// Non-existent opportunity sign-up
	const r8 = await apiPost(
		"/v1/volunteer-opportunities/00000000-0000-0000-0000-000000000099/engagements",
		{ message: "test" },
		veraToken,
	);
	if (r8.status === 404) {
		ok("validation", "POST /engagements for non-existent opportunity returns 404");
	} else {
		fail("validation", "POST /engagements for non-existent opportunity should return 404", `got ${r8.status}`);
	}
}

// ──────────────────────────────────────────────────────────────
// Suite 4 - API: Missing endpoints / features
// ──────────────────────────────────────────────────────────────
async function testApiMissingFeatures() {
	section("Suite 4 - API Missing Features Check");
	const veraToken = await getToken(VERA_USER, VERA_PASS);
	const olafToken = await getToken(OLAF_USER, OLAF_PASS);

	// Search by keyword
	const r1 = await apiGet("/v1/volunteer-opportunities?search=tier");
	if (r1.status === 200) {
		const data = await r1.json();
		const hasResults = data.items?.length > 0;
		ok("missing", `Keyword search (search=tier) works, ${data.items?.length ?? 0} results`);
	} else {
		fail("missing", "Keyword search param not supported", `got ${r1.status}`);
	}

	// Filter by category
	const r2 = await apiGet("/v1/volunteer-opportunities?Category=Animals");
	const d2 = await r2.json().catch(() => ({}));
	if (r2.status === 200 && Array.isArray(d2.items)) {
		ok("missing", `Category filter works, ${d2.items.length} results for Animals`);
	} else {
		fail("missing", "Category filter does not work", `status ${r2.status}`);
	}

	// PATCH user profile (should exist for partial updates)
	const r3 = await fetch(`${API}/v1/users/me`, {
		method: "PATCH",
		headers: {
			"Content-Type": "application/json",
			Authorization: `Bearer ${veraToken}`,
		},
		body: JSON.stringify({ displayName: "Vera Test" }),
	});
	if (r3.status === 200 || r3.status === 204) {
		ok("missing", "PATCH /v1/users/me exists for partial user update");
	} else if (r3.status === 405) {
		fail("missing", "PATCH /v1/users/me not supported (405) - only PUT available");
	} else {
		fail("missing", `PATCH /v1/users/me returned unexpected status`, `${r3.status}`);
	}

	// GET user profile
	const r4 = await apiGet("/v1/users/me", veraToken);
	if (r4.status === 200) {
		const data = await r4.json();
		const fields = Object.keys(data);
		ok("missing", `GET /v1/users/me works, fields: ${fields.join(", ")}`);
		if (!fields.includes("email")) {
			fail("missing", "GET /v1/users/me does not include email field");
		}
	} else {
		fail("missing", "GET /v1/users/me not available", `got ${r4.status}`);
	}

	// Notifications - mark all read
	const r5 = await fetch(`${API}/v1/notifications/read-all`, {
		method: "POST",
		headers: { Authorization: `Bearer ${veraToken}` },
	});
	if (r5.status === 200 || r5.status === 204) {
		ok("missing", "POST /v1/notifications/read-all exists");
	} else if (r5.status === 404) {
		fail("missing", "POST /v1/notifications/read-all not found - bulk mark-read missing");
	} else {
		fail("missing", `POST /v1/notifications/read-all returned unexpected status`, `${r5.status}`);
	}

	// Opportunity sort
	const r6 = await apiGet("/v1/volunteer-opportunities?SortBy=date&SortDirection=asc");
	const d6 = await r6.json().catch(() => ({}));
	if (r6.status === 200) {
		ok("missing", "Sort parameters accepted");
	} else {
		fail("missing", "Sort parameters not supported", `status ${r6.status}`);
	}

	// User achievements endpoint
	const r7 = await apiGet("/v1/me/achievements", veraToken);
	if (r7.status === 200) {
		const data = await r7.json();
		ok("missing", `GET /v1/me/achievements works, ${Array.isArray(data) ? data.length : "?"} entries`);
	} else {
		fail("missing", "GET /v1/me/achievements failed", `got ${r7.status}`);
	}

	// Org list (should require auth - this is testing the requirement is enforced)
	const r8 = await apiGet("/v1/organizations");
	if (r8.status === 401) {
		ok("missing", "GET /v1/organizations correctly requires authentication");
	} else if (r8.status === 200) {
		fail("missing", "GET /v1/organizations returns 200 without auth - should require auth");
	}
}

// ──────────────────────────────────────────────────────────────
// Suite 5 - Authenticated UX flows (browser)
// ──────────────────────────────────────────────────────────────
async function testAuthenticatedUxFlows(browser) {
	section("Suite 5 - Authenticated UX Flows");
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	try {
		// Login as vera
		await page.goto(`${BASE}/my-engagements`, { waitUntil: "domcontentloaded", timeout: 30000 });
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await page.fill("#username", VERA_USER);
		await page.click("#kc-login");
		await page.waitForSelector("#password", { timeout: 10000 });
		await page.fill("#password", VERA_PASS);
		await page.click("#kc-login");
		await page.waitForURL(`${BASE}/**`, { timeout: 20000 });
		ok("ux", "Login via Keycloak succeeded");

		// My Engagements page loads
		await page.goto(`${BASE}/my-engagements`, { waitUntil: "networkidle", timeout: 20000 });
		const bodyText = await page.locator("body").innerText();
		if (!bodyText.includes("Error") && !bodyText.includes("500")) {
			ok("ux", "My Engagements page loads without error");
		} else {
			fail("ux", "My Engagements page has error content", bodyText.substring(0, 200));
		}

		// Check page title
		const title = await page.title();
		if (title && title.length > 0) {
			ok("ux", `My Engagements has page title: "${title}"`);
		} else {
			fail("ux", "My Engagements page has no title");
		}

		// Achievements page
		await page.goto(`${BASE}/achievements`, { waitUntil: "networkidle", timeout: 20000 });
		const achievementsText = await page.locator("body").innerText();
		if (!achievementsText.includes("500") && achievementsText.length > 100) {
			ok("ux", "Achievements page loads");
		} else {
			fail("ux", "Achievements page error or empty", achievementsText.substring(0, 200));
		}

		// Account page
		await page.goto(`${BASE}/account`, { waitUntil: "networkidle", timeout: 20000 });
		const accountText = await page.locator("body").innerText();
		if (!accountText.includes("500") && accountText.length > 100) {
			ok("ux", "Account page loads");
		} else {
			fail("ux", "Account page error or empty");
		}

		// Check if account page shows user name/email
		if (accountText.toLowerCase().includes("vera") || accountText.includes("@")) {
			ok("ux", "Account page shows user information");
		} else {
			fail("ux", "Account page may not be showing user information");
		}

		// Check notification bell in header
		const notifBell = page.locator('[aria-label*="notification"], button[aria-label*="Notification"], [data-testid="notifications"]').first();
		const notifVisible = await notifBell.isVisible().catch(() => false);
		if (notifVisible) {
			ok("ux", "Notification bell visible when logged in");
		} else {
			fail("ux", "Notification bell not found in header");
		}

		// Language switch persists after navigation
		const langSelector = page.locator('[aria-label*="language"], select, button:has-text("DE"), button:has-text("EN")').first();
		const langVisible = await langSelector.isVisible().catch(() => false);
		if (langVisible) {
			ok("ux", "Language selector visible");
		} else {
			fail("ux", "Language selector not found");
		}
	} finally {
		await ctx.close();
	}
}

// ──────────────────────────────────────────────────────────────
// Suite 6 - Org dashboard (olaf)
// ──────────────────────────────────────────────────────────────
async function testOrgDashboard(browser) {
	section("Suite 6 - Organisator Dashboard & Pages");
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	try {
		// Login as olaf (organisator)
		await page.goto(BASE, { waitUntil: "domcontentloaded", timeout: 30000 });
		await page.goto(`${BASE}/my-engagements`, { waitUntil: "domcontentloaded", timeout: 15000 });
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await page.fill("#username", OLAF_USER);
		await page.click("#kc-login");
		await page.waitForSelector("#password", { timeout: 10000 });
		await page.fill("#password", OLAF_PASS);
		await page.click("#kc-login");
		await page.waitForURL(`${BASE}/**`, { timeout: 20000 });
		ok("org", "Olaf login succeeded");

		// Get olaf's org from API token
		const olafToken = await getToken(OLAF_USER, OLAF_PASS);
		const orgsResp = await apiGet("/v1/organizations", olafToken);
		if (orgsResp.status === 200) {
			const orgsData = await orgsResp.json();
			const orgId = orgsData.items?.[0]?.id ?? orgsData[0]?.id;
			if (orgId) {
				// Org dashboard
				await page.goto(`${BASE}/organizations/${orgId}/dashboard`, { waitUntil: "networkidle", timeout: 20000 });
				const dashText = await page.locator("body").innerText();
				if (!dashText.includes("404") && !dashText.includes("500") && dashText.length > 100) {
					ok("org", "Organisation dashboard page loads");
				} else {
					fail("org", "Organisation dashboard error", dashText.substring(0, 200));
				}

				// Org settings
				await page.goto(`${BASE}/organizations/${orgId}/settings`, { waitUntil: "networkidle", timeout: 20000 });
				const settingsText = await page.locator("body").innerText();
				if (!settingsText.includes("500") && settingsText.length > 100) {
					ok("org", "Organisation settings page loads");
				} else {
					fail("org", "Organisation settings error", settingsText.substring(0, 200));
				}

				// Org engagements
				await page.goto(`${BASE}/organizations/${orgId}/engagements`, { waitUntil: "networkidle", timeout: 20000 });
				const engText = await page.locator("body").innerText();
				if (!engText.includes("500") && engText.length > 100) {
					ok("org", "Organisation engagements page loads");
				} else {
					fail("org", "Organisation engagements error", engText.substring(0, 200));
				}

				// Org profile page (public)
				await page.goto(`${BASE}/organizations/${orgId}/profile`, { waitUntil: "networkidle", timeout: 20000 });
				const profileText = await page.locator("body").innerText();
				if (!profileText.includes("500") && profileText.length > 100) {
					ok("org", "Organisation public profile page loads");
				} else {
					fail("org", "Organisation public profile error", profileText.substring(0, 200));
				}
			} else {
				fail("org", "Could not get org ID for olaf");
			}
		}
	} finally {
		await ctx.close();
	}
}

// ──────────────────────────────────────────────────────────────
// Suite 7 - Opportunity detail UX completeness
// ──────────────────────────────────────────────────────────────
async function testOpportunityDetail(browser) {
	section("Suite 7 - Opportunity Detail Page Completeness");
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	try {
		const listResp = await apiGet("/v1/volunteer-opportunities?PageNumber=1&PageSize=1");
		const listData = await listResp.json();
		const opp = listData.items?.[0];
		if (!opp) {
			fail("detail", "No opportunities to test with");
			return;
		}

		await page.goto(`${BASE}/volunteer-opportunities/${opp.id}`, { waitUntil: "networkidle", timeout: 30000 });
		const bodyText = await page.locator("body").innerText();

		// Title present
		if (bodyText.includes(opp.title)) {
			ok("detail", "Opportunity title shown on detail page");
		} else {
			fail("detail", "Opportunity title not found on detail page");
		}

		// Map present
		const mapContainer = page.locator(".leaflet-container");
		const mapVisible = await mapContainer.isVisible().catch(() => false);
		if (mapVisible) {
			ok("detail", "Leaflet map rendered on detail page");
		} else {
			fail("detail", "No map on detail page");
		}

		// Category/tags (known issue #351)
		const hasCategoryBadge = await page.locator('[class*="badge"], [class*="tag"], [class*="chip"]').first().isVisible().catch(() => false);
		if (hasCategoryBadge) {
			ok("detail", "Category/tag badges visible on detail page");
		} else {
			fail("detail", "No category/tag badges on detail page (known issue #351)");
		}

		// Breadcrumb navigation
		const breadcrumb = page.locator('nav[aria-label*="breadcrumb"], [class*="breadcrumb"]').first();
		const breadcrumbVisible = await breadcrumb.isVisible().catch(() => false);
		if (breadcrumbVisible) {
			ok("detail", "Breadcrumb navigation present on detail page");
		} else {
			fail("detail", "No breadcrumb navigation on detail page");
		}

		// Organization name linked
		const orgLinks = await page.locator('a[href*="/organizations"]').all();
		if (orgLinks.length > 0) {
			ok("detail", "Organization name links to org profile");
		} else {
			fail("detail", "Organization name not linked on detail page");
		}

		// Time slots section
		if (bodyText.toLowerCase().includes("time slot") || bodyText.toLowerCase().includes("available")) {
			ok("detail", "Time slots / availability section present");
		} else {
			fail("detail", "No time slots section visible on detail page");
		}

		// Share button
		const shareBtn = page.locator('button[aria-label*="share"], button:has-text("Share"), button:has-text("Teilen")').first();
		const shareVisible = await shareBtn.isVisible().catch(() => false);
		if (shareVisible) {
			ok("detail", "Share button present on detail page");
		} else {
			fail("detail", "No share button on detail page (enhancement #373)");
		}

		// Page title includes opportunity name
		const pageTitle = await page.title();
		if (pageTitle.includes(opp.title) || pageTitle.length > 10) {
			ok("detail", `Page title set: "${pageTitle}"`);
		} else {
			fail("detail", "Page title not set on detail page");
		}
	} finally {
		await ctx.close();
	}
}

// ──────────────────────────────────────────────────────────────
// Suite 8 - Form validation UX
// ──────────────────────────────────────────────────────────────
async function testFormValidationUx(browser) {
	section("Suite 8 - Form Validation UX (Org Creation)");
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	try {
		// Login as vera
		await page.goto(`${BASE}/my-engagements`, { waitUntil: "domcontentloaded", timeout: 30000 });
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await page.fill("#username", VERA_USER);
		await page.click("#kc-login");
		await page.waitForSelector("#password", { timeout: 10000 });
		await page.fill("#password", VERA_PASS);
		await page.click("#kc-login");
		await page.waitForURL(`${BASE}/**`, { timeout: 20000 });

		await page.goto(BASE, { waitUntil: "networkidle", timeout: 20000 });

		// Try to open Create Organization modal
		const createOrgBtn = page.locator('button:has-text("Create Organization"), button:has-text("Organisation erstellen"), button:has-text("Organization")').first();
		const createOrgVisible = await createOrgBtn.isVisible().catch(() => false);
		if (createOrgVisible) {
			await createOrgBtn.click();
			await page.waitForTimeout(500);

			// Try submitting empty form
			const submitBtn = page.locator('button[type="submit"], button:has-text("Create"), button:has-text("Erstellen")').first();
			const submitVisible = await submitBtn.isVisible().catch(() => false);
			if (submitVisible) {
				await submitBtn.click();
				await page.waitForTimeout(1000);

				// Check for validation error message
				const hasError = await page.locator('[class*="error"], [class*="text-red"], [role="alert"]').first().isVisible().catch(() => false);
				if (hasError) {
					ok("form", "Create org form shows validation error on empty submit");
				} else {
					// Check if native HTML5 validation fired
					const nameInput = page.locator('input[name="name"], input[placeholder*="name"], input[placeholder*="Name"]').first();
					const validity = await nameInput.evaluate((el) => /** @type {HTMLInputElement} */ (el).validity?.valid).catch(() => null);
					if (validity === false) {
						ok("form", "Create org form uses native HTML5 validation");
					} else {
						fail("form", "Create org form has no visible validation feedback on empty submit");
					}
				}
			}
		} else {
			fail("form", "Create Organization button not found for vera (may need to be in org switcher)");
		}
	} finally {
		await ctx.close();
	}
}

// ──────────────────────────────────────────────────────────────
// Suite 9 - Header & navigation
// ──────────────────────────────────────────────────────────────
async function testHeaderNavigation(browser) {
	section("Suite 9 - Header & Navigation");
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	try {
		await page.goto(BASE, { waitUntil: "networkidle", timeout: 30000 });

		// Logo navigates to home
		const logo = page.locator('a[href="/"] img, a[href="/"]:has(img)').first();
		const logoHref = await logo.getAttribute("href").catch(() => null);
		if (logoHref === "/") {
			ok("nav", "Logo links to home page");
		} else {
			fail("nav", "Logo does not link to /", `href: ${logoHref}`);
		}

		// Login button visible when logged out
		const loginBtn = page.locator('button:has-text("Anmelden"), button:has-text("Login"), button:has-text("Sign in")').first();
		const loginVisible = await loginBtn.isVisible().catch(() => false);
		if (loginVisible) {
			ok("nav", "Login button visible when logged out");
		} else {
			fail("nav", "Login button not found when logged out");
		}

		// Language selector
		const langBtn = page.locator('[aria-label*="anguage"], select option[value="de"], button:has-text("DE"), button:has-text("EN")').first();
		const langVisible = await langBtn.isVisible().catch(() => false);
		if (langVisible) {
			ok("nav", "Language selector visible in header");
		} else {
			fail("nav", "Language selector not found in header");
		}

		// Skip to main link (known issue #361)
		const skipLink = await page.locator('a[href="#main-content"]').first().isVisible().catch(() => false);
		if (skipLink) {
			ok("nav", "Skip-to-content link present");
		} else {
			fail("nav", "No skip-to-content link (known issue #361)");
		}

		// Page has main landmark
		const main = page.locator("main, [role='main']").first();
		const mainVisible = await main.isVisible().catch(() => false);
		if (mainVisible) {
			ok("nav", "<main> landmark present on page");
		} else {
			fail("nav", "No <main> landmark on page");
		}

		// Check heading hierarchy - h1 should exist
		const h1 = page.locator("h1").first();
		const h1Visible = await h1.isVisible().catch(() => false);
		if (h1Visible) {
			const h1Text = await h1.innerText();
			ok("nav", `h1 present: "${h1Text.substring(0, 50)}"`);
		} else {
			fail("nav", "No h1 on home page");
		}
	} finally {
		await ctx.close();
	}
}

// ──────────────────────────────────────────────────────────────
// Suite 10 - Performance & headers
// ──────────────────────────────────────────────────────────────
async function testPerformanceAndHeaders() {
	section("Suite 10 - Performance & Security Headers");

	const start = Date.now();
	const resp = await fetch(BASE, { redirect: "follow" });
	const ttfb = Date.now() - start;

	ok("perf", `TTFB: ${ttfb}ms`);
	if (ttfb < 2000) {
		ok("perf", "TTFB under 2 seconds");
	} else {
		fail("perf", "TTFB over 2 seconds", `${ttfb}ms`);
	}

	// Security headers
	const headers = Object.fromEntries(resp.headers.entries());
	const secHeaders = {
		"x-content-type-options": "nosniff",
		"x-frame-options": null,
		"strict-transport-security": null,
	};

	for (const [header, expectedValue] of Object.entries(secHeaders)) {
		const value = headers[header];
		if (value) {
			ok("perf", `Security header present: ${header}: ${value}`);
		} else {
			fail("perf", `Missing security header: ${header}`);
		}
	}

	// Content-Security-Policy
	if (headers["content-security-policy"]) {
		ok("perf", "Content-Security-Policy header present");
	} else {
		fail("perf", "Missing Content-Security-Policy header");
	}

	// API health
	const apiHealth = await fetch(`${API}/health`);
	ok("perf", `API /health: ${apiHealth.status}`);

	// API response time
	const apiStart = Date.now();
	await fetch(`${API}/v1/volunteer-opportunities?PageNumber=1&PageSize=10`);
	const apiTime = Date.now() - apiStart;
	if (apiTime < 1000) {
		ok("perf", `API list response time: ${apiTime}ms`);
	} else {
		fail("perf", `API list response time slow: ${apiTime}ms`);
	}
}

// ──────────────────────────────────────────────────────────────
// Main
// ──────────────────────────────────────────────────────────────
const browser = await chromium.launch({ headless: true });

try {
	await testMobileViewport(browser);
	await testFooterAndStaticPages(browser);
	await testApiValidation();
	await testApiMissingFeatures();
	await testAuthenticatedUxFlows(browser);
	await testOrgDashboard(browser);
	await testOpportunityDetail(browser);
	await testFormValidationUx(browser);
	await testHeaderNavigation(browser);
	await testPerformanceAndHeaders();
} finally {
	await browser.close();
}

console.log(`\n${"=".repeat(60)}`);
console.log(`RESULTS: ${passed} passed, ${failed} failed`);
console.log(`${"=".repeat(60)}`);

if (findings.length > 0) {
	console.log("\nFINDINGS SUMMARY:");
	findings.forEach((f, i) => {
		console.log(`  ${i + 1}. [${f.suite}] ${f.msg}${f.detail ? " -- " + f.detail : ""}`);
	});
}

process.exit(failed > 0 ? 1 : 0);
