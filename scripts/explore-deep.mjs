import { chromium } from "playwright";
import fs from "fs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const SCREENSHOTS = "/home/user/einsatzbereit/scripts/screenshots";
fs.mkdirSync(SCREENSHOTS, { recursive: true });

const browser = await chromium.launch({ headless: true });

const findings = [];
function note(sev, title, detail) {
	console.log(`[${sev}] ${title}: ${detail}`);
	findings.push({ sev, title, detail });
}

// ── Anonymous exploration ───────────────────────────────────────
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1280, height: 900 } });
	const page = await ctx.newPage();

	await page.goto(BASE);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/01-homepage.png`, fullPage: true });

	// Check page title
	const title = await page.title();
	console.log("Page title:", title);

	// Check h1 text
	const h1 = await page.locator("h1").first().textContent();
	console.log("H1:", h1);

	// Check meta description
	const metaDesc = await page.locator("meta[name='description']").getAttribute("content").catch(() => null);
	if (!metaDesc) note("info", "Missing meta description", "No <meta name='description'> found on homepage");
	else console.log("Meta description:", metaDesc);

	// Check OG tags
	const ogTitle = await page.locator("meta[property='og:title']").getAttribute("content").catch(() => null);
	if (!ogTitle) note("info", "Missing OG title", "No Open Graph title meta tag found");

	// Check for filter bar elements
	const filterBar = page.locator("input[placeholder], [aria-label*='search'], [placeholder*='search']").first();
	const hasSearch = await filterBar.isVisible({ timeout: 3000 }).catch(() => false);
	console.log("Has search input:", hasSearch);

	// Check opportunity card content
	const cards = await page.locator("main a[href*='/volunteer-opportunities/']").count();
	console.log("Opportunity cards:", cards);
	if (cards === 0) note("bug", "No opportunities shown on homepage", "Homepage shows no volunteer opportunity cards");

	// Check for category/tags filter
	const catFilter = page.locator("[aria-label*='category'], select[name*='category'], button[data-testid*='category']").first();
	const hasCatFilter = await catFilter.isVisible({ timeout: 2000 }).catch(() => false);
	if (!hasCatFilter) note("enhancement", "No category filter on homepage", "Users cannot filter opportunities by category even though category field exists on opportunities");

	// Check for tag filter
	const tagFilter = page.locator("[aria-label*='tag'], [placeholder*='tag']").first();
	const hasTagFilter = await tagFilter.isVisible({ timeout: 2000 }).catch(() => false);
	if (!hasTagFilter) note("enhancement", "No tag filter on homepage", "Users cannot filter opportunities by tag even though tags field exists on opportunities");

	// Check opportunity card for category/tags display
	const firstCard = page.locator("main li, main article, main .card").first();
	const cardHtml = await firstCard.innerHTML().catch(() => "");
	const showsCategory = cardHtml.toLowerCase().includes("categor") || cardHtml.includes("data-category");
	const showsTags = cardHtml.toLowerCase().includes("tag");
	if (!showsCategory) note("enhancement", "Opportunity cards don't show category", "Category info not visible on opportunity list cards");
	if (!showsTags) note("enhancement", "Opportunity cards don't show tags", "Tags not shown on opportunity list cards");

	// Mobile viewport test
	await page.setViewportSize({ width: 375, height: 812 });
	await page.goto(BASE);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/02-homepage-mobile.png`, fullPage: false });

	// Check mobile hamburger or header layout
	const mobileNav = page.locator("button[aria-label*='menu'], button[aria-label*='nav'], .hamburger").first();
	const hasMobileNav = await mobileNav.isVisible({ timeout: 2000 }).catch(() => false);
	if (!hasMobileNav) {
		// Check if the header is still showing all items or collapsed
		const headerButtons = await page.locator("header button").count();
		console.log("Header buttons on mobile:", headerButtons);
		if (headerButtons > 3) note("styling", "Header may overflow on mobile", `${headerButtons} buttons visible in header on 375px viewport - check for overflow`);
	}

	// Back to desktop
	await page.setViewportSize({ width: 1280, height: 900 });

	// Opportunity detail page
	await page.goto(BASE);
	await page.waitForLoadState("networkidle");
	const opLink = page.locator("main a[href*='/volunteer-opportunities/']").first();
	if (await opLink.isVisible({ timeout: 5000 }).catch(() => false)) {
		await page.goto(`${BASE}${await opLink.getAttribute("href")}`);
		await page.waitForLoadState("networkidle");
		await page.screenshot({ path: `${SCREENSHOTS}/03-opportunity-detail.png`, fullPage: true });

		// Check for check-in pin display
		const bodyText = await page.locator("body").textContent();
		const h1text = await page.locator("h1").first().textContent();
		console.log("Opportunity detail h1:", h1text);

		// Check for time slots display
		const hasTimeSlots = bodyText?.toLowerCase().includes("slot") || bodyText?.toLowerCase().includes("time");
		console.log("Shows time slot info:", hasTimeSlots);
		if (!hasTimeSlots) note("enhancement", "Opportunity detail doesn't show time slots", "Time slot information is not clearly visible on the detail page");

		// Check for check-in method display
		const hasCheckInMethod = bodyText?.toLowerCase().includes("check") || bodyText?.toLowerCase().includes("qr") || bodyText?.toLowerCase().includes("pin");
		if (!hasCheckInMethod) note("info", "Check-in method not shown on detail page", "The check-in method (QR/PIN) is not displayed to users on the opportunity detail page");

		// Check for tags/category display on detail
		const hasTags = page.locator("[data-testid*='tag'], .tag, [aria-label*='tag']").first();
		const hasTagsVisible = await hasTags.isVisible({ timeout: 1000 }).catch(() => false);
		if (!hasTagsVisible) note("enhancement", "Tags not shown on opportunity detail page", "Category and tags are stored but not displayed to users on the detail page");
	}

	// Datenschutz/Impressum check
	await page.goto(`${BASE}/datenschutz`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/04-datenschutz.png`, fullPage: true });
	const dText = await page.locator("body").textContent();
	if (dText?.includes("Lorem") || dText?.includes("placeholder")) {
		note("bug", "Datenschutz still has placeholder text", "Privacy policy page still contains placeholder/Lorem ipsum content");
	}

	await page.goto(`${BASE}/impressum`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/05-impressum.png`, fullPage: true });
	const iText = await page.locator("body").textContent();
	if (iText?.includes("Lorem") || iText?.includes("placeholder")) {
		note("bug", "Impressum still has placeholder text", "Imprint page still contains placeholder/Lorem ipsum content");
	}

	// 404 page
	await page.goto(`${BASE}/nonexistent-page-xyz`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/06-404.png`, fullPage: false });

	await ctx.close();
}

// ── Authenticated exploration (vera) ───────────────────────────
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1280, height: 900 } });
	const page = await ctx.newPage();

	// Login
	await page.goto(BASE);
	await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
	await page.waitForURL("**/realms/einsatzbereit/**", { timeout: 20000 });
	await page.locator("#username").fill("vera");
	await page.locator("#kc-login").click();
	await page.locator("#password").waitFor({ timeout: 10000 });
	await page.locator("#password").fill("vera123");
	await page.locator("#kc-login").click();
	await page.waitForURL(`${BASE}/`, { timeout: 30000 });
	await page.screenshot({ path: `${SCREENSHOTS}/07-homepage-loggedin.png`, fullPage: false });

	// Achievements page
	await page.goto(`${BASE}/achievements`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/08-achievements.png`, fullPage: true });
	const achText = await page.locator("body").textContent();
	console.log("Achievements page excerpt:", achText?.slice(0, 200));

	// Check streak display
	const hasStreak = achText?.toLowerCase().includes("streak");
	console.log("Shows streak:", hasStreak);
	if (!hasStreak) note("enhancement", "Streak not displayed on achievements page", "User streak data is stored but streak counter may not be prominently displayed");

	// My engagements (will 500 on rc.14, expected)
	await page.goto(`${BASE}/my-engagements`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/09-my-engagements.png`, fullPage: false });

	// Account page
	await page.goto(`${BASE}/account`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/10-account.png`, fullPage: true });
	const acctText = await page.locator("body").textContent();

	// Check for profile picture / avatar support
	const hasAvatar = page.locator("img[alt*='avatar'], img[alt*='profile'], [data-testid*='avatar']").first();
	const hasAvatarVisible = await hasAvatar.isVisible({ timeout: 1000 }).catch(() => false);
	if (!hasAvatarVisible) note("enhancement", "No profile picture/avatar support", "Account page has no profile picture upload or avatar - users cannot personalize their profile");

	// Check for notification preferences
	const hasNotifPref = acctText?.toLowerCase().includes("notif") || acctText?.toLowerCase().includes("benachrichtig");
	if (!hasNotifPref) note("enhancement", "No notification preferences on account page", "Users cannot manage notification preferences from the account settings page");

	// Check for account deletion option
	const hasDeleteAccount = page.getByRole("button", { name: /delete account|konto löschen/i });
	const hasDelete = await hasDeleteAccount.isVisible({ timeout: 1000 }).catch(() => false);
	if (!hasDelete) note("enhancement", "No account deletion option in account page", "No self-service account deletion option is visible - may be a GDPR concern");

	// Profile page
	await page.goto(`${BASE}/profile`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/11-profile.png`, fullPage: true });
	const profileText = await page.locator("body").textContent();
	console.log("Profile page text:", profileText?.slice(0, 300));

	// Opportunity detail when logged in - check sign-up flow
	await page.goto(BASE);
	await page.waitForLoadState("networkidle");
	const opLink = page.locator("main a[href*='/volunteer-opportunities/']").first();
	if (await opLink.isVisible({ timeout: 5000 }).catch(() => false)) {
		const href = await opLink.getAttribute("href");
		await page.goto(`${BASE}${href}`);
		await page.waitForLoadState("networkidle");
		await page.screenshot({ path: `${SCREENSHOTS}/12-opportunity-detail-loggedin.png`, fullPage: true });

		// Check for sign-up CTA visibility
		const signUpArea = await page.locator("main").textContent();
		console.log("Opportunity detail (logged in) main text:", signUpArea?.slice(0, 300));
	}

	await ctx.close();
}

// ── Organisator exploration (olaf) ─────────────────────────────
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1280, height: 900 } });
	const page = await ctx.newPage();

	await page.goto(BASE);
	await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
	await page.waitForURL("**/realms/einsatzbereit/**", { timeout: 20000 });
	await page.locator("#username").fill("olaf");
	await page.locator("#kc-login").click();
	await page.locator("#password").waitFor({ timeout: 10000 });
	await page.locator("#password").fill("olaf123");
	await page.locator("#kc-login").click();
	await page.waitForURL(`${BASE}/`, { timeout: 30000 });

	// Check create opportunity modal - check for all fields
	const createBtn = page.getByRole("button", { name: /\+|create|new|erstellen|neu/i }).first();
	if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
		await createBtn.click();
		await page.locator('[role="dialog"]').waitFor({ timeout: 5000 });
		await page.screenshot({ path: `${SCREENSHOTS}/13-create-opportunity-modal.png`, fullPage: false });

		const modalText = await page.locator('[role="dialog"]').textContent();
		console.log("Create modal fields visible:", modalText?.slice(0, 500));

		// Check if category field is in the form
		const hasCategoryField = page.locator('[role="dialog"]').getByLabel(/category|kategorie/i);
		const hasCat = await hasCategoryField.isVisible({ timeout: 1000 }).catch(() => false);
		if (!hasCat) note("enhancement", "No category field in create opportunity modal", "The category field is stored in the DB but not exposed in the create opportunity form");

		// Check if tags field is in the form
		const hasTagsField = page.locator('[role="dialog"]').getByLabel(/tags|schlagwort/i);
		const hasTagsF = await hasTagsField.isVisible({ timeout: 1000 }).catch(() => false);
		if (!hasTagsF) note("enhancement", "No tags field in create opportunity modal", "Tags are stored in the DB but cannot be added through the create opportunity form");

		// Check for description field
		const hasDesc = await page.locator('[role="dialog"]').getByLabel(/description|beschreibung/i).isVisible({ timeout: 1000 }).catch(() => false);
		if (!hasDesc) note("styling", "Description field not clearly labeled in create modal", "The description field in create opportunity modal may not be properly labeled");

		await page.keyboard.press("Escape");
	}

	// Organization settings
	const opId = "019e5652-576f-7a50-8df4-9f706b7e50d6";
	const orgId = "1e13fc7d-a899-46c3-9345-43c47d343014";
	await page.goto(`${BASE}/organizations/${orgId}/settings`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/14-org-settings.png`, fullPage: true });
	const settingsText = await page.locator("body").textContent();
	console.log("Org settings text:", settingsText?.slice(0, 400));

	// Dashboard
	await page.goto(`${BASE}/organizations/${orgId}/dashboard`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/15-org-dashboard.png`, fullPage: true });
	const dashText = await page.locator("body").textContent();
	console.log("Dashboard text:", dashText?.slice(0, 400));

	// Check for stats/KPIs on dashboard
	const hasStats = dashText?.toLowerCase().includes("total") ||
									 dashText?.toLowerCase().includes("count") ||
									 dashText?.match(/\d+\s*(engagement|applicant|volunteer)/i);
	if (!hasStats) note("enhancement", "Organization dashboard lacks metrics/statistics", "The org dashboard should show volunteer statistics (total sign-ups, confirmed, pending) but displays minimal data");

	// Opportunity detail with olaf - check edit modal fields
	await page.goto(`${BASE}/volunteer-opportunities/${opId}`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/16-opportunity-detail-organisator.png`, fullPage: true });

	const editBtn = page.getByRole("button", { name: /edit|bearbeiten/i });
	if (await editBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
		await editBtn.click();
		await page.locator("div.fixed.inset-0.z-50").first().waitFor({ timeout: 5000 });
		await page.screenshot({ path: `${SCREENSHOTS}/17-edit-opportunity-modal.png`, fullPage: false });

		const editModal = page.locator("div.fixed.inset-0.z-50").first();
		const editText = await editModal.textContent();
		console.log("Edit modal fields:", editText?.slice(0, 400));

		const hasCategoryInEdit = await editModal.getByLabel(/category|kategorie/i).isVisible({ timeout: 1000 }).catch(() => false);
		if (!hasCategoryInEdit) note("enhancement", "Category field missing from edit opportunity modal", "Category cannot be set when editing an opportunity");

		const hasTagsInEdit = await editModal.getByLabel(/tags|schlagwort/i).isVisible({ timeout: 1000 }).catch(() => false);
		if (!hasTagsInEdit) note("enhancement", "Tags field missing from edit opportunity modal", "Tags cannot be added when editing an opportunity");

		// Check for role=dialog on edit modal - a11y bug
		const hasRoleDialog = await page.locator('[role="dialog"]').isVisible({ timeout: 1000 }).catch(() => false);
		if (!hasRoleDialog) note("bug", "EditVolunteerOpportunityModal missing role=dialog", "The edit opportunity modal uses div.fixed.inset-0 instead of role=dialog, breaking screen reader accessibility and failing axe-core checks");

		const cancelBtn = editModal.getByRole("button", { name: /cancel|abbrechen/i });
		if (await cancelBtn.isVisible({ timeout: 1000 }).catch(() => false)) {
			await cancelBtn.click();
		}
	}

	// Engagement management page
	await page.goto(`${BASE}/volunteer-opportunities/${opId}/engagements`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SCREENSHOTS}/18-engagement-management.png`, fullPage: true });

	await ctx.close();
}

await browser.close();

console.log("\n=== FINDINGS SUMMARY ===");
findings.forEach((f, i) => console.log(`${i+1}. [${f.sev}] ${f.title}`));
console.log(`\nScreenshots saved to ${SCREENSHOTS}/`);
