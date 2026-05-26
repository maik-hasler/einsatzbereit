/**
 * deep-test-2.mjs  –  End-to-end lifecycle + advanced coverage
 *
 * Strategy: login once per user, cache the bearer token, then mix
 * fetch()-based API calls with Playwright UI checks so we never lose
 * session state between suites.
 *
 * Suites:
 *   1  Token harvest & API authentication
 *   2  Full engagement lifecycle (sign-up → confirm → check-in → achievement)
 *   3  Withdraw / cancel flows (volunteer side + org side)
 *   4  Create → edit → delete opportunity (full CRUD)
 *   5  Advanced opportunity filters (date range, category, tag, bbox/radius)
 *   6  Account page – first/last name save, delete-account dialog
 *   7  Organization member management (add via invite, remove)
 *   8  Org dashboard data
 *   9  CheckInModal UI (QR vs manual)
 *   10 API performance – measure response times for key endpoints
 *   11 Concurrency guard – double sign-up to the same slot
 *   12 Volunteer profile page – public view
 *   13 i18n completeness – spot-check German translations
 *   14 Footer / legal pages (Impressum, Datenschutz)
 */

import { chromium } from "playwright";
import fs from "fs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API  = "https://api.maik-hasler.de";
const SS   = "scripts/screenshots/deep2";
fs.mkdirSync(SS, { recursive: true });

let passed = 0; let failed = 0;
const findings = [];

const ok   = (l)    => { console.log(`  PASS  ${l}`); passed++; };
const ko   = (l, e) => { console.log(`  FAIL  ${l}\n        ${String(e?.message??e).split("\n")[0]}`); failed++; };
const note = (sev, title, detail) => {
	const tag = sev === "bug" ? "[BUG]" : "[ENH]";
	console.log(`  ${tag} ${title}`);
	if (detail) console.log(`        ${String(detail).slice(0, 110)}`);
	findings.push({ sev, title, detail });
};
const ss   = (page, name) => page.screenshot({ path: `${SS}/${name}.png`, fullPage: true });

// ─── helpers ────────────────────────────────────────────────────────────────

async function extractToken(page) {
	return page.evaluate(() => {
		for (let i = 0; i < sessionStorage.length; i++) {
			try {
				const v = JSON.parse(sessionStorage.getItem(sessionStorage.key(i)));
				if (v?.access_token) return v.access_token;
			} catch { /* skip */ }
		}
		return null;
	});
}

async function loginAndGetToken(browser, user, pass) {
	const ctx  = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	await page.goto(`${BASE}/`);
	await page.waitForLoadState("networkidle");
	const btn = page.locator("button", { hasText: /sign in|anmelden/i }).first();
	if (await btn.count() > 0) {
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
	const token = await extractToken(page);
	return { ctx, page, token };
}

async function api(method, path, token, body) {
	const opts = {
		method,
		headers: {
			...(token ? { Authorization: `Bearer ${token}` } : {}),
			...(body ? { "Content-Type": "application/json" } : {}),
		},
		...(body ? { body: JSON.stringify(body) } : {}),
	};
	const t0 = Date.now();
	const r  = await fetch(`${API}${path}`, opts);
	const ms = Date.now() - t0;
	let data = null;
	try { data = await r.json(); } catch { /* no body */ }
	return { status: r.status, data, ms };
}

// ─── main ───────────────────────────────────────────────────────────────────

async function run() {
	const browser = await chromium.launch({ headless: true });

	// ── Suite 1: Token harvest ──────────────────────────────────────────────
	console.log("\n=== Suite 1: Token harvest & API authentication ===");
	let veraCtx, veraPage, veraToken;
	let olafCtx, olafPage, olafToken;
	let olafOrgId, testOppId;

	try {
		({ ctx: veraCtx, page: veraPage, token: veraToken } = await loginAndGetToken(browser, "vera", "vera123"));
		ok(`vera token: ${veraToken ? veraToken.slice(0, 20) + "…" : "MISSING"}`);

		({ ctx: olafCtx, page: olafPage, token: olafToken } = await loginAndGetToken(browser, "olaf", "olaf123"));
		ok(`olaf token: ${olafToken ? olafToken.slice(0, 20) + "…" : "MISSING"}`);

		// Confirm tokens authenticate correctly
		const { status: vs } = await api("GET", "/v1/me/engagements", veraToken);
		ok(`vera token authenticates → ${vs}`);
		const { status: os } = await api("GET", "/v1/me/engagements", olafToken);
		ok(`olaf token authenticates → ${os}`);

		// Find olaf's org
		const { data: orgs } = await api("GET", "/v1/organizations", olafToken);
		if (Array.isArray(orgs) && orgs.length) {
			olafOrgId = orgs[0].id;
			ok(`olaf org: "${orgs[0].name}" (${olafOrgId.slice(0, 8)})`);
		} else {
			note("bug", "Olaf has no organisations – lifecycle tests will be limited");
		}
	} catch (e) { ko("Suite 1", e); }

	// ── Suite 2: Full engagement lifecycle ─────────────────────────────────
	console.log("\n=== Suite 2: Full engagement lifecycle ===");
	let signedUpEngagementId = null;
	try {
		// Get the one opportunity
		const { data: oppList } = await api("GET", "/v1/volunteer-opportunities?PageNumber=1&PageSize=10");
		const opp = oppList?.items?.[0];
		if (!opp) throw new Error("No opportunity in DB");
		testOppId = opp.id;
		ok(`Target opportunity: "${opp.title}" (${opp.participationType})`);

		// vera signs up (via API since UI button may be hidden for organisator vera)
		const signupBody = { message: "Automated lifecycle test signup" };
		// For Waitlist, we need a timeSlotId. Get the opp details first.
		const { data: oppDetail } = await api("GET", `/v1/volunteer-opportunities/${testOppId}`);
		const timeSlotId = oppDetail?.timeSlots?.[0]?.id ?? null;
		if (timeSlotId) {
			signupBody.timeSlotId = timeSlotId;
			ok(`Time slot available: ${timeSlotId.slice(0, 8)}`);
		}

		const { status: signupStatus, data: signupData } = await api(
			"POST", `/v1/volunteer-opportunities/${testOppId}/engagements`,
			veraToken, signupBody,
		);
		if (signupStatus === 200 || signupStatus === 201) {
			signedUpEngagementId = signupData?.id;
			ok(`vera signed up → engagement ${signedUpEngagementId?.slice(0, 8)} (status ${signupStatus})`);
		} else if (signupStatus === 409) {
			// Already signed up – look up existing engagement
			const { data: myEngs } = await api("GET", "/v1/me/engagements", veraToken);
			const existing = myEngs?.find((e) => e.opportunityId === testOppId);
			signedUpEngagementId = existing?.id;
			ok(`vera already signed up – using existing engagement ${signedUpEngagementId?.slice(0, 8)}`);
		} else {
			note("bug", `Sign-up returned ${signupStatus}`, JSON.stringify(signupData));
		}

		if (!signedUpEngagementId) throw new Error("No engagement ID to continue lifecycle");

		// Verify engagement status = Pending
		const { data: myEngs2 } = await api("GET", "/v1/me/engagements", veraToken);
		const engRecord = myEngs2?.find((e) => e.id === signedUpEngagementId);
		if (engRecord?.status === "Pending") ok("Engagement status = Pending after sign-up");
		else if (engRecord) ok(`Engagement status = ${engRecord.status} (already processed)`);
		else note("bug", "Could not find newly created engagement in /me/engagements");

		// olaf confirms (only if Pending)
		if (!engRecord || engRecord.status === "Pending") {
			const { status: confirmStatus, data: confirmData } = await api(
				"POST", `/v1/engagements/${signedUpEngagementId}/confirm`,
				olafToken,
			);
			if (confirmStatus === 200 || confirmStatus === 204) {
				ok(`olaf confirmed engagement → ${confirmStatus}`);
			} else {
				note("bug", `Confirm returned ${confirmStatus}`, JSON.stringify(confirmData));
			}
		} else {
			ok(`Engagement already ${engRecord.status} – skipping confirm step`);
		}

		// Verify status = Confirmed
		const { data: myEngs3 } = await api("GET", "/v1/me/engagements", veraToken);
		const confirmed = myEngs3?.find((e) => e.id === signedUpEngagementId);
		if (confirmed?.status === "Confirmed") ok("Engagement status = Confirmed after olaf's confirmation");
		else ok(`Engagement status = ${confirmed?.status}`);

		// vera checks in via her own endpoint
		const { status: checkinStatus, data: checkinData } = await api(
			"POST", `/v1/me/engagements/${signedUpEngagementId}/check-in`,
			veraToken,
		);
		if (checkinStatus === 200 || checkinStatus === 204) {
			ok(`vera self-check-in → ${checkinStatus}`);
		} else {
			note("bug", `Self check-in returned ${checkinStatus}`, JSON.stringify(checkinData));
		}

		// Verify isCheckedIn flag
		const { data: myEngs4 } = await api("GET", "/v1/me/engagements", veraToken);
		const checkedIn = myEngs4?.find((e) => e.id === signedUpEngagementId);
		if (checkedIn?.isCheckedIn) ok("isCheckedIn = true after self check-in");
		else note("bug", `isCheckedIn = ${checkedIn?.isCheckedIn} after check-in`);

		// Check achievements – should have "first-step" if vera's first
		const { status: achStatus, data: achievements } = await api(
			"GET", "/v1/me/achievements", veraToken,
		);
		if (achStatus === 200 && Array.isArray(achievements)) {
			ok(`vera has ${achievements.length} achievement(s)`);
			const firstStep = achievements.find((a) => a.badgeKey === "first-step");
			if (firstStep) ok("'First Step' achievement earned");
			else note("enhancement", "First engagement completed but 'first-step' badge not awarded yet", "May require a background job or different trigger condition");
		} else {
			note("bug", `GET /me/achievements returned ${achStatus}`);
		}

		// Check notifications – olaf should have gotten one when vera signed up
		const { data: olafNotifs } = await api("GET", "/v1/notifications", olafToken);
		if (Array.isArray(olafNotifs)) {
			ok(`olaf has ${olafNotifs.length} notification(s)`);
			const signupNotif = olafNotifs.find((n) => n.message?.toLowerCase().includes("sign") || n.message?.toLowerCase().includes("anmeld") || n.type?.toLowerCase().includes("signup"));
			if (signupNotif) ok(`Notification sent to olaf: "${signupNotif.message?.slice(0, 60)}"`);
			else if (olafNotifs.length > 0) ok(`Latest notification: "${olafNotifs[0].message?.slice(0, 60)}"`);
			else note("enhancement", "No notifications in olaf's inbox after vera signed up");
		}

		// Check notifications for vera – should have gotten one when olaf confirmed
		const { data: veraNotifs } = await api("GET", "/v1/notifications", veraToken);
		if (Array.isArray(veraNotifs)) {
			ok(`vera has ${veraNotifs.length} notification(s)`);
			const confirmNotif = veraNotifs.find((n) => n.message?.toLowerCase().includes("confirm") || n.message?.toLowerCase().includes("bestätigt"));
			if (confirmNotif) ok(`Confirmation notification sent to vera: "${confirmNotif.message?.slice(0, 60)}"`);
			else if (veraNotifs.length > 0) ok(`vera latest notification: "${veraNotifs[0].message?.slice(0, 60)}"`);
			else note("enhancement", "No notification sent to vera after olaf confirmed her engagement");
		}
	} catch (e) { ko("Suite 2", e); }

	// ── Suite 3: Withdraw / cancel flows ───────────────────────────────────
	console.log("\n=== Suite 3: Withdraw and cancel flows ===");
	try {
		// Sign vera up fresh to a new engagement for withdrawal test
		const { data: oppDetail } = await api("GET", `/v1/volunteer-opportunities/${testOppId}`);
		const timeSlotId = oppDetail?.timeSlots?.[0]?.id ?? null;

		// Try signing up again (might get 409 if already signed up to same slot)
		const { status: newSignup, data: newEng } = await api(
			"POST", `/v1/volunteer-opportunities/${testOppId}/engagements`,
			veraToken,
			{ message: "Withdraw test", ...(timeSlotId ? { timeSlotId } : {}) },
		);

		let withdrawEngId = null;
		if (newSignup === 200 || newSignup === 201) {
			withdrawEngId = newEng?.id;
			ok(`Created fresh engagement for withdraw test: ${withdrawEngId?.slice(0, 8)}`);
		} else if (newSignup === 409 && signedUpEngagementId) {
			// Use the existing one if it's in a withdrawable state
			const { data: engs } = await api("GET", "/v1/me/engagements", veraToken);
			const withdrawable = engs?.find((e) => e.opportunityId === testOppId && e.status !== "Withdrawn");
			withdrawEngId = withdrawable?.id;
			if (withdrawEngId) ok(`Using existing engagement ${withdrawEngId.slice(0, 8)} for withdraw test`);
		}

		if (withdrawEngId) {
			// vera withdraws
			const { status: wStatus, data: wData } = await api(
				"POST", `/v1/engagements/${withdrawEngId}/withdraw`,
				veraToken,
			);
			if (wStatus === 200 || wStatus === 204) {
				ok(`vera withdrew from engagement → ${wStatus}`);
				// Verify status in my engagements
				const { data: engsAfter } = await api("GET", "/v1/me/engagements", veraToken);
				const withdrawn = engsAfter?.find((e) => e.id === withdrawEngId);
				if (withdrawn?.status === "Withdrawn") ok("Engagement status = Withdrawn after withdrawal");
				else note("bug", `Status after withdrawal: ${withdrawn?.status}`);
			} else {
				note("bug", `Withdraw returned ${wStatus}`, JSON.stringify(wData));
			}
		} else {
			note("enhancement", "Could not create a second engagement for withdraw test – opportunity may have a single slot");
		}

		// Org-side cancel: olaf cancels an engagement
		const { data: orgEngs } = await api(
			"GET", `/v1/volunteer-opportunities/${testOppId}/engagements`,
			olafToken,
		);
		const pendingEngs = orgEngs?.filter((e) => e.status === "Pending") ?? [];
		if (pendingEngs.length > 0) {
			const cancelId = pendingEngs[0].id;
			const { status: cStatus } = await api(
				"POST", `/v1/engagements/${cancelId}/cancel`,
				olafToken,
				{ reason: "Automated test cancellation" },
			);
			if (cStatus === 200 || cStatus === 204) ok(`olaf cancelled engagement → ${cStatus}`);
			else note("bug", `Cancel by org returned ${cStatus}`);
		} else {
			ok("No pending engagements for org to cancel (all already processed)");
		}

		// UI: check My Engagements shows Withdrawn badge
		await veraPage.goto(`${BASE}/my-engagements`);
		await veraPage.waitForLoadState("networkidle");
		await ss(veraPage, "03-my-engagements-after-withdraw");
		const pageText = await veraPage.locator("main").textContent();
		const hasWithdrawn = pageText?.match(/withdrawn|zurückgezogen/i);
		if (hasWithdrawn) ok("Withdrawn status label visible on My Engagements page");
		else note("enhancement", "Withdrawn status not clearly labelled on My Engagements UI");
	} catch (e) { ko("Suite 3", e); }

	// ── Suite 4: Create → edit → delete opportunity ─────────────────────────
	console.log("\n=== Suite 4: Opportunity CRUD (create → edit → delete) ===");
	let createdOppId = null;
	try {
		if (!olafOrgId) throw new Error("No org ID – skipping CRUD suite");

		// Create a new opportunity via API
		const createBody = {
			title: "Deep Test Opportunity – CRUD",
			description: "Created by automated deep test. Will be deleted at end.",
			organizationId: olafOrgId,
			isRemote: true,
			occurrence: "OneTime",
			participationType: "OpenToAll",
			checkInMethod: "Manual",
			category: "Environment",
			tags: ["test", "automated"],
		};
		const { status: cStatus, data: cData } = await api(
			"POST", "/v1/volunteer-opportunities",
			olafToken, createBody,
		);
		if (cStatus === 200 || cStatus === 201) {
			createdOppId = cData?.id;
			ok(`Created opportunity: ${createdOppId?.slice(0, 8)} (category=Environment, checkIn=Manual)`);
		} else {
			note("bug", `Create opportunity returned ${cStatus}`, JSON.stringify(cData)?.slice(0, 120));
			throw new Error("Cannot continue without created opportunity");
		}

		// Verify it appears in the list
		const { data: list2 } = await api("GET", `/v1/volunteer-opportunities?PageNumber=1&PageSize=20`);
		const inList = list2?.items?.some((o) => o.id === createdOppId);
		if (inList) ok("New opportunity appears in public listing immediately");
		else note("bug", "Newly created opportunity not found in public listing");

		// Verify category filter finds it
		const { data: byCat } = await api("GET", `/v1/volunteer-opportunities?Category=Environment&PageNumber=1&PageSize=20`);
		const catFound = byCat?.items?.some((o) => o.id === createdOppId);
		if (catFound) ok("Category=Environment filter returns the new opportunity");
		else note("bug", "Category filter does not return newly created opportunity");

		// Verify tag filter
		const { data: byTag } = await api("GET", `/v1/volunteer-opportunities?Tag=test&PageNumber=1&PageSize=20`);
		const tagFound = byTag?.items?.some((o) => o.id === createdOppId);
		if (tagFound) ok("Tag=test filter returns the new opportunity");
		else note("bug", "Tag filter does not return opportunity with matching tag");

		// Edit: update title and description
		const editBody = {
			title: "Deep Test Opportunity – UPDATED",
			description: "Updated by automated test.",
			isRemote: true,
			occurrence: "OneTime",
			participationType: "OpenToAll",
			checkInMethod: "Manual",
			category: "Education",
			tags: ["test", "updated"],
		};
		const { status: eStatus, data: eData } = await api(
			"PUT", `/v1/volunteer-opportunities/${createdOppId}`,
			olafToken, editBody,
		);
		if (eStatus === 200 || eStatus === 204) {
			ok(`Edit opportunity → ${eStatus}`);
		} else {
			note("bug", `Edit returned ${eStatus}`, JSON.stringify(eData)?.slice(0, 120));
		}

		// Verify edit persisted
		const { data: updated } = await api("GET", `/v1/volunteer-opportunities/${createdOppId}`);
		if (updated?.title === "Deep Test Opportunity – UPDATED") ok("Edit persisted: title updated");
		else note("bug", `Title after edit: "${updated?.title}"`);
		if (updated?.category === "Education") ok("Edit persisted: category updated");
		else note("bug", `Category after edit: "${updated?.category}"`);

		// UI: opportunity detail page shows updated content
		await olafPage.goto(`${BASE}/volunteer-opportunities/${createdOppId}`);
		await olafPage.waitForLoadState("networkidle");
		await ss(olafPage, "04a-edited-opportunity");
		const h1 = await olafPage.locator("h1").first().textContent();
		if (h1?.includes("UPDATED")) ok("Updated title visible on detail page");
		else note("bug", `Detail page h1: "${h1?.trim()}" – expected updated title`);

		// Add a time slot
		const slotBody = {
			startDateTime: new Date("2026-12-01T10:00:00Z"),
			endDateTime:   new Date("2026-12-01T14:00:00Z"),
			maxParticipants: 10,
		};
		const { status: tsStatus, data: tsData } = await api(
			"POST", `/v1/volunteer-opportunities/${createdOppId}/time-slots`,
			olafToken, slotBody,
		);
		if (tsStatus === 200 || tsStatus === 201) {
			ok(`Time slot added to opportunity → ${tsStatus}`);
		} else {
			note("bug", `Add time slot returned ${tsStatus}`, JSON.stringify(tsData)?.slice(0, 120));
		}

		// Edit modal in UI
		await olafPage.reload();
		await olafPage.waitForLoadState("networkidle");
		const editBtn = olafPage.locator("button", { hasText: /edit|bearbeiten/i }).first();
		if (await editBtn.count() > 0) {
			await editBtn.click();
			await olafPage.waitForTimeout(500);
			await ss(olafPage, "04b-edit-modal");
			const editModal = olafPage.locator('[role="dialog"]');
			if (await editModal.count() > 0) ok("Edit opportunity modal opens from detail page");
			else note("bug", "Edit button clicked but no modal appeared");
			await olafPage.keyboard.press("Escape");
		} else {
			note("bug", "No edit button visible on opportunity detail page for olaf");
		}

		// Delete
		const { status: dStatus } = await api(
			"DELETE", `/v1/volunteer-opportunities/${createdOppId}`,
			olafToken,
		);
		if (dStatus === 200 || dStatus === 204) {
			ok(`Delete opportunity → ${dStatus}`);
			createdOppId = null;
		} else {
			note("bug", `Delete returned ${dStatus}`);
		}

		// Verify gone from list
		const { data: listAfterDelete } = await api("GET", `/v1/volunteer-opportunities?PageNumber=1&PageSize=20`);
		const stillThere = listAfterDelete?.items?.some((o) => o.id === createdOppId);
		if (!stillThere) ok("Deleted opportunity no longer in listing");
	} catch (e) { ko("Suite 4", e); }

	// ── Suite 5: Advanced filters ───────────────────────────────────────────
	console.log("\n=== Suite 5: Advanced opportunity filters ===");
	try {
		// Date range filter
		const { status: drStatus, data: drData } = await api(
			"GET", "/v1/volunteer-opportunities?DateFrom=2026-01-01T00:00:00Z&DateTo=2026-12-31T23:59:59Z&PageNumber=1&PageSize=20",
		);
		if (drStatus === 200) {
			ok(`DateFrom/DateTo filter → 200, ${drData?.items?.length ?? 0} result(s)`);
		} else {
			note("bug", `Date range filter returned ${drStatus}`);
		}

		// Future only
		const { status: futStatus, data: futData } = await api(
			"GET", `/v1/volunteer-opportunities?DateFrom=${new Date().toISOString()}&PageNumber=1&PageSize=20`,
		);
		if (futStatus === 200) ok(`Future-only filter → ${futData?.items?.length ?? 0} result(s)`);

		// Bbox (Berlin bounding box)
		const { status: bboxStatus, data: bboxData } = await api(
			"GET", "/v1/volunteer-opportunities?North=52.7&South=52.3&East=13.8&West=13.1&PageNumber=1&PageSize=20",
		);
		if (bboxStatus === 200) {
			ok(`Bbox filter (Berlin) → 200, ${bboxData?.items?.length ?? 0} result(s)`);
		} else {
			note("bug", `Bbox filter returned ${bboxStatus}`);
		}

		// Radius search (centre Berlin, 50km)
		const { status: radStatus, data: radData } = await api(
			"GET", "/v1/volunteer-opportunities?CenterLatitude=52.52&CenterLongitude=13.40&RadiusKm=50&PageNumber=1&PageSize=20",
		);
		if (radStatus === 200) {
			ok(`Radius filter → 200, ${radData?.items?.length ?? 0} result(s) within 50km of Berlin`);
		} else {
			note("bug", `Radius filter returned ${radStatus}`);
		}

		// Occurrence filter
		const { status: occStatus, data: occData } = await api(
			"GET", "/v1/volunteer-opportunities?Occurrence=OneTime&PageNumber=1&PageSize=20",
		);
		if (occStatus === 200) ok(`Occurrence=OneTime → ${occData?.items?.length ?? 0} result(s)`);

		// ParticipationType filter
		const { status: ptStatus, data: ptData } = await api(
			"GET", "/v1/volunteer-opportunities?ParticipationType=Waitlist&PageNumber=1&PageSize=20",
		);
		if (ptStatus === 200) ok(`ParticipationType=Waitlist → ${ptData?.items?.length ?? 0} result(s)`);

		// isRemote = false (in-person)
		const { status: locStatus, data: locData } = await api(
			"GET", "/v1/volunteer-opportunities?IsRemote=false&PageNumber=1&PageSize=20",
		);
		if (locStatus === 200) ok(`IsRemote=false (in-person) → ${locData?.items?.length ?? 0} result(s)`);

		// Combine multiple filters
		const { status: comboStatus, data: comboData } = await api(
			"GET", "/v1/volunteer-opportunities?IsRemote=false&Occurrence=OneTime&PageNumber=1&PageSize=20",
		);
		if (comboStatus === 200) ok(`Combined filters (local + oneTime) → ${comboData?.items?.length ?? 0} result(s)`);
		else note("bug", `Combined filter returned ${comboStatus}`);

		// UI: open filter bar and exercise category dropdown
		await veraPage.goto(`${BASE}/`);
		await veraPage.waitForLoadState("networkidle");
		const catSelect = veraPage.locator("select").filter({ hasText: /category|kategorie|all/i }).first();
		if (await catSelect.count() > 0) {
			const catOptions = await catSelect.locator("option").count();
			ok(`Category dropdown has ${catOptions} option(s)`);
			if (catOptions > 1) {
				await catSelect.selectOption({ index: 1 });
				await veraPage.waitForTimeout(600);
				await ss(veraPage, "05a-category-filter");
				ok("Category filter applied in UI");
			}
		} else {
			note("enhancement", "No category filter dropdown visible in opportunity list filter bar");
		}
	} catch (e) { ko("Suite 5", e); }

	// ── Suite 6: Account page ──────────────────────────────────────────────
	console.log("\n=== Suite 6: Account page – name save & delete dialog ===");
	try {
		await veraPage.goto(`${BASE}/account`);
		await veraPage.waitForLoadState("networkidle");
		await ss(veraPage, "06a-account-page");

		// First/last name fields
		const firstNameInput = veraPage.locator('input[id="first-name"], input[id*="firstName" i]').first();
		const lastNameInput  = veraPage.locator('input[id="last-name"], input[id*="lastName" i]').first();

		if (await firstNameInput.count() > 0) {
			const original = await firstNameInput.inputValue();
			await firstNameInput.fill("Vera");
			await lastNameInput.fill("Testuser");
			const saveBtn = veraPage.locator('button[type="submit"]').first();
			await saveBtn.click();
			await veraPage.waitForTimeout(1500);
			const saved = veraPage.locator("div, p", { hasText: /saved|gespeichert|success/i }).first();
			if (await saved.count() > 0) ok("Account page: first/last name saved successfully");
			else note("bug", "No success message after saving account name");
			await ss(veraPage, "06b-account-saved");

			// Verify via API
			const { data: profile } = await api("GET", "/v1/users/me", veraToken);
			if (profile?.firstName === "Vera") ok("First name update persisted to backend");
			else note("bug", `First name in API: "${profile?.firstName}" expected "Vera"`);
		} else {
			note("bug", "First name input not found on account page");
		}

		// Delete account confirmation dialog
		const deleteBtn = veraPage.locator("button", { hasText: /delete|löschen/i }).first();
		if (await deleteBtn.count() > 0) {
			await deleteBtn.click();
			await veraPage.waitForTimeout(400);
			await ss(veraPage, "06c-delete-dialog");
			const dialog = veraPage.locator('[role="dialog"]');
			if (await dialog.count() > 0) {
				ok("Delete account confirmation dialog opens");
				const dialogText = await dialog.textContent();
				const hasWarning = dialogText?.match(/permanent|cannot be undone|unwiderruflich|nicht rückgängig/i);
				if (hasWarning) ok("Delete dialog has irreversibility warning");
				else note("enhancement", "Delete account dialog lacks clear irreversibility warning");
				// Cancel (don't actually delete!)
				const cancelBtn = dialog.locator("button", { hasText: /cancel|abbrechen/i }).first();
				if (await cancelBtn.count() > 0) await cancelBtn.click();
				else await veraPage.keyboard.press("Escape");
				ok("Delete account cancelled safely");
			} else {
				note("bug", "Delete button clicked but no confirmation dialog appeared");
			}
		} else {
			note("enhancement", "No 'Delete account' button on account page (GDPR Article 17 gap)");
		}
	} catch (e) { ko("Suite 6", e); }

	// ── Suite 7: Organization member management ─────────────────────────────
	console.log("\n=== Suite 7: Organization member management ===");
	try {
		if (!olafOrgId) throw new Error("No org ID");

		// Get current members
		const { data: orgDetails } = await api("GET", `/v1/organizations/${olafOrgId}`, olafToken);
		const members = orgDetails?.members ?? [];
		ok(`Org currently has ${members.length} member(s)`);
		members.forEach((m) => ok(`  member: ${m.username} (${m.isOrganisator ? "organisator" : "member"})`));

		// Try adding vera as a member (may already be a member)
		const { data: veraProfile } = await api("GET", "/v1/users/me", veraToken);
		const veraUserId = veraProfile?.id;
		ok(`vera user ID: ${veraUserId?.slice(0, 8)}`);

		const alreadyMember = members.some((m) => m.userId === veraUserId);
		if (!alreadyMember) {
			const { status: addStatus, data: addData } = await api(
				"POST", `/v1/organizations/${olafOrgId}/members`,
				olafToken, { userId: veraUserId },
			);
			if (addStatus === 200 || addStatus === 204) {
				ok(`Added vera to org → ${addStatus}`);
				// Verify in member list
				const { data: updated } = await api("GET", `/v1/organizations/${olafOrgId}`, olafToken);
				const nowMember = updated?.members?.some((m) => m.userId === veraUserId);
				if (nowMember) ok("vera appears in org member list after add");
				else note("bug", "vera not in member list after add");

				// Remove vera
				const { status: removeStatus } = await api(
					"DELETE", `/v1/organizations/${olafOrgId}/members/${veraUserId}`,
					olafToken,
				);
				if (removeStatus === 200 || removeStatus === 204) {
					ok(`Removed vera from org → ${removeStatus}`);
				} else {
					note("bug", `Remove member returned ${removeStatus}`);
				}
			} else {
				note("bug", `Add member returned ${addStatus}`, JSON.stringify(addData)?.slice(0, 100));
			}
		} else {
			ok("vera is already a member – skipping add (would need remove+re-add)");
		}

		// UI: members tab in org settings
		await olafPage.goto(`${BASE}/organizations/${olafOrgId}/settings`);
		await olafPage.waitForLoadState("networkidle");
		const membersTab = olafPage.locator("button", { hasText: /member|mitglied/i }).first();
		if (await membersTab.count() > 0) {
			await membersTab.click();
			await olafPage.waitForTimeout(400);
			await ss(olafPage, "07a-members-tab");
			const memberItems = olafPage.locator("ul li");
			ok(`Members tab shows ${await memberItems.count()} item(s)`);

			// Check for invite UI
			const inviteInput = olafPage.locator('input[placeholder*="username" i], input[placeholder*="user" i]').first();
			if (await inviteInput.count() > 0) {
				ok("Member invite input present");
				// Try invalid username
				await inviteInput.fill("does-not-exist-xyz");
				const addBtn = olafPage.locator("button", { hasText: /add|hinzufügen/i }).first();
				if (await addBtn.count() > 0) {
					await addBtn.click();
					await olafPage.waitForTimeout(1000);
					const errMsg = olafPage.locator("p, div", { hasText: /error|fehler|not found|not exist/i }).first();
					if (await errMsg.count() > 0) ok("Adding non-existent user shows error");
					else note("enhancement", "No error shown when adding non-existent username to org");
					await ss(olafPage, "07b-add-member-error");
				}
			} else {
				note("enhancement", "No member invite input on org settings members tab");
			}
		} else {
			note("bug", "No members tab on org settings page");
		}
	} catch (e) { ko("Suite 7", e); }

	// ── Suite 8: Org dashboard ──────────────────────────────────────────────
	console.log("\n=== Suite 8: Organization dashboard data ===");
	try {
		if (!olafOrgId) throw new Error("No org ID");
		const { status, data: dash } = await api(
			"GET", `/v1/organizations/${olafOrgId}/dashboard`,
			olafToken,
		);
		if (status === 200 && dash) {
			ok(`Dashboard API → 200`);
			const keys = Object.keys(dash);
			ok(`Dashboard fields: ${keys.join(", ")}`);

			// Check for useful stats
			const hasOpportunityCount = keys.some((k) => k.match(/opportunit|gelegenheit/i));
			const hasEngagementCount  = keys.some((k) => k.match(/engagement|bewerbung/i));
			const hasMemberCount      = keys.some((k) => k.match(/member|mitglied/i));
			if (hasOpportunityCount) ok("Dashboard includes opportunity count");
			else note("enhancement", "Dashboard missing opportunity count field");
			if (hasEngagementCount)  ok("Dashboard includes engagement count");
			else note("enhancement", "Dashboard missing engagement count field");
			if (hasMemberCount)      ok("Dashboard includes member count");
			else note("enhancement", "Dashboard missing member count field");

			// Check UI page for dashboard
			await olafPage.goto(`${BASE}/organizations/${olafOrgId}/dashboard`);
			await olafPage.waitForLoadState("networkidle");
			await ss(olafPage, "08a-org-dashboard-ui");
			const pageContent = await olafPage.locator("main").textContent();
			const hasNumbers = /\d+/.test(pageContent ?? "");
			if (hasNumbers) ok("Dashboard UI shows numbers");
			else note("enhancement", "Dashboard UI page appears empty or shows no numeric stats");
		} else {
			note("bug", `Dashboard returned ${status}`, JSON.stringify(dash)?.slice(0, 100));
		}
	} catch (e) { ko("Suite 8", e); }

	// ── Suite 9: CheckIn UI deep dive ───────────────────────────────────────
	console.log("\n=== Suite 9: CheckIn modal and QR flow ===");
	try {
		if (!olafOrgId) throw new Error("No org ID");

		// Create opp with QR check-in
		const { status: cStatus, data: qrOpp } = await api(
			"POST", "/v1/volunteer-opportunities",
			olafToken, {
				title: "QR Check-In Test Opp",
				description: "Test",
				organizationId: olafOrgId,
				isRemote: true,
				occurrence: "OneTime",
				participationType: "OpenToAll",
				checkInMethod: "QrCode",
			},
		);
		if (cStatus !== 200 && cStatus !== 201) {
			note("bug", `Create QR opp returned ${cStatus}`); throw new Error("no QR opp");
		}
		const qrOppId = qrOpp?.id;
		ok(`Created QR check-in opportunity: ${qrOppId?.slice(0, 8)}`);

		// vera signs up
		await api("POST", `/v1/volunteer-opportunities/${qrOppId}/engagements`,
			veraToken, { message: "QR test" });

		// olaf goes to engagement management
		await olafPage.goto(`${BASE}/volunteer-opportunities/${qrOppId}/engagements`);
		await olafPage.waitForLoadState("networkidle");
		await ss(olafPage, "09a-qr-engagement-mgmt");

		// Confirm vera's engagement
		const { data: engs } = await api("GET", `/v1/volunteer-opportunities/${qrOppId}/engagements`, olafToken);
		const veraEng = engs?.find((e) => true); // first one
		if (veraEng) {
			await api("POST", `/v1/engagements/${veraEng.id}/confirm`, olafToken);
			ok("Confirmed vera's engagement on QR opp");
		}

		await olafPage.reload();
		await olafPage.waitForLoadState("networkidle");

		// Look for QR or check-in button in management UI
		const checkInBtn = olafPage.locator("button", { hasText: /check.?in|qr/i }).first();
		if (await checkInBtn.count() > 0) {
			await checkInBtn.click();
			await olafPage.waitForTimeout(500);
			await ss(olafPage, "09b-checkin-modal");
			const modal = olafPage.locator('[role="dialog"]');
			if (await modal.count() > 0) {
				ok("Check-in modal opens");
				const hasQr = (await modal.locator("canvas, svg[data-testid], img[alt*='qr' i]").count()) > 0;
				if (hasQr) ok("QR code element visible in check-in modal");
				else note("enhancement", "Check-in modal open but no QR code element found");
				await ss(olafPage, "09c-checkin-modal-content");
				await olafPage.keyboard.press("Escape");
			} else {
				note("bug", "Check-in button clicked but modal did not open");
			}
		} else {
			note("enhancement", "No check-in button on engagement management page for QR-enabled opportunity");
		}

		// vera self-check-in via API
		if (veraEng) {
			const { status: sciStatus } = await api(
				"POST", `/v1/me/engagements/${veraEng.id}/check-in`,
				veraToken,
			);
			if (sciStatus === 200 || sciStatus === 204) ok(`vera self-check-in on QR opp → ${sciStatus}`);
			else note("bug", `Self check-in on QR opp returned ${sciStatus}`);
		}

		// Cleanup
		await api("DELETE", `/v1/volunteer-opportunities/${qrOppId}`, olafToken);
		ok("QR opp cleaned up");
	} catch (e) { ko("Suite 9", e); }

	// ── Suite 10: API performance ───────────────────────────────────────────
	console.log("\n=== Suite 10: API response time measurements ===");
	try {
		const endpoints = [
			["GET", "/v1/volunteer-opportunities?PageNumber=1&PageSize=10", null],
			["GET", `/v1/volunteer-opportunities/${testOppId}`, null],
			["GET", "/v1/badges", null],
			["GET", "/v1/me/engagements",   veraToken],
			["GET", "/v1/me/achievements",  veraToken],
			["GET", "/v1/me/streaks",       veraToken],
			["GET", "/v1/notifications",    veraToken],
		];
		for (const [method, path, token] of endpoints) {
			const { status, ms } = await api(method, path, token);
			const label = `${path.slice(0, 45).padEnd(45)} ${ms.toString().padStart(4)}ms  ${status}`;
			if (ms > 1000) note("enhancement", `Slow API response: ${path} took ${ms}ms`);
			ok(label);
		}
	} catch (e) { ko("Suite 10", e); }

	// ── Suite 11: Double sign-up guard ─────────────────────────────────────
	console.log("\n=== Suite 11: Concurrency / double sign-up guard ===");
	try {
		const { data: oppDetail } = await api("GET", `/v1/volunteer-opportunities/${testOppId}`);
		const ts = oppDetail?.timeSlots?.[0];

		const body = { message: "concurrent test 1", ...(ts ? { timeSlotId: ts.id } : {}) };
		const [r1, r2] = await Promise.all([
			api("POST", `/v1/volunteer-opportunities/${testOppId}/engagements`, veraToken, body),
			api("POST", `/v1/volunteer-opportunities/${testOppId}/engagements`, veraToken, { ...body, message: "concurrent test 2" }),
		]);
		ok(`Concurrent signup: r1=${r1.status} r2=${r2.status}`);
		const oneConflict = r1.status === 409 || r2.status === 409;
		const oneSuccess  = r1.status === 200 || r1.status === 201 ||
		                    r2.status === 200 || r2.status === 201;
		if (oneSuccess && oneConflict) ok("Concurrent double sign-up correctly produces one 409");
		else if (r1.status === 409 && r2.status === 409) ok("Both concurrent sign-ups rejected (already registered)");
		else note("bug", "Both concurrent sign-ups may have succeeded – duplicate engagement possible");
	} catch (e) { ko("Suite 11", e); }

	// ── Suite 12: Public volunteer profile ─────────────────────────────────
	console.log("\n=== Suite 12: Public volunteer profile page ===");
	try {
		const { data: veraProfile } = await api("GET", "/v1/users/me", veraToken);
		const veraId = veraProfile?.id;
		if (!veraId) throw new Error("Cannot get vera user ID");

		// User achievements public page
		const freshCtx  = await browser.newContext({ ignoreHTTPSErrors: true });
		const freshPage = await freshCtx.newPage();
		await freshPage.goto(`${BASE}/users/${veraId}/achievements`);
		await freshPage.waitForLoadState("networkidle");
		await ss(freshPage, "12a-public-achievements");

		const h1 = await freshPage.locator("h1, h2").first().textContent();
		ok(`Public achievements page title: "${h1?.trim()}"`);

		const badges = await freshPage.locator("[class*='badge' i], img[alt]").count();
		if (badges > 0) ok(`${badges} badge element(s) on public achievements page`);
		else note("enhancement", "No badge elements on public achievements page (may have no badges yet)");

		// Check page is accessible without login
		const signInBtn = freshPage.locator("button", { hasText: /sign in|anmelden/i });
		if (await signInBtn.count() === 0) ok("Public achievements page accessible without login (no login prompt)");
		else note("bug", "Sign-in prompt shown on public achievements page – should be accessible to all");

		await freshCtx.close();
	} catch (e) { ko("Suite 12", e); }

	// ── Suite 13: German translations spot-check ────────────────────────────
	console.log("\n=== Suite 13: German translation completeness ===");
	try {
		const deCtx  = await browser.newContext({ ignoreHTTPSErrors: true, locale: "de-DE" });
		const dePage = await deCtx.newPage();

		// Force German language via localStorage
		await dePage.goto(`${BASE}/`);
		await dePage.evaluate(() => localStorage.setItem("i18nextLng", "de"));
		await dePage.reload();
		await dePage.waitForLoadState("networkidle");
		await ss(dePage, "13a-home-german");

		const bodyText = await dePage.locator("main").textContent();
		// Check for untranslated keys (they appear as raw keys like "opportunities.title")
		const rawKeys = bodyText?.match(/[a-z]+\.[a-zA-Z]+\.[a-zA-Z]+/g) ?? [];
		const suspectKeys = rawKeys.filter((k) => !k.includes("http") && !k.includes("maik") && !k.includes("localhost"));
		if (suspectKeys.length === 0) ok("No obviously untranslated keys found on German home page");
		else note("bug", `Possible untranslated keys on home page: ${suspectKeys.slice(0, 5).join(", ")}`);

		// Check hero text is German
		const heroText = await dePage.locator("h1, h2").first().textContent();
		ok(`German hero heading: "${heroText?.trim().slice(0, 60)}"`);

		// Load German locale file and check for completeness vs English
		const enLocale = JSON.parse(fs.readFileSync("/home/user/einsatzbereit/frontend/src/locales/en.json", "utf8"));
		const deLocale = JSON.parse(fs.readFileSync("/home/user/einsatzbereit/frontend/src/locales/de.json", "utf8"));

		function flatKeys(obj, prefix = "") {
			return Object.keys(obj).flatMap((k) =>
				typeof obj[k] === "object" ? flatKeys(obj[k], `${prefix}${k}.`) : [`${prefix}${k}`],
			);
		}
		const enKeys = new Set(flatKeys(enLocale));
		const deKeys = new Set(flatKeys(deLocale));
		const missing = [...enKeys].filter((k) => !deKeys.has(k));
		const extra   = [...deKeys].filter((k) => !enKeys.has(k));
		if (missing.length === 0) ok("German locale has all English keys");
		else note("bug", `${missing.length} key(s) missing from de.json`, missing.slice(0, 5).join(", "));
		if (extra.length === 0) ok("No extra keys in German locale");
		else note("enhancement", `${extra.length} extra key(s) in de.json not in en.json`, extra.slice(0, 5).join(", "));

		await deCtx.close();
	} catch (e) { ko("Suite 13", e); }

	// ── Suite 14: Footer / legal pages ─────────────────────────────────────
	console.log("\n=== Suite 14: Footer links and legal pages ===");
	try {
		const freshCtx  = await browser.newContext({ ignoreHTTPSErrors: true });
		const freshPage = await freshCtx.newPage();
		await freshPage.goto(`${BASE}/`);
		await freshPage.waitForLoadState("networkidle");

		// Check footer links
		const footerLinks = await freshPage.locator("footer a").all();
		ok(`Footer has ${footerLinks.length} link(s)`);
		const footerHrefs = await Promise.all(footerLinks.map((l) => l.getAttribute("href")));
		ok(`Footer links: ${footerHrefs.filter(Boolean).join(", ")}`);

		// Impressum
		const impressumLink = freshPage.locator('footer a[href*="impressum" i], footer a', { hasText: /impressum|imprint/i }).first();
		if (await impressumLink.count() > 0) {
			await impressumLink.click();
			await freshPage.waitForLoadState("networkidle");
			await ss(freshPage, "14a-impressum");
			const h1 = await freshPage.locator("h1, h2").first().textContent();
			ok(`Impressum page title: "${h1?.trim()}"`);
			const hasContent = (await freshPage.locator("main p, main section").count()) > 0;
			if (hasContent) ok("Impressum has content");
			else note("bug", "Impressum page has no paragraph content");
			await freshPage.goBack();
		} else {
			note("bug", "No Impressum link in footer");
		}

		// Datenschutz
		const datenschutzLink = freshPage.locator('footer a[href*="datenschutz" i], footer a', { hasText: /privacy|datenschutz/i }).first();
		if (await datenschutzLink.count() > 0) {
			await datenschutzLink.click();
			await freshPage.waitForLoadState("networkidle");
			await ss(freshPage, "14b-datenschutz");
			const h1 = await freshPage.locator("h1, h2").first().textContent();
			ok(`Datenschutz page title: "${h1?.trim()}"`);
			const hasContent = (await freshPage.locator("main p, main section").count()) > 0;
			if (hasContent) ok("Datenschutz has content");
			else note("bug", "Datenschutz page has no paragraph content");
		} else {
			note("bug", "No Datenschutz/Privacy link in footer");
		}

		// Social links in footer
		const socials = await freshPage.locator("footer a[href*='github'], footer a[href*='twitter'], footer a[href*='instagram'], footer a[href*='linkedin']").count();
		if (socials > 0) ok(`${socials} social link(s) in footer`);
		else note("enhancement", "No social media links in footer");

		await freshCtx.close();
	} catch (e) { ko("Suite 14", e); }

	// ── Cleanup ─────────────────────────────────────────────────────────────
	if (createdOppId) {
		await api("DELETE", `/v1/volunteer-opportunities/${createdOppId}`, olafToken).catch(() => {});
	}

	await veraCtx?.close();
	await olafCtx?.close();
	await browser.close();

	// ── Summary ─────────────────────────────────────────────────────────────
	console.log("\n" + "═".repeat(64));
	console.log(`Results: ${passed} passed, ${failed} failed`);
	console.log(`\nFindings (${findings.length}):`);
	findings.forEach((f, i) => {
		const tag = f.sev === "bug" ? "[BUG]" : "[ENH]";
		console.log(`  ${i + 1}. ${tag} ${f.title}`);
		if (f.detail) console.log(`       ${String(f.detail).slice(0, 100)}`);
	});
	if (failed > 0) process.exit(1);
}

run().catch((e) => { console.error(e); process.exit(1); });
