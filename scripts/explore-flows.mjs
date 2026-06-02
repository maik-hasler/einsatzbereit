import { chromium } from "playwright";
import fs from "fs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const SS = "/home/user/einsatzbereit/scripts/screenshots";
fs.mkdirSync(SS, { recursive: true });

const browser = await chromium.launch({ headless: true });
const findings = [];
let pass = 0, fail = 0;

function note(sev, title, detail) {
	console.log(`  [${sev.toUpperCase()}] ${title}`);
	if (detail) console.log(`          ${detail}`);
	findings.push({ sev, title, detail });
}
async function ok(msg) { console.log(`  PASS  ${msg}`); pass++; }
async function ko(msg, e) { console.log(`  FAIL  ${msg} - ${e?.message ?? e}`); fail++; }
async function check(label, fn) {
	try { await fn(); ok(label); } catch (e) { ko(label, e); }
}

async function login(page, user, pwd) {
	await page.goto(BASE);
	await page.getByRole("button", { name: /sign in|anmelden/i }).first().click();
	await page.waitForURL("**/realms/einsatzbereit/**", { timeout: 20000 });
	await page.locator("#username").fill(user);
	await page.locator("#kc-login").click();
	await page.locator("#password").waitFor({ timeout: 10000 });
	await page.locator("#password").fill(pwd);
	await page.locator("#kc-login").click();
	await page.waitForURL(`${BASE}/`, { timeout: 30000 });
}

// ── 1. Profile page save ─────────────────────────────────────────────────────
console.log("\n=== 1: Profile page save flow ===");
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	const apiCalls = {};
	page.on("response", async r => {
		if (r.url().includes("api.maik-hasler.de/v1/")) {
			try { apiCalls[r.url()] = { status: r.status(), body: (await r.text()).slice(0, 200) }; } catch {}
		}
	});
	await login(page, "vera", "vera123");
	await page.goto(`${BASE}/profile`);
	await page.waitForLoadState("networkidle");

	// Try filling bio
	const bioField = page.getByLabel(/bio/i);
	const hasBio = await bioField.isVisible({ timeout: 3000 }).catch(() => false);
	if (hasBio) {
		await bioField.fill("I love volunteering for animal shelters and community events.");
		const saveBtn = page.getByRole("button", { name: /save|speichern/i });
		await saveBtn.click();
		await page.waitForTimeout(2000);
		await page.screenshot({ path: `${SS}/30-profile-save.png`, fullPage: true });

		// Check for success/error feedback
		const body = await page.locator("body").textContent();
		const hasSuccess = body?.match(/saved|gespeichert|success|erfolgreich/i);
		const hasError = body?.match(/error|fehler/i);
		if (hasSuccess) {
			ok("Profile: save shows success feedback");
		} else if (hasError) {
			ko("Profile: save returned an error", new Error(body?.slice(0, 100)));
		} else {
			note("bug", "Profile save: no visible success/error feedback after clicking Save", "User cannot tell if their profile was saved successfully");
		}

		// Check if bio persists on reload
		await page.reload();
		await page.waitForLoadState("networkidle");
		const reloadedBio = await page.getByLabel(/bio/i).inputValue().catch(() => "");
		if (reloadedBio.includes("animal shelters")) {
			ok("Profile: bio persists after reload");
		} else {
			note("bug", "Profile: bio does not persist after page reload", `Expected saved bio but got: "${reloadedBio?.slice(0, 50)}"`);
		}
	}

	// Check API calls
	const profilePutCalls = Object.entries(apiCalls).filter(([u]) => u.includes("/me/profile") || u.includes("/profile"));
	if (profilePutCalls.length === 0) note("bug", "Profile save: no PUT/PATCH API call observed", "Clicking Save on the profile page did not make an API call");
	else profilePutCalls.forEach(([u, r]) => console.log(`  API: ${r.status} ${u}`));

	await ctx.close();
}

// ── 2. Account page save ─────────────────────────────────────────────────────
console.log("\n=== 2: Account page save flow ===");
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	const apiCalls = {};
	page.on("response", async r => {
		if (r.url().includes("api.maik-hasler.de/v1/")) {
			try { apiCalls[r.url()] = { status: r.status(), body: (await r.text()).slice(0, 200) }; } catch {}
		}
	});
	await login(page, "vera", "vera123");
	await page.goto(`${BASE}/account`);
	await page.waitForLoadState("networkidle");

	// Try changing first name and saving
	const firstNameField = page.getByLabel(/first name|vorname/i);
	const hasFN = await firstNameField.isVisible({ timeout: 3000 }).catch(() => false);
	if (hasFN) {
		const original = await firstNameField.inputValue();
		await firstNameField.fill("Vera");
		await page.getByRole("button", { name: /save|speichern/i }).first().click();
		await page.waitForTimeout(2000);
		await page.screenshot({ path: `${SS}/31-account-save.png`, fullPage: false });
		const body = await page.locator("body").textContent();
		if (body?.match(/saved|gespeichert|success|erfolgreich/i)) {
			ok("Account: save shows success feedback");
		} else if (body?.match(/error|fehler/i)) {
			ko("Account: save returned an error", new Error(body.slice(0, 100)));
		} else {
			note("bug", "Account save: no visible success/error feedback", "User cannot tell if account was saved");
		}
	}
	await ctx.close();
}

// ── 3. Language switching completeness ───────────────────────────────────────
console.log("\n=== 3: German language switching ===");
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	await login(page, "vera", "vera123");
	await page.goto(BASE);
	await page.waitForLoadState("networkidle");

	// Switch to German
	const langBtn = page.locator("button[aria-label='Switch language']");
	await langBtn.click();
	const deOpt = page.getByRole("option", { name: /deutsch/i }).first();
	const deOptAlt = page.locator('[role="listbox"] li, [role="listbox"] button').filter({ hasText: /deutsch/i }).first();
	const useAlt = !(await deOpt.isVisible({ timeout: 2000 }).catch(() => false));
	const btn = useAlt ? deOptAlt : deOpt;
	await btn.click();
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SS}/32-german-homepage.png`, fullPage: false });

	const h1de = await page.locator("h1").first().textContent();
	console.log("  German h1:", h1de);
	await check("Language: German h1 is non-English", async () => {
		if (!h1de || h1de === "Ready to make a difference?") throw new Error(`Still English: "${h1de}"`);
	});

	// Check nav items are translated
	const navText = await page.locator("header").textContent();
	const hasGerman = navText?.match(/anmelden|suchen|deutsch/i);
	if (!hasGerman) note("bug", "Language switch: header nav not fully translated to German", `Header text: ${navText?.slice(0, 100)}`);

	// Navigate to opportunity detail in German
	const opLink = page.locator("main a[href*='/volunteer-opportunities/']").first();
	if (await opLink.isVisible({ timeout: 5000 }).catch(() => false)) {
		await opLink.click();
		await page.waitForLoadState("networkidle");
		await page.screenshot({ path: `${SS}/33-german-detail.png`, fullPage: false });
		const detailText = await page.locator("main").textContent();
		const hasGermanDetail = detailText?.match(/einmalig|termin|teilnehmer|anmelden/i);
		if (!hasGermanDetail) note("bug", "Language switch: opportunity detail page not fully translated", `Detail text: ${detailText?.slice(0, 200)}`);
		else ok("Language: opportunity detail shows German text");
	}
	await ctx.close();
}

// ── 4. Sign-up flow (as vera, for the one opportunity) ───────────────────────
console.log("\n=== 4: Sign-up flow ===");
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	const apiCalls = {};
	page.on("response", async r => {
		if (r.url().includes("api.maik-hasler.de/v1/")) {
			try { apiCalls[r.url()] = { status: r.status(), body: (await r.text()).slice(0, 300) }; } catch {}
		}
	});
	await login(page, "vera", "vera123");

	const opId = "019e5652-576f-7a50-8df4-9f706b7e50d6";
	await page.goto(`${BASE}/volunteer-opportunities/${opId}`);
	await page.waitForLoadState("networkidle");

	// Check what buttons exist
	const mainContent = await page.locator("main").innerHTML();
	const hasJoinBtn = await page.getByRole("button", { name: /join waitlist|warteliste|express interest|interesse/i }).isVisible({ timeout: 3000 }).catch(() => false);
	const hasAlreadySigned = mainContent.includes("success") || mainContent.includes("Warteliste");
	console.log(`  Join button visible: ${hasJoinBtn}, Already signed: ${hasAlreadySigned}`);

	if (hasJoinBtn) {
		await page.getByRole("button", { name: /join waitlist|warteliste|express interest|interesse/i }).click();
		await page.locator('[role="dialog"]').waitFor({ timeout: 5000 });
		await page.screenshot({ path: `${SS}/34-signup-modal.png`, fullPage: false });
		const modalText = await page.locator('[role="dialog"]').textContent();
		console.log("  Sign-up modal content:", modalText?.slice(0, 200));
		await check("Sign-up modal: time slots shown", async () => {
			if (!modalText?.match(/slot|termin|time|zeit/i)) throw new Error("No time slot info in modal");
		});
		await page.keyboard.press("Escape");
	} else {
		note("info", "Sign-up: button not visible (vera may be organisator or already signed up)", "");
	}

	// Check /v1/me/engagements to see vera's state
	await page.goto(`${BASE}/my-engagements`);
	await page.waitForLoadState("networkidle");
	const engBody = await page.locator("main").textContent();
	console.log("  My Engagements page:", engBody?.slice(0, 200));
	await page.screenshot({ path: `${SS}/35-my-engagements.png`, fullPage: false });

	await ctx.close();
}

// ── 5. Organisation settings - General + Members ─────────────────────────────
console.log("\n=== 5: Organisation settings ===");
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	const apiCalls = {};
	page.on("response", async r => {
		if (r.url().includes("api.maik-hasler.de/v1/")) {
			try { apiCalls[r.url()] = { status: r.status(), body: (await r.text()).slice(0, 300) }; } catch {}
		}
	});
	await login(page, "olaf", "olaf123");

	const orgId = "1e13fc7d-a899-46c3-9345-43c47d343014";
	await page.goto(`${BASE}/organizations/${orgId}/settings`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SS}/36-org-settings-general.png`, fullPage: true });

	const settingsText = await page.locator("main").textContent();
	console.log("  Settings page:", settingsText?.slice(0, 400));

	// Check General tab is active
	await check("Org settings: heading visible", async () => {
		await page.locator("h1,h2").first().waitFor({ timeout: 5000 });
	});

	// Check Members tab
	const membersTab = page.getByRole("button", { name: /members|mitglieder/i });
	if (await membersTab.isVisible({ timeout: 3000 }).catch(() => false)) {
		await membersTab.click();
		await page.waitForLoadState("networkidle");
		await page.screenshot({ path: `${SS}/37-org-settings-members.png`, fullPage: true });
		const memberText = await page.locator("main").textContent();
		console.log("  Members tab:", memberText?.slice(0, 300));
		await check("Org settings members: list visible", async () => {
			if (!memberText?.match(/member|mitglied|olaf|vera/i)) throw new Error("No member list visible");
		});
	}

	// Check organisation profile page (public)
	await page.goto(`${BASE}/organizations/${orgId}`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SS}/38-org-profile.png`, fullPage: true });
	const orgProfileText = await page.locator("main").textContent();
	console.log("  Org profile:", orgProfileText?.slice(0, 300));
	await check("Org profile: shows opportunities", async () => {
		if (!orgProfileText?.match(/tierkuschler|opportunity|volunteer|einsatz/i)) throw new Error("No opportunities on org profile");
	});

	// Check if org profile shows contact info
	const hasContact = orgProfileText?.match(/email|phone|website|kontakt/i);
	if (!hasContact) note("enhancement", "Organization profile doesn't show contact information", "Email, phone, and website fields exist in the org model but are not displayed on the public profile page");

	// Check organisation dashboard
	await page.goto(`${BASE}/organizations/${orgId}/dashboard`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SS}/39-org-dashboard.png`, fullPage: true });
	const dashText = await page.locator("main").textContent();
	console.log("  Dashboard:", dashText?.slice(0, 400));

	// Check for any API 4xx/5xx errors
	const errCalls = Object.entries(apiCalls).filter(([, r]) => r.status >= 400);
	errCalls.forEach(([u, r]) => note("bug", `API error on org settings: ${r.status} ${u}`, r.body));

	await ctx.close();
}

// ── 6. Pagination and empty state for list ───────────────────────────────────
console.log("\n=== 6: Homepage filter + pagination ===");
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	await page.goto(BASE);
	await page.waitForLoadState("networkidle");

	// Search for something that doesn't exist
	const searchInput = page.getByPlaceholder(/search|suchen/i).first();
	await searchInput.fill("zzznoresultsxxx");
	await searchInput.press("Enter");
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SS}/40-search-noresults.png`, fullPage: false });
	const noResultText = await page.locator("main").textContent();
	const hasEmptyState = noResultText?.match(/no results|keine ergebnisse|nothing found|nicht gefunden|0 result/i);
	if (!hasEmptyState) note("enhancement", "No visible empty state when search returns no results", `Main text: ${noResultText?.slice(0, 200)}`);
	else ok("Search: empty state shown for no results");

	// Category filter
	await searchInput.fill("");
	await searchInput.press("Enter");
	await page.waitForLoadState("networkidle");
	const catSelect = page.locator("select").filter({ hasText: /categor|kategor/i }).first();
	const catSelectAlt = page.locator("select[name*='category'], select[aria-label*='category']").first();
	const hasCat = await catSelect.isVisible({ timeout: 2000 }).catch(() => false);
	if (hasCat) {
		const options = await catSelect.locator("option").allTextContents();
		console.log("  Category options:", options);
		if (options.length < 3) note("enhancement", "Category filter has very few options", `Only: ${options.join(", ")}`);
	}

	// Remote filter
	const remoteFilter = page.locator("input[type='checkbox'][name*='remote'], [aria-label*='remote']").first();
	const hasRemote = await remoteFilter.isVisible({ timeout: 2000 }).catch(() => false);
	if (!hasRemote) note("enhancement", "No remote/in-person toggle filter on homepage", "Users cannot filter by remote vs in-person opportunities");
	else ok("Homepage: remote filter available");

	await ctx.close();
}

// ── 7. User achievements page (public share URL) ─────────────────────────────
console.log("\n=== 7: Public achievements share page ===");
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	// First get vera's user ID by checking the share modal
	await login(page, "vera", "vera123");
	await page.goto(`${BASE}/achievements`);
	await page.waitForLoadState("networkidle");

	const shareBtn = page.getByRole("button", { name: /share achievements|errungenschaften teilen/i });
	if (await shareBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
		await shareBtn.click();
		await page.locator('[role="dialog"]').waitFor({ timeout: 5000 });
		const modalText = await page.locator('[role="dialog"]').textContent();
		// Extract the share URL
		const urlMatch = modalText?.match(/https?:\/\/[^\s"]+achievements[^\s"]+/);
		if (urlMatch) {
			const shareUrl = urlMatch[0];
			console.log("  Share URL:", shareUrl);
			// Navigate as anonymous user
			await page.keyboard.press("Escape");

			const anonCtx = await browser.newContext({ ignoreHTTPSErrors: true });
			const anonPage = await anonCtx.newPage();
			await anonPage.goto(shareUrl);
			await anonPage.waitForLoadState("networkidle");
			await anonPage.screenshot({ path: `${SS}/41-public-achievements.png`, fullPage: true });
			const publicText = await anonPage.locator("main").textContent();
			console.log("  Public achievements page:", publicText?.slice(0, 300));
			await check("Public achievements: accessible without login", async () => {
				if (!publicText?.match(/achievement|badge|first step|errungenschaft/i)) throw new Error(`No achievements visible: ${publicText?.slice(0, 100)}`);
			});
			await anonCtx.close();
		} else {
			note("bug", "Share modal doesn't show a shareable URL", `Modal text: ${modalText?.slice(0, 200)}`);
		}
	}
	await ctx.close();
}

// ── 8. Error page / boundary ─────────────────────────────────────────────────
console.log("\n=== 8: Error handling ===");
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();

	// Navigate to a non-existent opportunity
	await page.goto(`${BASE}/volunteer-opportunities/00000000-0000-0000-0000-000000000000`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SS}/42-nonexistent-opportunity.png`, fullPage: false });
	const bodyText = await page.locator("body").textContent();
	console.log("  Non-existent opportunity:", bodyText?.slice(0, 200));
	const hasNotFound = bodyText?.match(/not found|nicht gefunden|404|doesn't exist/i);
	if (!hasNotFound) note("bug", "Non-existent opportunity shows no proper not-found UI", `Body: ${bodyText?.slice(0, 150)}`);
	else ok("Non-existent opportunity: shows not-found UI");

	// Non-existent org
	await page.goto(`${BASE}/organizations/00000000-0000-0000-0000-000000000000`);
	await page.waitForLoadState("networkidle");
	await page.screenshot({ path: `${SS}/43-nonexistent-org.png`, fullPage: false });
	const orgText = await page.locator("body").textContent();
	const hasOrgNF = orgText?.match(/not found|nicht gefunden|404/i);
	if (!hasOrgNF) note("bug", "Non-existent organization shows no proper not-found UI", `Body: ${orgText?.slice(0, 150)}`);
	else ok("Non-existent organization: shows not-found UI");

	await ctx.close();
}

// ── 9. Notification bell - mark-as-read / interactions ───────────────────────
console.log("\n=== 9: Notifications panel ===");
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	await login(page, "vera", "vera123");
	await page.goto(BASE);
	await page.waitForLoadState("networkidle");

	const bellBtn = page.locator("button[aria-label='Notifications']");
	await bellBtn.click();
	await page.waitForTimeout(500);
	await page.screenshot({ path: `${SS}/44-notifications.png`, fullPage: false });

	const panelText = await page.locator("body").textContent();
	const notifSection = panelText?.slice(panelText.indexOf("Notifications"), panelText.indexOf("Notifications") + 300);
	console.log("  Notifications panel:", notifSection?.slice(0, 200));

	// Check for proper ARIA role on the notification dropdown
	const hasProperRole = await page.locator('[role="dialog"],[role="listbox"],[role="menu"]').isVisible({ timeout: 1000 }).catch(() => false);
	if (!hasProperRole) note("enhancement", "Notifications dropdown missing ARIA role", "The notifications panel opens but uses no ARIA role (not role=dialog/listbox/menu), which may confuse screen readers");
	else ok("Notifications: panel has ARIA role");

	// Check bell icon unread count (badge)
	const badge = page.locator("button[aria-label='Notifications'] span, button[aria-label='Notifications'] .badge");
	const hasBadge = await badge.isVisible({ timeout: 1000 }).catch(() => false);
	console.log("  Notification badge visible:", hasBadge);

	// Close by clicking elsewhere
	await page.locator("h1").first().click();
	await page.waitForTimeout(300);
	const stillOpen = await page.locator("text=No notifications").isVisible({ timeout: 1000 }).catch(() => false);
	if (stillOpen) note("bug", "Notifications panel doesn't close when clicking outside", "Clicking outside the notification dropdown does not close it");
	else ok("Notifications: panel closes on outside click");

	await ctx.close();
}

// ── 10. Create Opportunity full form (olaf) ───────────────────────────────────
console.log("\n=== 10: Create opportunity form fields ===");
{
	const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	await login(page, "olaf", "olaf123");
	await page.goto(BASE);
	await page.waitForLoadState("networkidle");

	const createBtn = page.getByRole("button", { name: /\+ create opportunity|erstellen/i }).first();
	const createBtnAlt = page.locator("button").filter({ hasText: /^\+ Create opportunity$/ }).first();
	const hasCreate = await createBtn.isVisible({ timeout: 3000 }).catch(() => false)
		|| await createBtnAlt.isVisible({ timeout: 1000 }).catch(() => false);

	if (hasCreate) {
		const btn = (await createBtn.isVisible().catch(() => false)) ? createBtn : createBtnAlt;
		await btn.click();
		await page.locator('[role="dialog"]').waitFor({ timeout: 5000 });
		await page.screenshot({ path: `${SS}/45-create-modal-full.png`, fullPage: false });

		const allLabels = await page.locator('[role="dialog"] label').allTextContents();
		console.log("  Create modal labels:", allLabels);

		const hasCategory = allLabels.some(l => /category|kategorie/i.test(l));
		const hasTags = allLabels.some(l => /tags?/i.test(l));
		const hasCheckInMethod = allLabels.some(l => /check.?in method|check-in/i.test(l));
		const hasTimeSlots = await page.locator('[role="dialog"]').getByText(/time slot|slot|termin/i).first().isVisible({ timeout: 2000 }).catch(() => false);
		const hasMaxParticipants = allLabels.some(l => /max.*participant|teilnehmer/i.test(l));

		if (!hasCategory) note("enhancement", "Create opportunity form missing Category field", "Category is stored in DB but not in the create form");
		else ok("Create modal: Category field present");
		if (!hasTags) note("enhancement", "Create opportunity form missing Tags field", "Tags are stored in DB but not in the create form");
		else ok("Create modal: Tags field present");
		if (!hasCheckInMethod) note("enhancement", "Create opportunity form missing Check-in method field", "CheckInMethod is in DB but not exposed in the create form");
		else ok("Create modal: Check-in method field present");
		if (!hasTimeSlots) note("enhancement", "Create opportunity form has no time slot section", "Users cannot add time slots during opportunity creation");
		else ok("Create modal: Time slots section present");
		if (!hasMaxParticipants) note("enhancement", "Create opportunity form missing max participants field", "maxParticipants is part of TimeSlot but not shown in create form");
		else ok("Create modal: Max participants field present");

		await page.keyboard.press("Escape");
	} else {
		note("info", "Create opportunity button not found with expected label", "");
	}
	await ctx.close();
}

await browser.close();

console.log(`\n${"=".repeat(60)}`);
console.log(`Flow tests: ${pass} passed, ${fail} failed`);
console.log(`\nFindings (${findings.length}):`);
findings.forEach((f, i) => console.log(`  ${i + 1}. [${f.sev}] ${f.title}`));
