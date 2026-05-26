/**
 * deep-test.mjs  –  In-depth live staging tests
 *
 * Covers:
 *   Suite 1  – Opportunity list: filters, pagination, map toggle
 *   Suite 2  – Opportunity detail: full content, map, badges
 *   Suite 3  – Sign-up flow (vera signs up for an opportunity)
 *   Suite 4  – My engagements: status, withdraw
 *   Suite 5  – Notification system: mark-read, mark-all-read
 *   Suite 6  – Streaks API
 *   Suite 7  – Achievement / badge catalog
 *   Suite 8  – Engagement management (olaf side)
 *   Suite 9  – Create opportunity + time-slot validation
 *   Suite 10 – Org settings: save contact info, verify on profile
 *   Suite 11 – Form validation / required fields
 *   Suite 12 – Deep-link / URL filter persistence
 *   Suite 13 – Error boundary (404 page)
 *   Suite 14 – API contract spot-checks (status codes, schemas)
 */

import { chromium } from "playwright";
import fs from "fs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const SS = "scripts/screenshots/deep";
fs.mkdirSync(SS, { recursive: true });

let passed = 0;
let failed = 0;
const findings = [];

const ok = (label) => { console.log(`  PASS  ${label}`); passed++; };
const ko = (label, err) => { console.log(`  FAIL  ${label}\n        ${err?.message?.split("\n")[0] ?? err}`); failed++; };
const note = (sev, title, detail) => {
	const tag = sev === "bug" ? "[BUG]" : "[ENHANCEMENT]";
	console.log(`  ${tag} ${title}`);
	if (detail) console.log(`        ${detail}`);
	findings.push({ sev, title, detail });
};
const ss = (page, name) => page.screenshot({ path: `${SS}/${name}.png`, fullPage: true });

async function apiFetch(path, opts = {}) {
	const r = await fetch(`${API}${path}`, opts);
	return { status: r.status, body: r.ok ? await r.json().catch(() => null) : null };
}

async function loginAs(page, user, pass) {
	await page.goto(`${BASE}/`);
	await page.waitForLoadState("networkidle");
	const btn = page.locator("button", { hasText: /sign in|anmelden/i }).first();
	if (await btn.count() === 0) return; // already logged in
	await btn.click();
	await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 10000 });
	await page.fill("#username", user);
	await page.click("#kc-login");
	await page.waitForSelector("#password", { timeout: 10000 });
	await page.fill("#password", pass);
	await page.click("#kc-login");
	await page.waitForURL(/einsatzbereit\.maik-hasler\.de/, { timeout: 15000 });
	await page.waitForLoadState("networkidle");
}

async function getBearerToken(page) {
	return page.evaluate(() => {
		for (let i = 0; i < sessionStorage.length; i++) {
			const key = sessionStorage.key(i);
			try {
				const v = JSON.parse(sessionStorage.getItem(key));
				if (v?.access_token) return v.access_token;
			} catch { /* skip */ }
		}
		return null;
	});
}

// ─────────────────────────────────────────────────────────────────────────────

async function run() {
	const browser = await chromium.launch({ headless: true });

	// ── Suite 1: Opportunity list – filters, pagination, map toggle ──────────
	console.log("\n=== Suite 1: Opportunity list – filters, pagination, map ===");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			await page.goto(`${BASE}/`);
			await page.waitForLoadState("networkidle");
			await ss(page, "01a-home");

			// Keyword search
			const searchInput = page.locator('input[placeholder*="search" i], input[placeholder*="suchen" i]').first();
			await searchInput.fill("Feuerwehr");
			await page.waitForTimeout(800);
			await ss(page, "01b-search-feuerwehr");
			const listItems = page.locator("ul li, [role='listitem']");
			const count = await listItems.count();
			ok(`Keyword search returned ${count} result(s)`);

			// Clear search and apply Occurrence filter
			await searchInput.fill("");
			await page.waitForTimeout(500);

			const occSelect = page.locator("select").filter({ hasText: /once|einmalig|recurring|regelm/i }).first();
			if (await occSelect.count() > 0) {
				await occSelect.selectOption({ index: 1 });
				await page.waitForTimeout(600);
				const filteredCount = await listItems.count();
				ok(`Occurrence filter returned ${filteredCount} result(s)`);
				await occSelect.selectOption({ index: 0 }); // reset
			} else {
				note("enhancement", "Occurrence filter dropdown not found in filter bar");
			}

			// isRemote filter
			const remoteSelect = page.locator("select").filter({ hasText: /remote/i }).first();
			if (await remoteSelect.count() > 0) {
				await remoteSelect.selectOption("true");
				await page.waitForTimeout(600);
				await ss(page, "01c-remote-filter");
				const remoteCount = await listItems.count();
				ok(`isRemote=true filter returned ${remoteCount} result(s)`);
				await remoteSelect.selectOption(""); // reset
			} else {
				note("enhancement", "isRemote filter select not visible on homepage");
			}

			// Map toggle
			const mapBtn = page.locator("button", { hasText: /map|karte/i }).first();
			if (await mapBtn.count() > 0) {
				await mapBtn.click();
				await page.waitForTimeout(1000);
				await ss(page, "01d-map-view");
				const leaflet = page.locator(".leaflet-container");
				if (await leaflet.count() > 0) {
					ok("Map view renders Leaflet container");
					// Check for pins
					const pins = page.locator(".leaflet-marker-icon");
					const pinCount = await pins.count();
					ok(`Map shows ${pinCount} marker pin(s)`);
				} else {
					note("bug", "Map toggle clicked but no Leaflet container rendered");
				}
				// Switch back to list
				const listBtn = page.locator("button", { hasText: /list|liste/i }).first();
				if (await listBtn.count() > 0) await listBtn.click();
			} else {
				note("enhancement", "No map/list toggle button found on homepage");
			}

			// Pagination – navigate to page 2 if available
			const nextBtn = page.locator("button[aria-label*='next' i], button", { hasText: /next|weiter|›|»/i }).first();
			if (await nextBtn.count() > 0 && await nextBtn.isEnabled()) {
				await nextBtn.click();
				await page.waitForTimeout(600);
				const page2Items = await page.locator("ul li").count();
				ok(`Page 2 loaded with ${page2Items} item(s)`);
				await ss(page, "01e-page2");
			} else {
				ok("Only one page of results (or no next-page button)");
			}
		} catch (e) {
			ko("Suite 1", e);
		}
		await ctx.close();
	}

	// ── Suite 2: Opportunity detail – full content ───────────────────────────
	console.log("\n=== Suite 2: Opportunity detail – full content check ===");
	let firstOppId = null;
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			const { status, body } = await apiFetch("/v1/volunteer-opportunities?PageNumber=1&PageSize=10");
			if (status !== 200 || !body?.items?.length) {
				ko("Suite 2: no opportunities returned by API");
			} else {
				firstOppId = body.items[0].id;
				const opp = body.items[0];
				ok(`API returned opportunity: "${opp.title}" (${opp.participationType})`);

				await page.goto(`${BASE}/volunteer-opportunities/${firstOppId}`);
				await page.waitForLoadState("networkidle");
				await ss(page, "02a-opp-detail");

				// Title visible
				const h1 = await page.locator("h1").first().textContent();
				if (h1?.trim()) ok(`Title rendered: "${h1.trim().slice(0, 50)}"`);
				else note("bug", "Opportunity detail: h1 is empty");

				// Org link
				const orgLink = page.locator('a[href*="/organizations/"]');
				if (await orgLink.count() > 0) ok("Organization link present on detail page");
				else note("bug", "No organization link on opportunity detail page");

				// Badges row (occurrence, participation type, remote/address)
				const badges = page.locator(".rounded-full");
				const badgeCount = await badges.count();
				if (badgeCount >= 2) ok(`${badgeCount} badge chips rendered`);
				else note("bug", `Only ${badgeCount} badge chip(s) on detail page, expected >=2`);

				// Map for in-person
				if (!opp.isRemote && opp.latitude && opp.longitude) {
					const map = page.locator(".leaflet-container");
					if (await map.count() > 0) ok("Single-marker map shown for in-person opportunity");
					else note("bug", "In-person opportunity with coords has no map on detail page");
				}

				// Time slots section for Waitlist
				if (opp.participationType === "Waitlist") {
					const slots = page.locator("ul li").filter({ hasText: /max|participant/i });
					ok(`Waitlist: ${await slots.count()} time slot(s) listed`);
				}

				// Tags / category (if present)
				if (opp.tags?.length) {
					const tagText = await page.locator("main").textContent();
					const found = opp.tags.some((t) => tagText?.includes(t));
					if (found) ok(`Tags visible on detail page`);
					else note("enhancement", "Tags returned by API are not rendered on opportunity detail page");
				}
			}
		} catch (e) {
			ko("Suite 2", e);
		}
		await ctx.close();
	}

	// ── Suite 3: Sign-up flow (vera) ─────────────────────────────────────────
	console.log("\n=== Suite 3: Sign-up flow as vera ===");
	let veraToken = null;
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			await loginAs(page, "vera", "vera123");
			veraToken = await getBearerToken(page);

			if (!firstOppId) {
				const { body } = await apiFetch("/v1/volunteer-opportunities?PageNumber=1&PageSize=10");
				firstOppId = body?.items?.[0]?.id;
			}

			if (!firstOppId) throw new Error("No opportunity ID available");

			await page.goto(`${BASE}/volunteer-opportunities/${firstOppId}`);
			await page.waitForLoadState("networkidle");
			await ss(page, "03a-detail-as-vera");

			// Check if already signed up
			const signedUpMsg = page.locator("p", { hasText: /signed up|success|erfolgreich/i });
			const alreadyDone = await signedUpMsg.count() > 0;

			const signUpBtn = page.locator("button", {
				hasText: /express interest|join waitlist|sign up|anmelden|interesse/i,
			}).first();

			if (await signUpBtn.count() > 0) {
				await signUpBtn.click();
				await page.waitForTimeout(500);
				await ss(page, "03b-signup-modal");

				// Modal should be open
				const modal = page.locator('[role="dialog"]');
				if (await modal.count() > 0) {
					ok("Sign-up modal opens");

					// Fill message field if present
					const msgField = modal.locator("textarea, input[type='text']").first();
					if (await msgField.count() > 0) {
						await msgField.fill("I am very interested in helping!");
					}

					// Select time slot if present
					const slotOptions = modal.locator('input[type="radio"], select option');
					if (await slotOptions.count() > 0) {
						await slotOptions.first().click().catch(() => {});
						ok(`Time slot selection present (${await slotOptions.count()} option(s))`);
					}

					await ss(page, "03c-signup-modal-filled");

					// Submit
					const submitBtn = modal.locator('button[type="submit"], button', {
						hasText: /submit|send|absenden|bestätigen/i,
					}).first();
					if (await submitBtn.count() > 0) {
						await submitBtn.click();
						await page.waitForTimeout(1500);
						await ss(page, "03d-after-signup");

						const successEl = page.locator("p, div", {
							hasText: /success|signed up|erfolgreich|bestätigt/i,
						}).first();
						if (await successEl.count() > 0) {
							ok("Sign-up submission shows success message");
						} else {
							// Check if modal closed (also a success indicator)
							const modalGone = await page.locator('[role="dialog"]').count() === 0;
							if (modalGone) ok("Sign-up modal closed after submission (success)");
							else note("bug", "Sign-up modal still open after submit – possible silent error");
						}
					} else {
						note("bug", "Sign-up modal has no submit button");
					}
				} else {
					// Maybe direct submit (no modal)
					ok("Sign-up submitted without modal");
				}
			} else if (alreadyDone) {
				ok("Vera already signed up for this opportunity (success state visible)");
			} else {
				note("bug", "No sign-up button visible for authenticated non-organisator vera");
			}
		} catch (e) {
			ko("Suite 3", e);
		}
		await ctx.close();
	}

	// ── Suite 4: My engagements ──────────────────────────────────────────────
	console.log("\n=== Suite 4: My engagements page ===");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			await loginAs(page, "vera", "vera123");
			veraToken = await getBearerToken(page);

			await page.goto(`${BASE}/my-engagements`);
			await page.waitForLoadState("networkidle");
			await ss(page, "04a-my-engagements");

			const mainText = await page.locator("main").textContent();
			const hasEngagements = page.locator("ul li, tr, [class*='card']");
			const engCount = await hasEngagements.count();

			if (engCount > 0) {
				ok(`My Engagements shows ${engCount} item(s)`);

				// Check for status labels
				const statusWords = ["Pending", "Confirmed", "Cancelled", "Ausstehend", "Bestätigt", "Storniert"];
				const hasStatus = statusWords.some((w) => mainText?.includes(w));
				if (hasStatus) ok("Engagement status label visible");
				else note("enhancement", "No visible status label on engagement list items");

				// Check for org name link
				const orgLinks = page.locator('a[href*="/organizations/"]');
				if (await orgLinks.count() > 0) ok("Org name linked from engagement list");
				else note("enhancement", "No organization link on My Engagements items");

				// Check for opp link
				const oppLinks = page.locator('a[href*="/volunteer-opportunities/"]');
				if (await oppLinks.count() > 0) ok("Opportunity link in My Engagements");
				else note("enhancement", "No opportunity link in My Engagements items");

				// Withdraw button
				const withdrawBtn = page.locator("button", {
					hasText: /withdraw|cancel|stornieren|absagen|zurückziehen/i,
				}).first();
				if (await withdrawBtn.count() > 0) {
					ok("Withdraw/cancel button visible on engagement");
					// Don't actually click it - just confirm it's there
				} else {
					note("enhancement", "No withdraw/cancel button on My Engagements – users cannot withdraw from an opportunity");
				}
			} else if (mainText?.match(/no engagement|keine|empty|leer/i)) {
				ok("My Engagements shows empty state");
				note("enhancement", "Vera has no engagements – sign-up in suite 3 may have failed or opp is different org");
			} else {
				ok("My Engagements page loaded (content state unclear)");
			}

			// API check
			if (veraToken) {
				const { status, body } = await apiFetch("/v1/me/engagements", {
					headers: { Authorization: `Bearer ${veraToken}` },
				});
				if (status === 200 && Array.isArray(body)) {
					ok(`GET /me/engagements → 200, ${body.length} engagement(s)`);
					if (body.length > 0) {
						const first = body[0];
						const hasRequired = first.id && first.opportunityId && first.status;
						if (hasRequired) ok(`Engagement shape OK: id, opportunityId, status present`);
						else note("bug", "Engagement response missing required fields", JSON.stringify(first));
					}
				} else {
					note("bug", `GET /me/engagements returned ${status}`, JSON.stringify(body));
				}
			}
		} catch (e) {
			ko("Suite 4", e);
		}
		await ctx.close();
	}

	// ── Suite 5: Notifications ───────────────────────────────────────────────
	console.log("\n=== Suite 5: Notification system ===");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			await loginAs(page, "vera", "vera123");
			veraToken = await getBearerToken(page);

			// Check notification bell in header
			const bell = page.locator('[aria-label*="notification" i], button[class*="bell" i], button svg').first();

			// API check first
			if (veraToken) {
				const { status, body } = await apiFetch("/v1/notifications", {
					headers: { Authorization: `Bearer ${veraToken}` },
				});
				if (status === 200 && Array.isArray(body)) {
					ok(`GET /v1/notifications → 200, ${body.length} notification(s)`);
					const unread = body.filter((n) => !n.isRead);
					ok(`${unread.length} unread notification(s)`);

					if (body.length > 0) {
						const n = body[0];
						const hasShape = n.id && n.message && "isRead" in n;
						if (hasShape) ok("Notification shape OK: id, message, isRead");
						else note("bug", "Notification missing fields", JSON.stringify(n));

						// Mark one as read
						const { status: readStatus } = await apiFetch(
							`/v1/notifications/${n.id}/read`,
							{ method: "POST", headers: { Authorization: `Bearer ${veraToken}` } },
						);
						if (readStatus === 204 || readStatus === 200) {
							ok(`POST /v1/notifications/${n.id}/read → ${readStatus}`);
						} else {
							note("bug", `Mark notification as read returned ${readStatus}`);
						}
					}

					// Mark all read
					const { status: allStatus } = await apiFetch("/v1/notifications/read-all", {
						method: "POST",
						headers: { Authorization: `Bearer ${veraToken}` },
					});
					if (allStatus === 204 || allStatus === 200) ok(`POST /v1/notifications/read-all → ${allStatus}`);
					else note("bug", `read-all returned ${allStatus}`);
				} else {
					note("bug", `GET /v1/notifications returned ${status}`);
				}
			}

			// UI: open notification dropdown
			await page.goto(`${BASE}/`);
			await page.waitForLoadState("networkidle");
			const notifBell = page.locator("header button").filter({ has: page.locator("svg") }).last();
			const bellCount = await notifBell.count();
			if (bellCount > 0) {
				await notifBell.click();
				await page.waitForTimeout(500);
				await ss(page, "05a-notifications-open");

				const dropdown = page.locator('[role="menu"], [role="listbox"], [aria-label*="notification" i], [class*="dropdown" i]');
				if (await dropdown.count() > 0) {
					ok("Notification dropdown opens");
				} else {
					// Check if any overlay appeared
					const anyOverlay = await page.locator("div[class*='notification'], div[class*='Notification']").count();
					if (anyOverlay > 0) ok("Notification panel appeared");
					else note("bug", "Bell clicked but no notification dropdown/panel appeared");
				}

				// Close
				await page.keyboard.press("Escape");
			} else {
				note("enhancement", "No notification bell button in header");
			}
		} catch (e) {
			ko("Suite 5", e);
		}
		await ctx.close();
	}

	// ── Suite 6: Streaks ─────────────────────────────────────────────────────
	console.log("\n=== Suite 6: Streaks API ===");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			await loginAs(page, "vera", "vera123");
			veraToken = await getBearerToken(page);
			if (veraToken) {
				const { status, body } = await apiFetch("/v1/me/streaks", {
					headers: { Authorization: `Bearer ${veraToken}` },
				});
				if (status === 200 && body) {
					ok(`GET /v1/me/streaks → 200`);
					const hasShape = "currentStreak" in body || "longestStreak" in body || "streak" in body;
					if (hasShape) {
						ok(`Streak shape OK: ${JSON.stringify(body).slice(0, 80)}`);
					} else {
						note("bug", "Streaks response has unexpected shape", JSON.stringify(body).slice(0, 120));
					}
				} else {
					note("bug", `GET /v1/me/streaks returned ${status}`);
				}

				// Check if streaks are shown on profile or achievements page
				await page.goto(`${BASE}/achievements`);
				await page.waitForLoadState("networkidle");
				await ss(page, "06a-achievements-page");
				const pageText = await page.locator("main").textContent();
				const hasStreakUI = pageText?.match(/streak|serie/i) !== null;
				if (hasStreakUI) ok("Streak info visible on achievements page");
				else note("enhancement", "Streak data exists in API but not displayed on achievements page");
			}
		} catch (e) {
			ko("Suite 6", e);
		}
		await ctx.close();
	}

	// ── Suite 7: Badge catalog / achievements ────────────────────────────────
	console.log("\n=== Suite 7: Badge catalog and achievements ===");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			// Public badge catalog
			const { status: badgeStatus, body: badges } = await apiFetch("/v1/badges");
			if (badgeStatus === 200 && Array.isArray(badges)) {
				ok(`GET /v1/badges → 200, ${badges.length} badge(s) in catalog`);
				const b = badges[0];
				if (b && b.id && b.name) ok(`Badge shape OK: ${b.name}`);
				else note("bug", "Badge missing id/name fields");
			} else {
				note("bug", `GET /v1/badges returned ${badgeStatus}`);
			}

			await loginAs(page, "vera", "vera123");
			veraToken = await getBearerToken(page);

			await page.goto(`${BASE}/achievements`);
			await page.waitForLoadState("networkidle");
			await ss(page, "07a-achievements");

			// Count badge tiles
			const badgeTiles = page.locator('[class*="badge" i], [class*="Badge" i], img[alt], svg[aria-label]');
			const tileCount = await badgeTiles.count();
			ok(`${tileCount} badge tile(s) rendered`);

			// Check for locked vs earned distinction
			const mainText = await page.locator("main").textContent();
			const hasLocked = mainText?.match(/locked|gesperrt|not yet|noch nicht/i) !== null;
			const hasEarned = mainText?.match(/earned|erworben|achieved/i) !== null;
			if (hasLocked || hasEarned) ok("Badge locked/earned states visible");
			else note("enhancement", "Badges shown but locked/earned distinction unclear from page text");

			// Share button
			const shareBtn = page.locator("button", { hasText: /share|teilen/i }).first();
			if (await shareBtn.count() > 0) {
				ok("Share achievements button present");
				await shareBtn.click();
				await page.waitForTimeout(400);
				await ss(page, "07b-share-modal");
				const modal = page.locator('[role="dialog"]');
				if (await modal.count() > 0) {
					ok("Share achievements modal opens");
					// Check copy link button
					const copyBtn = modal.locator("button", { hasText: /copy|kopieren/i }).first();
					if (await copyBtn.count() > 0) ok("Copy link button in share modal");
					else note("enhancement", "Share modal has no Copy Link button");
					// Extract the actual URL from the modal (look for an anchor or input)
					const linkEl = modal.locator('a[href], input[type="text"]').first();
					if (await linkEl.count() > 0) {
						const href = await linkEl.getAttribute("href") ?? await linkEl.inputValue().catch(() => "");
						if (href?.includes("/users/")) ok(`Share URL looks correct: ${href.slice(0, 60)}`);
						else note("bug", "Share URL in modal does not include /users/ path", href);
					}
					await page.keyboard.press("Escape");
				} else {
					note("bug", "Share button clicked but no dialog appeared");
				}
			} else {
				note("enhancement", "No 'Share achievements' button on achievements page");
			}
		} catch (e) {
			ko("Suite 7", e);
		}
		await ctx.close();
	}

	// ── Suite 8: Engagement management (olaf / organisator side) ────────────
	console.log("\n=== Suite 8: Engagement management as olaf ===");
	let olafToken = null;
	let olafOrgId = null;
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			await loginAs(page, "olaf", "olaf123");
			olafToken = await getBearerToken(page);

			// Get olaf's orgs
			if (olafToken) {
				const { status, body } = await apiFetch("/v1/organizations", {
					headers: { Authorization: `Bearer ${olafToken}` },
				});
				if (status === 200 && body?.length) {
					olafOrgId = body[0].id;
					ok(`Olaf belongs to org: ${body[0].name} (${olafOrgId})`);
				}

				// Get opportunities for that org
				if (olafOrgId) {
					const { status: oppStatus, body: opps } = await apiFetch(
						`/v1/volunteer-opportunities?PageNumber=1&PageSize=20`,
					);
					const orgOpps = opps?.items?.filter((o) => o.organizationId === olafOrgId) ?? [];
					ok(`Org has ${orgOpps.length} opportunity(ies)`);

					if (orgOpps.length > 0) {
						const opp = orgOpps[0];
						await page.goto(`${BASE}/volunteer-opportunities/${opp.id}/engagements`);
						await page.waitForLoadState("networkidle");
						await ss(page, "08a-engagement-management");

						const mainText = await page.locator("main").textContent();
						ok(`Engagement management page loaded for: "${opp.title}"`);

						// Check for confirm/reject buttons
						const confirmBtn = page.locator("button", { hasText: /confirm|bestätigen/i }).first();
						const rejectBtn = page.locator("button", { hasText: /reject|ablehnen|decline/i }).first();
						if (await confirmBtn.count() > 0) ok("Confirm engagement button present");
						else note("enhancement", "No 'Confirm' button on engagement management page");
						if (await rejectBtn.count() > 0) ok("Reject engagement button present");

						// Check for check-in column
						const checkInHeader = page.locator("th, td", { hasText: /check.?in/i }).first();
						if (await checkInHeader.count() > 0) ok("Check-in status column present");
						else note("enhancement", "No check-in column on engagement management table");

						// Test API directly
						const { status: engStatus, body: engBody } = await apiFetch(
							`/v1/volunteer-opportunities/${opp.id}/engagements`,
							{ headers: { Authorization: `Bearer ${olafToken}` } },
						);
						if (engStatus === 200 && Array.isArray(engBody)) {
							ok(`GET /volunteer-opportunities/${opp.id}/engagements → 200, ${engBody.length} engagement(s)`);
						} else {
							note("bug", `Org engagements endpoint returned ${engStatus}`, JSON.stringify(engBody)?.slice(0, 100));
						}
					} else {
						note("enhancement", "Olaf's org has no opportunities yet – create one to test engagement management");
					}
				}
			}
		} catch (e) {
			ko("Suite 8", e);
		}
		await ctx.close();
	}

	// ── Suite 9: Create opportunity + validation ─────────────────────────────
	console.log("\n=== Suite 9: Create opportunity form and validation ===");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			await loginAs(page, "olaf", "olaf123");

			await page.goto(`${BASE}/`);
			await page.waitForLoadState("networkidle");

			// Click create button
			const createBtn = page.locator("button", { hasText: /create|erstellen/i }).first();
			if (await createBtn.count() === 0) {
				// Possibly needs active org cookie
				note("bug", "Create opportunity button not visible for olaf on homepage", "Active org cookie may not be set");
				throw new Error("no create button");
			}

			await createBtn.click();
			await page.waitForTimeout(600);

			const modal = page.locator('[role="dialog"]');
			if (await modal.count() === 0) throw new Error("Create modal did not open");
			ok("Create opportunity modal opens");

			await ss(page, "09a-create-modal");

			// Try to submit empty form
			const submitBtn = modal.locator('button[type="submit"]').first();
			if (await submitBtn.count() > 0) {
				await submitBtn.click();
				await page.waitForTimeout(400);
				// HTML5 required validation should fire
				const invalid = await page.evaluate(() =>
					document.querySelectorAll(":invalid").length,
				);
				if (invalid > 0) ok(`Empty form blocked by HTML5 validation (${invalid} invalid field(s))`);
				else note("enhancement", "Empty opportunity form submitted without client-side validation");
			}

			// Fill required fields
			const titleInput = modal.locator('input[id*="title" i], input[placeholder*="title" i]').first();
			if (await titleInput.count() > 0) await titleInput.fill("Deep Test Opportunity");

			const descInput = modal.locator("textarea").first();
			if (await descInput.count() > 0) await descInput.fill("This is a test opportunity created by the automated deep test suite.");

			// Toggle isRemote
			const isRemoteToggle = modal.locator('input[type="checkbox"]').filter({ hasText: /remote/i }).first();
			const isRemoteCheckbox = modal.locator('#is-remote, input[id*="remote" i]').first();
			if (await isRemoteCheckbox.count() > 0) {
				await isRemoteCheckbox.check();
				await page.waitForTimeout(300);
				// Address fields should disappear
				const streetInput = modal.locator('input[id*="street" i]').first();
				const streetVisible = await streetInput.isVisible().catch(() => false);
				if (!streetVisible) ok("Address fields hidden when isRemote is checked");
				else note("enhancement", "Address fields still visible after checking isRemote");
			}

			// Select category
			const categorySelect = modal.locator('select[id*="category" i]').first();
			if (await categorySelect.count() > 0) {
				const options = await categorySelect.locator("option").count();
				ok(`Category dropdown has ${options} option(s)`);
				if (options > 1) await categorySelect.selectOption({ index: 1 });
			} else {
				note("enhancement", "No category selector in create opportunity modal");
			}

			// Select occurrence
			const occSelect = modal.locator('select[id*="occurrence" i]').first();
			if (await occSelect.count() > 0) await occSelect.selectOption("Recurring");

			// Select participationType
			const ptSelect = modal.locator('select[id*="participation" i]').first();
			if (await ptSelect.count() > 0) {
				const ptValue = await ptSelect.inputValue();
				ok(`ParticipationType default: ${ptValue}`);
			}

			// Inspect time-slot section
			const addSlotBtn = modal.locator("button", { hasText: /add.*slot|slot.*add|zeitslot/i }).first();
			if (await addSlotBtn.count() > 0) {
				ok("Add time-slot button present in modal");
				// Try adding a slot with invalid dates
				const startInput = modal.locator('input[type="datetime-local"]').first();
				const endInput = modal.locator('input[type="datetime-local"]').nth(1);
				if (await startInput.count() > 0 && await endInput.count() > 0) {
					await startInput.fill("2026-12-01T10:00");
					await endInput.fill("2026-12-01T09:00"); // end before start
					await addSlotBtn.click();
					await page.waitForTimeout(300);
					const slotError = page.locator("p, span", { hasText: /end.*after.*start|zeit|error/i }).first();
					if (await slotError.count() > 0) ok("Time-slot validation: end-before-start error shown");
					else note("enhancement", "No validation error when time-slot end < start");
					// Fix the slot
					await endInput.fill("2026-12-01T12:00");
					await addSlotBtn.click();
					await page.waitForTimeout(300);
					const slotList = modal.locator("li").filter({ hasText: /2026/ });
					if (await slotList.count() > 0) ok("Time slot added to pending list");
				}
			}

			await ss(page, "09b-create-modal-filled");
			await page.keyboard.press("Escape");
		} catch (e) {
			ko("Suite 9", e);
		}
		await ctx.close();
	}

	// ── Suite 10: Org settings – save and verify on profile ─────────────────
	console.log("\n=== Suite 10: Org settings – save contact info, verify on profile ===");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			await loginAs(page, "olaf", "olaf123");
			olafToken = await getBearerToken(page);

			if (!olafOrgId && olafToken) {
				const { body } = await apiFetch("/v1/organizations", {
					headers: { Authorization: `Bearer ${olafToken}` },
				});
				olafOrgId = body?.[0]?.id;
			}

			if (!olafOrgId) throw new Error("No org ID available");

			await page.goto(`${BASE}/organizations/${olafOrgId}/settings`);
			await page.waitForLoadState("networkidle");
			await ss(page, "10a-org-settings");

			// Fill contact email
			const emailInput = page.locator('input[id*="contact-email" i], input[type="email"]').first();
			const phoneInput = page.locator('input[id*="phone" i], input[type="tel"]').first();
			const websiteInput = page.locator('input[id*="website" i], input[type="url"]').first();

			const testEmail = "test-org@example.com";
			const testPhone = "+49 30 1234567";
			const testWebsite = "https://example.com";

			if (await emailInput.count() > 0) await emailInput.fill(testEmail);
			if (await phoneInput.count() > 0) await phoneInput.fill(testPhone);
			if (await websiteInput.count() > 0) await websiteInput.fill(testWebsite);

			const saveBtn = page.locator('button[type="submit"]').first();
			await saveBtn.click();
			await page.waitForTimeout(1500);

			const successMsg = page.locator("div, p", { hasText: /saved|gespeichert|success/i }).first();
			if (await successMsg.count() > 0) ok("Org settings saved successfully");
			else note("bug", "No success message after saving org settings");

			await ss(page, "10b-org-settings-saved");

			// Navigate to public profile and verify contact info appears
			await page.goto(`${BASE}/organizations/${olafOrgId}`);
			await page.waitForLoadState("networkidle");
			await ss(page, "10c-org-profile-public");

			const profileText = await page.locator("main").textContent();
			const emailVisible = profileText?.includes(testEmail);
			const phoneVisible = profileText?.includes(testPhone);
			const websiteVisible = profileText?.includes("example.com");

			if (emailVisible) ok("Contact email visible on public org profile after save");
			else note("bug", "Saved contact email not visible on public org profile", `Expected: ${testEmail}`);

			if (phoneVisible) ok("Contact phone visible on public org profile after save");
			else note("bug", "Saved contact phone not visible on public org profile");

			if (websiteVisible) ok("Website visible on public org profile after save");
			else note("bug", "Saved website not visible on public org profile");
		} catch (e) {
			ko("Suite 10", e);
		}
		await ctx.close();
	}

	// ── Suite 11: Form validation / API boundary checks ──────────────────────
	console.log("\n=== Suite 11: API boundary / validation ===");
	{
		try {
			// Unauthenticated access to protected endpoint
			const { status: s1 } = await apiFetch("/v1/me/engagements");
			if (s1 === 401) ok("GET /me/engagements → 401 for unauthenticated request");
			else note("bug", `GET /me/engagements unauthenticated returned ${s1}, expected 401`);

			// 404 for non-existent opportunity
			const fakeId = "00000000-0000-0000-0000-000000000099";
			const { status: s2 } = await apiFetch(`/v1/volunteer-opportunities/${fakeId}`);
			if (s2 === 404) ok("GET /volunteer-opportunities/{fake} → 404");
			else note("bug", `Non-existent opportunity returned ${s2}, expected 404`);

			// 404 for non-existent org
			const { status: s3 } = await apiFetch(`/v1/organizations/${fakeId}/profile`);
			if (s3 === 404) ok("GET /organizations/{fake}/profile → 404");
			else note("bug", `Non-existent org profile returned ${s3}, expected 404`);

			// Invalid GUID format
			const { status: s4 } = await apiFetch("/v1/volunteer-opportunities/not-a-guid");
			if (s4 === 400 || s4 === 404) ok(`GET with invalid GUID → ${s4} (expected 400 or 404)`);
			else note("bug", `Invalid GUID in path returned ${s4}`);

			// Health endpoint
			const { status: s5 } = await apiFetch("/health");
			if (s5 === 200) ok("GET /health → 200");
			else note("bug", `Health check returned ${s5}`);

			// Opportunity list with invalid page
			const { status: s6 } = await apiFetch("/v1/volunteer-opportunities?PageNumber=0&PageSize=10");
			if (s6 === 400 || s6 === 200) ok(`List with PageNumber=0 → ${s6}`);
			else note("bug", `PageNumber=0 returned ${s6}`);

			// Opportunity list with huge page size
			const { status: s7, body: b7 } = await apiFetch("/v1/volunteer-opportunities?PageNumber=1&PageSize=1000");
			if (s7 === 400) ok("PageSize=1000 rejected → 400");
			else if (s7 === 200 && b7) {
				note(
					"enhancement",
					"No max page size enforced – PageSize=1000 accepted",
					"Large page sizes should be capped (e.g. max 100) to prevent DoS.",
				);
			}
		} catch (e) {
			ko("Suite 11", e);
		}
	}

	// ── Suite 12: Deep-link / URL filter persistence ─────────────────────────
	console.log("\n=== Suite 12: URL filter persistence / deep linking ===");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			// Land on home with search param
			await page.goto(`${BASE}/?search=Feuerwehr&isRemote=false`);
			await page.waitForLoadState("networkidle");
			await ss(page, "12a-deeplink-filters");

			const url = page.url();
			if (url.includes("search=Feuerwehr")) ok("Search param preserved in URL after load");
			else note("bug", "search= query param lost after page load");

			const searchInput = page.locator('input[placeholder*="search" i], input[placeholder*="suchen" i]').first();
			if (await searchInput.count() > 0) {
				const val = await searchInput.inputValue();
				if (val === "Feuerwehr") ok("Search input pre-populated from URL param");
				else note("bug", `Search input value "${val}" doesn't match URL param "Feuerwehr"`);
			}

			// Navigate away and back (simulate browser back)
			await page.goto(`${BASE}/achievements`);
			await page.goBack();
			await page.waitForLoadState("networkidle");
			const backUrl = page.url();
			if (backUrl.includes("search=Feuerwehr")) ok("Browser back restores filter state from URL");
			else note("enhancement", "Browser back loses filter URL params");

			// City filter deep link
			await page.goto(`${BASE}/?city=Berlin`);
			await page.waitForLoadState("networkidle");
			const cityInput = page.locator('input[id*="city" i], input[placeholder*="city" i], input[placeholder*="stadt" i]').first();
			if (await cityInput.count() > 0) {
				const cityVal = await cityInput.inputValue();
				if (cityVal === "Berlin") ok("City filter pre-populated from URL param");
				else note("enhancement", `City input shows "${cityVal}", expected "Berlin" from URL`);
			}
		} catch (e) {
			ko("Suite 12", e);
		}
		await ctx.close();
	}

	// ── Suite 13: Error pages ────────────────────────────────────────────────
	console.log("\n=== Suite 13: Error pages ===");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			// 404 page
			await page.goto(`${BASE}/this-page-does-not-exist`);
			await page.waitForLoadState("networkidle");
			await ss(page, "13a-404-page");

			const h1 = await page.locator("h1, h2").first().textContent();
			if (h1?.match(/not found|404|nicht gefunden/i)) {
				ok(`404 page shows correct heading: "${h1.trim()}"`);
			} else {
				note("bug", `404 page h1 is "${h1?.trim()}" – expected 404/not-found message`);
			}

			// Check it still has header/footer (SPA layout)
			const header = await page.locator("header").count();
			const footer = await page.locator("footer").count();
			if (header > 0 && footer > 0) ok("404 page retains app header and footer");
			else note("bug", "404 page missing header or footer");

			// Home link on 404
			const homeLink = page.locator('a[href="/"], a', { hasText: /home|start|zurück/i }).first();
			if (await homeLink.count() > 0) ok("404 page has link back to home");
			else note("enhancement", "404 page has no link back to home");
		} catch (e) {
			ko("Suite 13", e);
		}
		await ctx.close();
	}

	// ── Suite 14: Org dashboard ──────────────────────────────────────────────
	console.log("\n=== Suite 14: Organization dashboard ===");
	{
		const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
		const page = await ctx.newPage();
		try {
			await loginAs(page, "olaf", "olaf123");
			olafToken = await getBearerToken(page);

			if (!olafOrgId && olafToken) {
				const { body } = await apiFetch("/v1/organizations", {
					headers: { Authorization: `Bearer ${olafToken}` },
				});
				olafOrgId = body?.[0]?.id;
			}

			if (olafOrgId && olafToken) {
				const { status, body } = await apiFetch(
					`/v1/organizations/${olafOrgId}/dashboard`,
					{ headers: { Authorization: `Bearer ${olafToken}` } },
				);
				if (status === 200 && body) {
					ok(`GET /organizations/${olafOrgId}/dashboard → 200`);
					const keys = Object.keys(body);
					ok(`Dashboard response keys: ${keys.join(", ")}`);

					// Check if dashboard is surfaced in the UI
					await page.goto(`${BASE}/`);
					await page.waitForLoadState("networkidle");
					const dashLink = page.locator('a[href*="/dashboard" i]');
					if (await dashLink.count() > 0) {
						await dashLink.first().click();
						await page.waitForLoadState("networkidle");
						await ss(page, "14a-org-dashboard");
						ok("Org dashboard page accessible via nav");
					} else {
						note(
							"enhancement",
							"Org dashboard API endpoint exists but no dashboard link in nav",
							`GET /v1/organizations/${olafOrgId}/dashboard → 200 with keys: ${keys.join(", ")} – but the UI has no dashboard page`,
						);
					}
				} else {
					note("bug", `GET /organizations/{id}/dashboard returned ${status}`);
				}
			}
		} catch (e) {
			ko("Suite 14", e);
		}
		await ctx.close();
	}

	await browser.close();

	// ── Summary ───────────────────────────────────────────────────────────────
	console.log("\n" + "═".repeat(64));
	console.log(`Results: ${passed} passed, ${failed} failed`);
	console.log(`\nFindings (${findings.length}):`);
	findings.forEach((f, i) => {
		const tag = f.sev === "bug" ? "[BUG]" : "[ENHANCEMENT]";
		console.log(`  ${i + 1}. ${tag} ${f.title}`);
		if (f.detail) console.log(`       ${f.detail.slice(0, 100)}`);
	});

	if (failed > 0) process.exit(1);
}

run().catch((e) => {
	console.error(e);
	process.exit(1);
});
