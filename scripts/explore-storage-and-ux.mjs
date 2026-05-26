import { chromium } from "playwright";
import fs from "fs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const SS_DIR = "scripts/screenshots/storage-ux";
if (!fs.existsSync(SS_DIR)) fs.mkdirSync(SS_DIR, { recursive: true });

let passed = 0;
let failed = 0;
const findings = [];

function ok(label) {
	console.log(`  PASS  ${label}`);
	passed++;
}

function ko(label, err) {
	console.log(`  FAIL  ${label} - ${err?.message ?? err}`);
	failed++;
}

function note(sev, title, detail) {
	const icon = sev === "bug" ? "[BUG]" : "[ENHANCEMENT]";
	console.log(`  ${icon} ${title}`);
	if (detail) console.log(`          ${detail}`);
	findings.push({ sev, title, detail });
}

async function ss(page, name) {
	await page.screenshot({ path: `${SS_DIR}/${name}.png`, fullPage: true });
}

async function loginAs(page, username, password) {
	await page.goto(`${BASE}/`);
	const btn = page.locator("button", { hasText: /sign in|anmelden/i }).first();
	await btn.click();
	await page.waitForURL(/login\.maik-hasler\.de/);
	await page.fill("#username", username);
	await page.click("#kc-login");
	await page.fill("#password", password);
	await page.click("#kc-login");
	await page.waitForURL(/einsatzbereit\.maik-hasler\.de/);
}

async function run() {
	const browser = await chromium.launch({ headless: true });
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	// ---- Suite 1: User profile - missing avatar upload ----
	console.log("\n=== Suite 1: User profile page ===");
	try {
		await loginAs(page, "vera", "vera123");
		await page.goto(`${BASE}/profile`);
		await page.waitForLoadState("networkidle");
		await ss(page, "01-profile-page");

		const hasAvatarInput =
			(await page.locator('input[type="file"]').count()) > 0;
		const hasAvatarImg =
			(await page.locator("img[alt*='avatar' i], img[alt*='profile' i]").count()) > 0;
		if (!hasAvatarInput && !hasAvatarImg) {
			note(
				"enhancement",
				"No avatar / profile picture upload on profile page",
				"ProfilePage has bio, skills, languages, preferredContact - but no photo upload. " +
					"MinIO would be the natural backend for user avatar storage.",
			);
		} else {
			ok("Profile page has avatar upload/display");
		}

		const profileFields = await page.locator("form label, form input, form textarea, form select").count();
		ok(`Profile page has ${profileFields} form fields`);

		// Check for missing first/last name fields
		const hasFirstName =
			(await page.locator('input[id*="first" i], input[placeholder*="first" i]').count()) > 0 ||
			(await page.getByLabel(/first name|vorname/i).count()) > 0;
		if (!hasFirstName) {
			note(
				"enhancement",
				"Profile page has no First Name / Last Name fields",
				"UpdateUserProfileRequest only stores bio/skills/languages/preferredContact. " +
					"First and last name are stored in Keycloak but only editable via the Account page, not Profile.",
			);
		}
	} catch (e) {
		ko("Suite 1", e);
	}

	// ---- Suite 2: Organization settings - missing logo upload ----
	console.log("\n=== Suite 2: Organization settings ===");
	try {
		// olaf is an organisator
		await page.goto(`${BASE}/`);
		// check if still logged in or need to switch user
		const userMenu = await page.locator('[aria-label*="user" i], [data-testid*="user" i]').count();
		// log out vera and log in as olaf
		const headerText = await page.locator("header").textContent();
		if (headerText?.includes("vera") || headerText?.includes("Vera")) {
			// logout
			const logoutBtn = page.locator("button", { hasText: /sign out|log out|abmelden/i }).first();
			const logoutCount = await logoutBtn.count();
			if (logoutCount > 0) {
				await logoutBtn.click();
				await page.waitForTimeout(1500);
			}
		}
		await ctx.clearCookies();
		await loginAs(page, "olaf", "olaf123");

		// find org settings link
		await page.goto(`${BASE}/`);
		await page.waitForLoadState("networkidle");

		// look for org switcher or nav link to settings
		const settingsLinks = page.locator('a[href*="/settings"]');
		const settingsCount = await settingsLinks.count();

		// Try to find org ID from the API
		const orgsResp = await page.evaluate(async (apiBase) => {
			try {
				const r = await fetch(`${apiBase}/v1/organizations`, {
					credentials: "include",
				});
				return r.status;
			} catch {
				return 0;
			}
		}, API);

		// Navigate to header to find org links
		const headerHtml = await page.locator("header").innerHTML();
		const orgMatch = headerHtml.match(/\/organizations\/([0-9a-f-]{36})/);
		if (orgMatch) {
			const orgId = orgMatch[1];
			await page.goto(`${BASE}/organizations/${orgId}/settings`);
			await page.waitForLoadState("networkidle");
			await ss(page, "02-org-settings");

			const hasLogoInput =
				(await page.locator('input[type="file"]').count()) > 0;
			const hasLogoImg =
				(await page.locator("img[alt*='logo' i], img[alt*='org' i]").count()) > 0;
			if (!hasLogoInput && !hasLogoImg) {
				note(
					"enhancement",
					"No organization logo / cover image upload in settings",
					"OrgSettingsPage supports name/description/contact/address but no logo. " +
						"A logo would be shown on the public org profile page and next to opportunity listings.",
				);
			} else {
				ok("Org settings has logo upload/display");
			}

			// Check for address map preview
			const hasMapPreview =
				(await page.locator(".leaflet-container, [data-testid='map']").count()) > 0;
			if (!hasMapPreview) {
				note(
					"enhancement",
					"No map preview in organization settings for address",
					"When an address is entered, a small map preview would give organisators confidence the geocoding is correct.",
				);
			}
		} else {
			ko("Suite 2: Could not find org ID in header");
		}
	} catch (e) {
		ko("Suite 2", e);
	}

	// ---- Suite 3: Opportunity creation - missing image upload ----
	console.log("\n=== Suite 3: Opportunity creation modal ===");
	try {
		await page.goto(`${BASE}/`);
		await page.waitForLoadState("networkidle");

		// Look for "New opportunity" / "Create" button
		const createBtn = page
			.locator("button", { hasText: /create|new|erstellen|neu/i })
			.first();
		const createCount = await createBtn.count();
		if (createCount > 0) {
			await createBtn.click();
			await page.waitForTimeout(800);
			await ss(page, "03-opportunity-create-modal");

			const hasImageInput =
				(await page.locator('input[type="file"]').count()) > 0;
			if (!hasImageInput) {
				note(
					"enhancement",
					"No image/photo upload in volunteer opportunity creation form",
					"Opportunities only have text fields. A cover image would make listings more engaging. " +
						"MinIO would store the uploaded images.",
				);
			}

			// Check for rich text description
			const hasRichText =
				(await page.locator('[contenteditable="true"], .ql-editor, .tiptap').count()) > 0;
			if (!hasRichText) {
				note(
					"enhancement",
					"Opportunity description is a plain textarea, not a rich-text editor",
					"A markdown or WYSIWYG editor would allow organisators to add formatting, " +
						"lists, and links to their opportunity descriptions.",
				);
			}

			// Check for max participants on non-waitlist types
			const modalText = await page.locator('[role="dialog"], .modal, form').first().textContent();
			if (!modalText?.match(/max.*participant|teilnehmer/i)) {
				note(
					"enhancement",
					"No global max-participants cap for non-Waitlist opportunities",
					"Waitlist opportunities have per-time-slot maxParticipants. " +
						"OpenToAll/InterestBased opportunities have no capacity limit at all.",
				);
			}

			// Close modal
			await page.keyboard.press("Escape");
		} else {
			note(
				"bug",
				"Create opportunity button not visible on homepage for organisator",
				"Olaf is logged in with organisator role but no create button is visible - " +
					"may require active org cookie to be set.",
			);
		}
	} catch (e) {
		ko("Suite 3", e);
	}

	// ---- Suite 4: Opportunity detail - check-in flow ----
	console.log("\n=== Suite 4: Check-in QR / manual flow ===");
	try {
		const apiResp = await page.evaluate(async (apiBase) => {
			const r = await fetch(`${apiBase}/v1/volunteer-opportunities?page=1&pageSize=5`);
			const j = await r.json();
			return j;
		}, API);

		const items = apiResp?.items ?? [];
		let checkInOppId = null;
		for (const item of items) {
			if (item.checkInMethod && item.checkInMethod !== "None") {
				checkInOppId = item.id;
				break;
			}
		}

		if (checkInOppId) {
			await page.goto(`${BASE}/volunteer-opportunities/${checkInOppId}`);
			await page.waitForLoadState("networkidle");
			await ss(page, "04-opportunity-with-checkin");
			ok(`Found opportunity with check-in method: ${checkInOppId}`);
		} else {
			// Check if checkInMethod is even returned in the list response
			const sampleItem = items[0];
			if (sampleItem && !("checkInMethod" in sampleItem)) {
				note(
					"enhancement",
					"checkInMethod not included in opportunity list response",
					"The list endpoint returns opportunities without checkInMethod field. " +
						"This means the frontend can't filter/sort by check-in type.",
				);
			} else {
				note(
					"enhancement",
					"No opportunities with check-in enabled in test data",
					"All test opportunities use checkInMethod='None'. " +
						"Add a test opportunity with QR or Manual check-in to exercise that flow.",
				);
			}
		}

		// Explore the engagement management page for check-in UI
		await page.goto(`${BASE}/`);
		await page.waitForLoadState("networkidle");
		const engagementLinks = page.locator('a[href*="/engagements"]');
		const engCount = await engagementLinks.count();
		if (engCount > 0) {
			const href = await engagementLinks.first().getAttribute("href");
			if (href) {
				await page.goto(`${BASE}${href}`);
				await page.waitForLoadState("networkidle");
				await ss(page, "04b-engagement-management");
				const hasCheckinBtn =
					(await page.locator("button", { hasText: /check.?in/i }).count()) > 0;
				if (hasCheckinBtn) {
					ok("Engagement management page has check-in button");
				} else {
					note(
						"enhancement",
						"Check-in button not visible on engagement management page",
						"The CheckInModal exists in code but may only appear when checkInMethod != None.",
					);
				}
			}
		}
	} catch (e) {
		ko("Suite 4", e);
	}

	// ---- Suite 5: My engagements page ----
	console.log("\n=== Suite 5: My engagements page (as vera) ===");
	try {
		await ctx.clearCookies();
		await loginAs(page, "vera", "vera123");
		await page.goto(`${BASE}/my-engagements`);
		await page.waitForLoadState("networkidle");
		await ss(page, "05-my-engagements");

		const bodyText = await page.locator("main").textContent();
		if (bodyText?.match(/no engagement|keine|leer|empty/i) || bodyText?.includes("loading") === false) {
			ok("My engagements page loaded without error");
		}

		// Check for cancellation UI
		const hasCancelBtn =
			(await page.locator("button", { hasText: /cancel|stornieren|absagen/i }).count()) > 0;
		if (!hasCancelBtn) {
			note(
				"enhancement",
				"No cancel/withdraw button visible on My Engagements page",
				"Users cannot withdraw from an opportunity they signed up for. " +
					"Either this is not implemented or vera has no engagements.",
			);
		}

		// Check for status badges
		const hasBadges =
			(await page.locator(".rounded-full, [class*='badge'], [class*='status']").count()) > 0;
		if (hasBadges) {
			ok("Engagement status badges visible");
		}

		await ss(page, "05b-my-engagements-detail");
	} catch (e) {
		ko("Suite 5", e);
	}

	// ---- Suite 6: Account page ----
	console.log("\n=== Suite 6: Account page ===");
	try {
		await page.goto(`${BASE}/account`);
		await page.waitForLoadState("networkidle");
		await ss(page, "06-account-page");

		const bodyText = await page.locator("main").textContent();
		ok("Account page loaded");

		const hasDeleteAccount =
			bodyText?.match(/delete account|konto löschen|account löschen/i) !== null;
		if (!hasDeleteAccount) {
			note(
				"enhancement",
				"No 'Delete account' option on Account page",
				"GDPR Article 17 (right to erasure) requires users to be able to delete their account. " +
					"The backend has DeleteUserAsync in KeycloakUserService but no frontend delete flow.",
			);
		}

		const hasEmailChange =
			bodyText?.match(/change email|e-mail ändern/i) !== null;
		if (!hasEmailChange) {
			note(
				"enhancement",
				"No 'Change email' option on Account page",
				"Users cannot change their email address from within the app. " +
					"This requires a Keycloak admin API call or a Keycloak account console link.",
			);
		}

		const hasPasswordChange =
			bodyText?.match(/change password|passwort|password/i) !== null;
		if (!hasPasswordChange) {
			note(
				"enhancement",
				"No 'Change password' link on Account page",
				"Users cannot change their password. A link to Keycloak account console would suffice.",
			);
		}
	} catch (e) {
		ko("Suite 6", e);
	}

	// ---- Suite 7: Public org profile (without login) ----
	console.log("\n=== Suite 7: Public org profile (unauthenticated) ===");
	try {
		const freshCtx = await browser.newContext({ ignoreHTTPSErrors: true });
		const freshPage = await freshCtx.newPage();

		// Get first org from API
		const orgsJson = await freshPage.evaluate(async (apiBase) => {
			const r = await fetch(`${apiBase}/v1/organizations`);
			return r.json();
		}, API);

		const orgs = Array.isArray(orgsJson) ? orgsJson : orgsJson?.items ?? [];
		if (orgs.length > 0) {
			const orgId = orgs[0].id;
			await freshPage.goto(`${BASE}/organizations/${orgId}`);
			await freshPage.waitForLoadState("networkidle");
			await freshPage.screenshot({
				path: `${SS_DIR}/07-public-org-profile.png`,
				fullPage: true,
			});

			const mainText = await freshPage.locator("main").textContent();

			// Check for logo/banner
			const hasLogo =
				(await freshPage.locator("img[alt*='logo' i], img[alt*='org' i], [class*='logo' i]").count()) > 0;
			if (!hasLogo) {
				note(
					"enhancement",
					"Organization public profile has no logo or cover image",
					"The profile shows name, description, and contact info but no visual identity. " +
						"A logo image (stored in MinIO) would significantly improve org brand recognition.",
				);
			}

			// Check for member count
			const hasMemberCount = mainText?.match(/\d+ member|\d+ Mitglied/i) !== null;
			if (!hasMemberCount) {
				note(
					"enhancement",
					"Organization profile does not show member count",
					"Showing '12 members' builds trust with potential volunteers.",
				);
			}

			// Check for social links
			const hasSocialLinks =
				(await freshPage.locator('a[href*="twitter"], a[href*="instagram"], a[href*="facebook"], a[href*="linkedin"]').count()) > 0;
			if (!hasSocialLinks) {
				note(
					"enhancement",
					"Organization profile has no social media links",
					"Social links (Twitter, Instagram, LinkedIn) are common on org profiles and " +
						"help volunteers learn more about the organization.",
				);
			}

			ok(`Public org profile loaded for org ${orgId}`);
		} else {
			ko("Suite 7: no organizations found");
		}

		await freshCtx.close();
	} catch (e) {
		ko("Suite 7", e);
	}

	// ---- Suite 8: API endpoints - missing features ----
	console.log("\n=== Suite 8: API surface exploration ===");
	try {
		const openapi = await page.evaluate(async (apiBase) => {
			const r = await fetch(`${apiBase}/openapi/v1.json`);
			return r.json();
		}, API);

		const paths = Object.keys(openapi?.paths ?? {});
		ok(`API has ${paths.length} paths`);

		const hasUserSearch = paths.some((p) => p.match(/\/users\?|\/users\/search/i));
		if (!hasUserSearch) {
			note(
				"enhancement",
				"No user search / directory endpoint in the API",
				"Organisators cannot search for users to add to their organization by name/email. " +
					"AddMember currently requires knowing the exact username.",
			);
		}

		const hasNotifMarkRead = paths.some((p) =>
			p.match(/notifications.*read|read.*notifications/i),
		);
		const notifPaths = paths.filter((p) => p.includes("notification"));
		if (notifPaths.length > 0 && !hasNotifMarkRead) {
			note(
				"enhancement",
				"Notifications cannot be individually marked as read",
				`Notification paths: ${notifPaths.join(", ")}. ` +
					"Only bulk-dismiss may be available; per-notification read tracking is missing.",
			);
		}

		const hasFileUpload = paths.some((p) =>
			p.match(/upload|media|files|images|avatars/i),
		);
		if (!hasFileUpload) {
			note(
				"bug",
				"No file upload endpoints in the API - MinIO integration is entirely absent",
				"There are no /upload, /media, /avatars, or /files endpoints. " +
					"User avatars, organization logos, and opportunity cover images cannot be uploaded. " +
					"MinIO (S3-compatible) should be added to the Aspire AppHost and a file upload " +
					"service implemented to unlock these features.",
			);
		}

		const hasOpportunitySearch = paths.some((p) =>
			p.match(/volunteer-opportunities\/search/i),
		);
		const listPath = paths.find((p) =>
			p.match(/volunteer-opportunities$/) && !p.includes("{"),
		);
		if (listPath) {
			const listOp = openapi.paths[listPath]?.get;
			const params = listOp?.parameters?.map((p) => p.name) ?? [];
			ok(`Opportunity list params: ${params.join(", ")}`);
			if (!params.includes("isRemote")) {
				note(
					"enhancement",
					"GET /volunteer-opportunities has no isRemote filter parameter",
					"Backend does not support filtering by remote/in-person. " +
						"Parameter needs to be added to the query handler and endpoint.",
				);
			}
			if (!params.includes("category")) {
				note(
					"enhancement",
					"GET /volunteer-opportunities has no category filter parameter",
					"Even though VolunteerOpportunity has a Category field, " +
						"the list endpoint does not support filtering by it.",
				);
			}
		}
	} catch (e) {
		ko("Suite 8", e);
	}

	// ---- Suite 9: Mobile responsiveness ----
	console.log("\n=== Suite 9: Mobile responsiveness ===");
	try {
		const mobileCtx = await browser.newContext({
			ignoreHTTPSErrors: true,
			viewport: { width: 375, height: 812 },
			userAgent:
				"Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1",
		});
		const mobilePage = await mobileCtx.newPage();
		await mobilePage.goto(`${BASE}/`);
		await mobilePage.waitForLoadState("networkidle");
		await mobilePage.screenshot({
			path: `${SS_DIR}/09-mobile-home.png`,
			fullPage: false,
		});

		// Check for hamburger menu or mobile nav
		const hasHamburger =
			(await mobilePage.locator('[aria-label*="menu" i], [aria-label*="navigation" i], button[class*="hamburger" i]').count()) > 0;
		const headerLinksVisible =
			(await mobilePage.locator("header nav a:visible").count()) > 0;
		if (!hasHamburger && !headerLinksVisible) {
			note(
				"enhancement",
				"No hamburger / mobile navigation menu on small screens",
				"On 375px viewport the header navigation links may be hidden with no mobile menu. " +
					"The site may be unusable on mobile without a responsive nav.",
			);
		} else {
			ok("Mobile nav present");
		}

		// Check hero section on mobile
		const heroText = await mobilePage.locator("h1, h2").first().textContent();
		if (heroText) ok(`Hero heading visible on mobile: "${heroText.trim().slice(0, 40)}"`);

		// Check filter bar on mobile
		const filterBar = await mobilePage.locator('input[placeholder*="search" i], input[placeholder*="suchen" i]').count();
		if (filterBar > 0) {
			ok("Search input visible on mobile");
		} else {
			note(
				"enhancement",
				"Search/filter bar may not be visible on mobile",
				"On 375px viewport the filter inputs may be hidden or overflow the screen.",
			);
		}

		await mobileCtx.close();
	} catch (e) {
		ko("Suite 9", e);
	}

	// ---- Suite 10: Accessibility quick checks ----
	console.log("\n=== Suite 10: Accessibility quick checks ===");
	try {
		const a11yCtx = await browser.newContext({ ignoreHTTPSErrors: true });
		const a11yPage = await a11yCtx.newPage();
		await a11yPage.goto(`${BASE}/`);
		await a11yPage.waitForLoadState("networkidle");

		// Check skip link
		const hasSkipLink =
			(await a11yPage.locator('a[href="#main"], a[href="#content"], [class*="skip" i]').count()) > 0;
		if (!hasSkipLink) {
			note(
				"enhancement",
				"No skip-to-main-content link for keyboard / screen reader users",
				"A visually hidden 'Skip to main content' link should be the first focusable element " +
					"so keyboard users can bypass the header navigation.",
			);
		}

		// Check lang attribute
		const htmlLang = await a11yPage.locator("html").getAttribute("lang");
		if (htmlLang) {
			ok(`HTML lang attribute set: "${htmlLang}"`);
		} else {
			note(
				"bug",
				"HTML <html> element is missing the lang attribute",
				"Screen readers need the lang attribute to select the correct voice/language.",
			);
		}

		// Check focus visible styles
		await a11yPage.keyboard.press("Tab");
		const focusedEl = await a11yPage.locator(":focus").count();
		if (focusedEl > 0) {
			ok("Focus ring visible after Tab key press");
		} else {
			note(
				"enhancement",
				"Focus ring may not be visible on first Tab press",
				"Keyboard navigation focus indicators should always be visible.",
			);
		}

		await a11yCtx.close();
	} catch (e) {
		ko("Suite 10", e);
	}

	await browser.close();

	console.log("\n" + "=".repeat(60));
	console.log(`Results: ${passed} passed, ${failed} failed`);
	console.log(`\nFindings (${findings.length} total):`);
	findings.forEach((f, i) => {
		const icon = f.sev === "bug" ? "[BUG]" : "[ENHANCEMENT]";
		console.log(`  ${i + 1}. ${icon} ${f.title}`);
	});

	if (failed > 0) process.exit(1);
}

run().catch((e) => {
	console.error(e);
	process.exit(1);
});
