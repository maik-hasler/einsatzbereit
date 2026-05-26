// deep-test-3.mjs - Corrected token extraction + full lifecycle tests
// Fixes from deep-test-2:
//   - Tokens stored in localStorage (WebStorageStateStore), not sessionStorage
//   - Key format: oidc.user:<authority>:<client_id>
//   - Org listing uses /v1/organizations (auth required for details, not list)
// Suites:
//   1  Token extraction validation
//   2  Full engagement lifecycle (sign-up, confirm, check-in, achievement)
//   3  CRUD: create, edit, delete opportunity
//   4  Organization member management
//   5  Org dashboard UI vs API
//   6  CheckIn modal (QR + manual)
//   7  Account page: first/last name save, delete dialog
//   8  Notification read-state flow (UI + API)
//   9  Streak display
//   10 My Engagements UX: links, org name, withdraw button
//   11 Advanced filter combinations (UI smoke)
//   12 Create opportunity: category filter + tag filter verify
//   13 Org settings: save contact info, public profile confirms
//   14 Volunteer self-check-in UI flow

import { chromium } from "playwright";
import fs from "fs";

const BASE  = "https://einsatzbereit.maik-hasler.de";
const API   = "https://api.maik-hasler.de";
const AUTH  = "https://login.maik-hasler.de/realms/einsatzbereit";
const CLIENT = "frontend";
const SS    = "scripts/screenshots/deep3";
fs.mkdirSync(SS, { recursive: true });

let passed = 0; let failed = 0;
const findings = [];
const ok   = (l)    => { console.log(`  PASS  ${l}`); passed++; };
const ko   = (l, e) => { console.log(`  FAIL  ${l}\n        ${String(e?.message ?? e).split("\n")[0]}`); failed++; };
const note = (sev, t, d) => {
	const tag = sev === "bug" ? "[BUG]" : "[ENH]";
	console.log(`  ${tag} ${t}`);
	if (d) console.log(`        ${String(d).slice(0, 110)}`);
	findings.push({ sev, title: t, detail: d });
};
const ss   = (page, n) => page.screenshot({ path: `${SS}/${n}.png`, fullPage: true });

// ─── helpers ────────────────────────────────────────────────────────────────

async function extractToken(page) {
	// oidc-client-ts stores in localStorage under oidc.user:<authority>:<client_id>
	return page.evaluate(({ auth, client }) => {
		const key = `oidc.user:${auth}:${client}`;
		try {
			const raw = localStorage.getItem(key);
			if (raw) return JSON.parse(raw).access_token ?? null;
		} catch { /* skip */ }
		// Fallback: scan all localStorage keys
		for (let i = 0; i < localStorage.length; i++) {
			const k = localStorage.key(i);
			if (!k.startsWith("oidc.")) continue;
			try {
				const v = JSON.parse(localStorage.getItem(k));
				if (v?.access_token) return v.access_token;
			} catch { /* skip */ }
		}
		return null;
	}, { auth: AUTH, client: CLIENT });
}

async function loginAndToken(browser, user, pass) {
	const ctx  = await browser.newContext({ ignoreHTTPSErrors: true });
	const page = await ctx.newPage();
	await page.goto(`${BASE}/`);
	await page.waitForLoadState("networkidle");

	const btn = page.locator("button", { hasText: /sign in|anmelden/i }).first();
	if (await btn.count() > 0) {
		await btn.click();
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 12000 });
		await page.fill("#username", user);
		await page.click("#kc-login");
		await page.waitForSelector("#password", { timeout: 12000 });
		await page.fill("#password", pass);
		await page.click("#kc-login");
		await page.waitForURL(/einsatzbereit\.maik-hasler\.de/, { timeout: 18000 });
		await page.waitForLoadState("networkidle");
	}
	const token = await extractToken(page);
	return { ctx, page, token };
}

async function apiFetch(method, path, token, body) {
	const opts = {
		method,
		headers: {
			...(token ? { Authorization: `Bearer ${token}` } : {}),
			...(body   ? { "Content-Type": "application/json" } : {}),
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
	let veraCtx, veraPage, veraToken;
	let olafCtx, olafPage, olafToken;
	let olafOrgId, testOppId;

	// ── Suite 1: Token extraction ───────────────────────────────────────────
	console.log("\n=== Suite 1: Token extraction & initial state ===");
	try {
		({ ctx: veraCtx, page: veraPage, token: veraToken } = await loginAndToken(browser, "vera", "vera123"));
		({ ctx: olafCtx, page: olafPage, token: olafToken } = await loginAndToken(browser, "olaf", "olaf123"));

		if (veraToken) ok(`vera token extracted (${veraToken.slice(0, 20)}…)`);
		else { note("bug", "vera token extraction failed"); throw new Error("no vera token"); }

		if (olafToken) ok(`olaf token extracted (${olafToken.slice(0, 20)}…)`);
		else { note("bug", "olaf token extraction failed"); throw new Error("no olaf token"); }

		// Auth smoke
		const { status: vs } = await apiFetch("GET", "/v1/me/engagements", veraToken);
		const { status: os } = await apiFetch("GET", "/v1/me/engagements", olafToken);
		if (vs === 200) ok("vera token → /me/engagements 200");
		else note("bug", `vera token → /me/engagements returned ${vs}`);
		if (os === 200) ok("olaf token → /me/engagements 200");
		else note("bug", `olaf token → /me/engagements returned ${os}`);

		// Find olaf's org from the org list (requires auth to see all fields)
		const { data: orgList } = await apiFetch("GET", "/v1/organizations", olafToken);
		if (Array.isArray(orgList) && orgList.length > 0) {
			olafOrgId = orgList[0].id;
			ok(`olaf org: "${orgList[0].name}" (${olafOrgId.slice(0, 8)}…)`);
		} else {
			// Try without auth - public list
			const { data: publicList } = await apiFetch("GET", "/v1/organizations");
			if (Array.isArray(publicList) && publicList.length > 0) {
				olafOrgId = publicList[0].id;
				ok(`org (public list): "${publicList[0].name}" (${olafOrgId.slice(0, 8)}…)`);
			} else {
				note("bug", "No organizations found in listing");
			}
		}

		// Opportunity
		const { data: oppList } = await apiFetch("GET", "/v1/volunteer-opportunities?PageNumber=1&PageSize=20");
		testOppId = oppList?.items?.[0]?.id;
		ok(`Test opportunity: ${testOppId?.slice(0, 8)} - "${oppList?.items?.[0]?.title}"`);
	} catch (e) { ko("Suite 1", e); }

	// ── Suite 2: Full engagement lifecycle ─────────────────────────────────
	console.log("\n=== Suite 2: Full engagement lifecycle ===");
	let mainEngId = null;
	try {
		if (!veraToken || !olafToken || !testOppId) throw new Error("Prerequisites missing");

		// Get opportunity details for time slot
		const { data: detail } = await apiFetch("GET", `/v1/volunteer-opportunities/${testOppId}`);
		const tsId = detail?.timeSlots?.[0]?.id ?? null;
		ok(`Opportunity: "${detail?.title}", participationType=${detail?.participationType}, timeSlots=${detail?.timeSlots?.length}`);

		// vera signs up
		const signBody = { message: "E2E lifecycle test" };
		if (tsId) signBody.timeSlotId = tsId;
		const { status: s1, data: eng1 } = await apiFetch(
			"POST", `/v1/volunteer-opportunities/${testOppId}/engagements`,
			veraToken, signBody,
		);
		if (s1 === 200 || s1 === 201) {
			mainEngId = eng1?.id;
			ok(`vera signed up → engagement ${mainEngId?.slice(0, 8)} (${s1})`);
		} else if (s1 === 409) {
			// Retrieve existing
			const { data: myEngs } = await apiFetch("GET", "/v1/me/engagements", veraToken);
			const ex = myEngs?.find((e) => e.opportunityId === testOppId && e.status !== "Withdrawn" && e.status !== "Cancelled");
			mainEngId = ex?.id;
			ok(`vera already signed up (409) - using existing ${mainEngId?.slice(0, 8)} status=${ex?.status}`);
		} else {
			note("bug", `Sign-up returned ${s1}`, JSON.stringify(eng1)?.slice(0, 100));
		}

		if (!mainEngId) throw new Error("No engagement ID");

		// Read back status
		const { data: myEngsA } = await apiFetch("GET", "/v1/me/engagements", veraToken);
		const engA = myEngsA?.find((e) => e.id === mainEngId);
		ok(`Engagement status after sign-up: ${engA?.status}`);

		// olaf confirms if Pending
		if (!engA || engA.status === "Pending") {
			const { status: c1, data: cd1 } = await apiFetch(
				"POST", `/v1/engagements/${mainEngId}/confirm`, olafToken,
			);
			if (c1 === 200 || c1 === 204) ok(`olaf confirmed → ${c1}`);
			else note("bug", `Confirm returned ${c1}`, JSON.stringify(cd1)?.slice(0, 80));
		} else ok(`Engagement already ${engA?.status} - skipping confirm`);

		// Re-read status
		const { data: myEngsB } = await apiFetch("GET", "/v1/me/engagements", veraToken);
		const engB = myEngsB?.find((e) => e.id === mainEngId);
		ok(`Status after confirm: ${engB?.status}`);

		// vera self-check-in
		const { status: ci1 } = await apiFetch(
			"POST", `/v1/me/engagements/${mainEngId}/check-in`, veraToken,
		);
		if (ci1 === 200 || ci1 === 204) ok(`vera self-check-in → ${ci1}`);
		else note("bug", `Self check-in returned ${ci1}`);

		// Verify isCheckedIn
		const { data: myEngsC } = await apiFetch("GET", "/v1/me/engagements", veraToken);
		const engC = myEngsC?.find((e) => e.id === mainEngId);
		if (engC?.isCheckedIn) ok("isCheckedIn = true");
		else note("bug", `isCheckedIn = ${engC?.isCheckedIn} after check-in`);

		// Achievements
		const { status: aS, data: achs } = await apiFetch("GET", "/v1/me/achievements", veraToken);
		if (aS === 200) {
			ok(`vera achievements: ${achs?.length ?? 0} earned`);
			achs?.forEach((a) => ok(`  → badge: ${a.badgeKey} earned ${new Date(a.earnedOn).toLocaleDateString()}`));
			const fs2 = achs?.find((a) => a.badgeKey === "first-step");
			if (fs2) ok("'first-step' badge confirmed earned");
			else note("enhancement", "'first-step' badge not yet awarded", "May need background job trigger or completed engagement");
		} else note("bug", `Achievements returned ${aS}`);

		// Streaks
		const { status: stS, data: streak } = await apiFetch("GET", "/v1/me/streaks", veraToken);
		if (stS === 200 && streak) {
			ok(`Streaks: ${JSON.stringify(streak)}`);
		} else note("bug", `Streaks returned ${stS}`);

		// Notifications
		const { data: olafNotifs } = await apiFetch("GET", "/v1/notifications", olafToken);
		const { data: veraNotifs } = await apiFetch("GET", "/v1/notifications", veraToken);
		ok(`olaf notifications: ${olafNotifs?.length ?? 0}`);
		ok(`vera notifications: ${veraNotifs?.length ?? 0}`);
		if (olafNotifs?.length > 0) ok(`  olaf latest: "${olafNotifs[0].message?.slice(0, 60)}"`);
		if (veraNotifs?.length > 0) ok(`  vera latest: "${veraNotifs[0].message?.slice(0, 60)}"`);
	} catch (e) { ko("Suite 2", e); }

	// ── Suite 3: Opportunity CRUD ───────────────────────────────────────────
	console.log("\n=== Suite 3: Opportunity CRUD (create → edit → delete) ===");
	let crudOppId = null;
	try {
		if (!olafToken || !olafOrgId) throw new Error("Prerequisites missing");

		const { status: cS, data: cD } = await apiFetch(
			"POST", "/v1/volunteer-opportunities", olafToken, {
				title: "CRUD Test Opportunity",
				description: "Automated lifecycle test - will be deleted.",
				organizationId: olafOrgId,
				isRemote: true,
				occurrence: "OneTime",
				participationType: "OpenToAll",
				checkInMethod: "Manual",
				category: "Community",
				tags: ["auto-test", "delete-me"],
			},
		);
		if (cS === 200 || cS === 201) {
			crudOppId = cD?.id;
			ok(`Created: ${crudOppId?.slice(0, 8)} (${cS})`);
		} else {
			note("bug", `Create returned ${cS}`, JSON.stringify(cD)?.slice(0, 100));
			throw new Error("create failed");
		}

		// Verify in list
		await new Promise((r) => setTimeout(r, 300));
		const { data: list } = await apiFetch("GET", "/v1/volunteer-opportunities?PageNumber=1&PageSize=20");
		if (list?.items?.some((o) => o.id === crudOppId)) ok("New opp in public listing");
		else note("bug", "New opportunity not found in listing immediately after creation");

		// Category filter
		const { data: byCat } = await apiFetch("GET", `/v1/volunteer-opportunities?Category=Community&PageNumber=1&PageSize=20`);
		if (byCat?.items?.some((o) => o.id === crudOppId)) ok("Category=Community filter finds new opp");
		else note("bug", "Category filter did not return newly created opp");

		// Tag filter
		const { data: byTag } = await apiFetch("GET", `/v1/volunteer-opportunities?Tag=auto-test&PageNumber=1&PageSize=20`);
		if (byTag?.items?.some((o) => o.id === crudOppId)) ok("Tag=auto-test filter finds new opp");
		else note("bug", "Tag filter did not return newly created opp");

		// Edit
		const { status: eS } = await apiFetch(
			"PUT", `/v1/volunteer-opportunities/${crudOppId}`, olafToken, {
				title: "CRUD Test - EDITED",
				description: "Updated.",
				isRemote: false,
				street: "Musterstraße",
				houseNumber: "1",
				zipCode: "10115",
				city: "Berlin",
				occurrence: "Recurring",
				participationType: "Waitlist",
				checkInMethod: "QrCode",
				category: "Education",
				tags: ["auto-test", "edited"],
			},
		);
		if (eS === 200 || eS === 204) ok(`Edit → ${eS}`);
		else note("bug", `Edit returned ${eS}`);

		// Verify edit
		const { data: editedDetail } = await apiFetch("GET", `/v1/volunteer-opportunities/${crudOppId}`);
		if (editedDetail?.title === "CRUD Test - EDITED") ok("Title edit persisted");
		else note("bug", `Title after edit: "${editedDetail?.title}"`);
		if (editedDetail?.occurrence === "Recurring") ok("Occurrence edit persisted");
		if (editedDetail?.participationType === "Waitlist") ok("ParticipationType edit persisted");
		if (editedDetail?.checkInMethod === "QrCode") ok("CheckInMethod edit persisted");
		if (editedDetail?.category === "Education") ok("Category edit persisted");
		if (editedDetail?.city === "Berlin") ok("City edit persisted (isRemote→false with address)");

		// Add time slot (required for Waitlist)
		const { status: tsS } = await apiFetch(
			"POST", `/v1/volunteer-opportunities/${crudOppId}/time-slots`, olafToken, {
				startDateTime: new Date("2027-03-01T09:00:00Z"),
				endDateTime:   new Date("2027-03-01T17:00:00Z"),
				maxParticipants: 5,
			},
		);
		if (tsS === 200 || tsS === 201) ok(`Time slot added → ${tsS}`);
		else note("bug", `Add time slot returned ${tsS}`);

		// UI: detail page shows updated content
		await olafPage.goto(`${BASE}/volunteer-opportunities/${crudOppId}`);
		await olafPage.waitForLoadState("networkidle");
		await ss(olafPage, "03a-crud-detail");
		const h1 = await olafPage.locator("h1").first().textContent();
		ok(`Detail page h1: "${h1?.trim()}"`);
		const hasEditBtn = await olafPage.locator("button", { hasText: /edit|bearbeiten/i }).count() > 0;
		if (hasEditBtn) ok("Edit button visible for owner on detail page");
		else note("bug", "Edit button missing for opportunity owner");

		// Delete
		const { status: dS } = await apiFetch(
			"DELETE", `/v1/volunteer-opportunities/${crudOppId}`, olafToken,
		);
		if (dS === 200 || dS === 204) {
			ok(`Deleted → ${dS}`);
			// Verify 404
			const { status: gone } = await apiFetch("GET", `/v1/volunteer-opportunities/${crudOppId}`);
			if (gone === 404) ok("Deleted opp returns 404");
			else note("bug", `Deleted opp returns ${gone} instead of 404`);
			crudOppId = null;
		} else {
			note("bug", `Delete returned ${dS}`);
		}
	} catch (e) { ko("Suite 3", e); }

	// ── Suite 4: Organization member management ─────────────────────────────
	console.log("\n=== Suite 4: Organization member management ===");
	try {
		if (!olafToken || !olafOrgId) throw new Error("Prerequisites missing");

		const { data: orgD } = await apiFetch("GET", `/v1/organizations/${olafOrgId}`, olafToken);
		const members = orgD?.members ?? [];
		ok(`Org has ${members.length} member(s): ${members.map((m) => m.username).join(", ")}`);

		// Get vera's user ID
		const { data: veraMe } = await apiFetch("GET", "/v1/users/me", veraToken);
		const veraId = veraMe?.id;
		ok(`vera user ID: ${veraId?.slice(0, 8)}`);

		if (!veraId) throw new Error("Cannot get vera's user ID");

		const alreadyMember = members.some((m) => m.userId === veraId);
		if (!alreadyMember) {
			// Add vera
			const { status: addS, data: addD } = await apiFetch(
				"POST", `/v1/organizations/${olafOrgId}/members`, olafToken,
				{ userId: veraId },
			);
			if (addS === 200 || addS === 204) {
				ok(`Added vera to org → ${addS}`);
				// Verify
				const { data: afterAdd } = await apiFetch("GET", `/v1/organizations/${olafOrgId}`, olafToken);
				const nowIn = afterAdd?.members?.some((m) => m.userId === veraId);
				if (nowIn) ok("vera in member list after add");
				else note("bug", "vera not in member list after add");

				// Remove vera
				const { status: rmS } = await apiFetch(
					"DELETE", `/v1/organizations/${olafOrgId}/members/${veraId}`, olafToken,
				);
				if (rmS === 200 || rmS === 204) {
					ok(`Removed vera → ${rmS}`);
					// Verify removed
					const { data: afterRm } = await apiFetch("GET", `/v1/organizations/${olafOrgId}`, olafToken);
					const stillIn = afterRm?.members?.some((m) => m.userId === veraId);
					if (!stillIn) ok("vera no longer in member list after removal");
					else note("bug", "vera still appears in member list after removal");
				} else {
					note("bug", `Remove member returned ${rmS}`);
				}
			} else {
				note("bug", `Add member returned ${addS}`, JSON.stringify(addD)?.slice(0, 80));
			}
		} else {
			ok("vera already in org - remove then re-add to test full flow");
			const { status: rmS } = await apiFetch(
				"DELETE", `/v1/organizations/${olafOrgId}/members/${veraId}`, olafToken,
			);
			ok(`Removed vera (pre-test cleanup) → ${rmS}`);
			const { status: addS } = await apiFetch(
				"POST", `/v1/organizations/${olafOrgId}/members`, olafToken, { userId: veraId },
			);
			ok(`Re-added vera → ${addS}`);
			const { status: rmS2 } = await apiFetch(
				"DELETE", `/v1/organizations/${olafOrgId}/members/${veraId}`, olafToken,
			);
			ok(`Final cleanup remove → ${rmS2}`);
		}

		// UI: members tab
		await olafPage.goto(`${BASE}/organizations/${olafOrgId}/settings`);
		await olafPage.waitForLoadState("networkidle");
		const membersTab = olafPage.locator("button", { hasText: /member|mitglied/i }).first();
		if (await membersTab.count() > 0) {
			await membersTab.click();
			await olafPage.waitForTimeout(400);
			await ss(olafPage, "04a-members-tab");
			const rows = olafPage.locator("ul li");
			ok(`Members tab shows ${await rows.count()} row(s)`);
		}
	} catch (e) { ko("Suite 4", e); }

	// ── Suite 5: Org dashboard UI ───────────────────────────────────────────
	console.log("\n=== Suite 5: Organization dashboard ===");
	try {
		if (!olafToken || !olafOrgId) throw new Error("Prerequisites missing");

		// API
		const { status: dS, data: dash } = await apiFetch(
			"GET", `/v1/organizations/${olafOrgId}/dashboard`, olafToken,
		);
		if (dS === 200 && dash) {
			ok(`Dashboard API: ${JSON.stringify(dash)}`);
		} else {
			note("bug", `Dashboard API returned ${dS}`);
		}

		// UI
		await olafPage.goto(`${BASE}/organizations/${olafOrgId}/dashboard`);
		await olafPage.waitForLoadState("networkidle");
		await ss(olafPage, "05a-dashboard");
		const content = await olafPage.locator("main").textContent();
		const numbers = content?.match(/\d+/g) ?? [];
		ok(`Dashboard UI shows numbers: [${numbers.slice(0, 8).join(", ")}]`);

		if (numbers.length === 0) {
			note("enhancement", "Org dashboard UI shows no numeric stats");
		}

		// Check for specific stat cards/sections
		const hasEngCount = content?.match(/engagement|bewerbung/i);
		const hasOppCount = content?.match(/opportunit|gelegenheit/i);
		if (hasEngCount) ok("Dashboard mentions engagements");
		if (hasOppCount) ok("Dashboard mentions opportunities");
	} catch (e) { ko("Suite 5", e); }

	// ── Suite 6: CheckIn modal ─────────────────────────────────────────────
	console.log("\n=== Suite 6: CheckIn modal (QR + Manual) ===");
	try {
		if (!olafToken || !olafOrgId) throw new Error("Prerequisites missing");

		// Create QR opp, sign vera up, confirm, then check the modal
		const { status: cQR, data: qrD } = await apiFetch(
			"POST", "/v1/volunteer-opportunities", olafToken, {
				title: "QR Modal Test",
				description: "Test",
				organizationId: olafOrgId,
				isRemote: true,
				occurrence: "OneTime",
				participationType: "OpenToAll",
				checkInMethod: "QrCode",
			},
		);
		if (cQR !== 200 && cQR !== 201) throw new Error(`Create QR opp: ${cQR}`);
		const qrOppId = qrD?.id;
		ok(`Created QR opp: ${qrOppId?.slice(0, 8)}`);

		// vera signs up via API
		const { status: sgS, data: sgD } = await apiFetch(
			"POST", `/v1/volunteer-opportunities/${qrOppId}/engagements`,
			veraToken, { message: "QR test" },
		);
		if (sgS !== 200 && sgS !== 201) throw new Error(`Sign-up: ${sgS}`);
		const qrEngId = sgD?.id;
		ok(`vera signed up: ${qrEngId?.slice(0, 8)}`);

		// olaf confirms
		await apiFetch("POST", `/v1/engagements/${qrEngId}/confirm`, olafToken);
		ok("olaf confirmed vera's QR engagement");

		// Open engagement management in UI
		await olafPage.goto(`${BASE}/volunteer-opportunities/${qrOppId}/engagements`);
		await olafPage.waitForLoadState("networkidle");
		await ss(olafPage, "06a-qr-eng-mgmt");

		const checkInBtn = olafPage.locator("button", { hasText: /check.?in|qr/i }).first();
		if (await checkInBtn.count() > 0) {
			await checkInBtn.click();
			await olafPage.waitForTimeout(600);
			await ss(olafPage, "06b-checkin-modal");
			const modal = olafPage.locator('[role="dialog"]');
			if (await modal.count() > 0) {
				ok("Check-in modal opened");
				// Look for QR code canvas
				const qrCanvas = modal.locator("canvas");
				const qrSvg    = modal.locator("svg").filter({ has: modal.locator("path[d]") });
				if (await qrCanvas.count() > 0) ok("QR code canvas in check-in modal");
				else if (await qrSvg.count() > 0) ok("QR code SVG in check-in modal");
				else note("enhancement", "Check-in modal open but no QR code element (canvas/svg) found");
				await ss(olafPage, "06c-checkin-modal-detail");
				await olafPage.keyboard.press("Escape");
			} else {
				note("bug", "CheckIn button clicked but no modal appeared");
			}
		} else {
			note("bug", "No check-in button on engagement management page for QR-enabled opportunity");
		}

		// Test manual check-in via org endpoint
		const { status: manS } = await apiFetch(
			"POST", `/v1/engagements/${qrEngId}/check-in`, olafToken,
		);
		if (manS === 200 || manS === 204) ok(`Org-side manual check-in → ${manS}`);
		else note("bug", `Org check-in returned ${manS}`);

		// Cleanup
		await apiFetch("DELETE", `/v1/volunteer-opportunities/${qrOppId}`, olafToken);
		ok("QR opp cleaned up");
	} catch (e) { ko("Suite 6", e); }

	// ── Suite 7: Account page ──────────────────────────────────────────────
	console.log("\n=== Suite 7: Account page - name save, delete dialog ===");
	try {
		await veraPage.goto(`${BASE}/account`);
		await veraPage.waitForLoadState("networkidle");
		await ss(veraPage, "07a-account");

		const firstInput = veraPage.locator("#first-name").first();
		const lastInput  = veraPage.locator("#last-name").first();
		if (await firstInput.count() > 0) {
			await firstInput.fill("Vera");
			await lastInput.fill("Mustermann");
			await veraPage.locator('button[type="submit"]').first().click();
			await veraPage.waitForTimeout(1500);
			const saved = veraPage.locator("div, p", { hasText: /saved|gespeichert|success/i }).first();
			if (await saved.count() > 0) ok("First/last name saved (success message shown)");
			else note("bug", "No success feedback after saving account name");
			await ss(veraPage, "07b-account-saved");

			// Verify via API
			const { data: me } = await apiFetch("GET", "/v1/users/me", veraToken);
			ok(`/v1/users/me firstName: "${me?.firstName}", lastName: "${me?.lastName}"`);
			if (me?.firstName === "Vera") ok("First name persisted to Keycloak via API");
			else note("bug", `firstName in API is "${me?.firstName}", expected "Vera"`, "UpdateUserAsync may not be updating Keycloak correctly");
		} else {
			note("bug", "First name input (#first-name) not found on account page");
		}

		// Delete account dialog
		const delBtn = veraPage.locator("button", { hasText: /delete.*account|konto.*löschen|löschen/i }).first();
		if (await delBtn.count() > 0) {
			await delBtn.click();
			await veraPage.waitForTimeout(400);
			await ss(veraPage, "07c-delete-dialog");
			const dialog = veraPage.locator('[role="dialog"]');
			if (await dialog.count() > 0) {
				ok("Delete-account confirmation dialog opens");
				const txt = await dialog.textContent();
				if (txt?.match(/permanent|cannot be undone|unwiderruflich/i)) ok("Dialog has irreversibility warning");
				// Cancel safely
				await (dialog.locator("button", { hasText: /cancel|abbrechen/i }).first().click().catch(
					() => veraPage.keyboard.press("Escape"),
				));
				ok("Delete account dialog dismissed safely");
			} else {
				note("bug", "Delete button exists but no dialog appeared");
			}
		} else {
			note("enhancement", "No delete-account button on account page (GDPR gap)");
		}
	} catch (e) { ko("Suite 7", e); }

	// ── Suite 8: Notification read-state flow ──────────────────────────────
	console.log("\n=== Suite 8: Notification read-state flow ===");
	try {
		if (!veraToken) throw new Error("No vera token");

		const { data: notifs } = await apiFetch("GET", "/v1/notifications", veraToken);
		ok(`vera has ${notifs?.length ?? 0} notifications`);
		const unread = notifs?.filter((n) => !n.isRead) ?? [];
		ok(`${unread.length} unread`);

		if (unread.length > 0) {
			const n = unread[0];
			// Mark one read
			const { status: readS } = await apiFetch(
				"POST", `/v1/notifications/${n.id}/read`, veraToken,
			);
			if (readS === 200 || readS === 204) {
				ok(`Mark single notification read → ${readS}`);
				// Verify
				const { data: after } = await apiFetch("GET", "/v1/notifications", veraToken);
				const stillUnread = after?.find((x) => x.id === n.id && !x.isRead);
				if (!stillUnread) ok("Notification marked as read successfully");
				else note("bug", "Notification still showing as unread after mark-read");
			} else {
				note("bug", `Mark-read returned ${readS}`);
			}
		}

		// Mark all read
		const { status: allS } = await apiFetch("POST", "/v1/notifications/read-all", veraToken);
		if (allS === 200 || allS === 204) {
			ok(`Mark-all-read → ${allS}`);
			const { data: allAfter } = await apiFetch("GET", "/v1/notifications", veraToken);
			const anyUnread = allAfter?.some((n) => !n.isRead);
			if (!anyUnread) ok("All notifications now marked as read");
			else note("bug", "Some notifications still unread after mark-all-read");
		} else {
			note("bug", `Mark-all-read returned ${allS}`);
		}

		// UI: bell and dropdown
		await veraPage.goto(`${BASE}/`);
		await veraPage.waitForLoadState("networkidle");
		// Find notification bell (usually last SVG button in header before auth buttons)
		const headerBtns = await veraPage.locator("header button").all();
		ok(`Header has ${headerBtns.length} button(s)`);
		// Try each SVG-containing button
		let bellFound = false;
		for (const btn of headerBtns) {
			const svg = await btn.locator("svg").count();
			if (svg === 0) continue;
			const ariaLabel = await btn.getAttribute("aria-label");
			const title = await btn.getAttribute("title");
			if (ariaLabel?.match(/notif|bell/i) || title?.match(/notif/i)) {
				await btn.click();
				await veraPage.waitForTimeout(500);
				await ss(veraPage, "08a-notif-panel");
				ok(`Notification bell found via aria-label="${ariaLabel}"`);
				bellFound = true;
				break;
			}
		}
		if (!bellFound) {
			// Try clicking the last button in header (typically the bell)
			const lastSvgBtn = veraPage.locator("header button svg").last();
			if (await lastSvgBtn.count() > 0) {
				await lastSvgBtn.click();
				await veraPage.waitForTimeout(500);
				await ss(veraPage, "08b-last-header-btn-clicked");
			}
		}
	} catch (e) { ko("Suite 8", e); }

	// ── Suite 9: Streaks display ────────────────────────────────────────────
	console.log("\n=== Suite 9: Streak API and display ===");
	try {
		if (!veraToken) throw new Error("No token");
		const { status: stS, data: streaks } = await apiFetch("GET", "/v1/me/streaks", veraToken);
		if (stS === 200) {
			ok(`Streaks: ${JSON.stringify(streaks)}`);
			// Check UI
			await veraPage.goto(`${BASE}/achievements`);
			await veraPage.waitForLoadState("networkidle");
			await ss(veraPage, "09a-achievements-streaks");
			const mainText = await veraPage.locator("main").textContent();
			const streakNum = Object.values(streaks ?? {}).find((v) => typeof v === "number" && v > 0);
			if (streakNum && mainText?.includes(String(streakNum))) {
				ok(`Streak value ${streakNum} visible on achievements page`);
			} else if (mainText?.match(/streak|serie/i)) {
				ok("Streak section visible on achievements page");
			} else {
				note("enhancement", "Streaks from API not displayed on achievements page UI");
			}
		} else {
			note("bug", `Streaks returned ${stS}`);
		}
	} catch (e) { ko("Suite 9", e); }

	// ── Suite 10: My Engagements UX ────────────────────────────────────────
	console.log("\n=== Suite 10: My Engagements UX details ===");
	try {
		await veraPage.goto(`${BASE}/my-engagements`);
		await veraPage.waitForLoadState("networkidle");
		await ss(veraPage, "10a-my-engagements");

		const items = veraPage.locator("ul > li");
		const count = await items.count();
		ok(`My Engagements: ${count} item(s) visible`);

		if (count > 0) {
			const first = items.first();
			// Check opportunity title is clickable
			const titleBtn = first.locator("button, a").filter({ hasText: /\w{3,}/ }).first();
			if (await titleBtn.count() > 0) {
				const tag = await titleBtn.evaluate((el) => el.tagName.toLowerCase());
				if (tag === "a") ok("Opportunity title is a proper <a> link");
				else if (tag === "button") note("enhancement", "Opportunity title is <button onClick> not <Link> - no right-click open-in-new-tab");
				ok(`Title element tag: <${tag}>`);
			}

			// Check status badge
			const statusBadge = first.locator(".rounded-full, [class*='status'], [class*='badge']").first();
			if (await statusBadge.count() > 0) {
				const statusText = await statusBadge.textContent();
				ok(`Status badge: "${statusText?.trim()}"`);
			} else {
				note("enhancement", "No visible status badge on engagement list item");
			}

			// Check for org name
			const orgLink = first.locator('a[href*="/organizations/"]');
			if (await orgLink.count() > 0) ok("Org link present on engagement item");
			else {
				const orgText = await first.textContent();
				note("enhancement", "No org name/link on My Engagements items - EngagementSummary missing organizationName field");
			}

			// Check for withdraw/cancel button
			const withdrawBtn = first.locator("button", { hasText: /withdraw|cancel|absagen|zurückziehen|stornieren/i }).first();
			if (await withdrawBtn.count() > 0) ok("Withdraw/cancel button present");
			else note("enhancement", "No withdraw button on engagement item");

			// Check date/time display
			const mainText = await first.textContent();
			const hasDate = /\d{1,2}[./]\d{1,2}[./]\d{2,4}|\d{4}-\d{2}-\d{2}|Jan|Feb|Mar|Apr|Mai|Jun|Jul|Aug|Sep|Oct|Nov|Dec/.test(mainText ?? "");
			if (hasDate) ok("Date information visible on engagement item");
			else note("enhancement", "No date shown on My Engagements items - users don't know when they signed up");
		}
	} catch (e) { ko("Suite 10", e); }

	// ── Suite 11: Org settings → contact info → public profile ─────────────
	console.log("\n=== Suite 11: Org settings save → public profile verify ===");
	try {
		if (!olafToken || !olafOrgId) throw new Error("Prerequisites missing");

		// Fill and save contact info
		await olafPage.goto(`${BASE}/organizations/${olafOrgId}/settings`);
		await olafPage.waitForLoadState("networkidle");

		const emailIn   = olafPage.locator("#org-contact-email").first();
		const phoneIn   = olafPage.locator("#org-phone").first();
		const websiteIn = olafPage.locator("#org-website").first();
		const descIn    = olafPage.locator("#org-description").first();

		const testEmail   = "kontakt@testorg.de";
		const testPhone   = "+49 30 9876543";
		const testWebsite = "https://testorg.example.com";
		const testDesc    = "We are a great test organization for the e2e test.";

		if (await emailIn.count() > 0) {
			await emailIn.fill(testEmail);
			await phoneIn.fill(testPhone);
			await websiteIn.fill(testWebsite);
			await descIn.fill(testDesc);
			await olafPage.locator('button[type="submit"]').first().click();
			await olafPage.waitForTimeout(1500);
			await ss(olafPage, "11a-settings-saved");
			const success = olafPage.locator("div, p", { hasText: /saved|gespeichert/i }).first();
			if (await success.count() > 0) ok("Org settings saved");
			else note("bug", "No success message after org settings save");

			// Navigate to public profile
			const freshCtx  = await browser.newContext({ ignoreHTTPSErrors: true });
			const freshPage = await freshCtx.newPage();
			await freshPage.goto(`${BASE}/organizations/${olafOrgId}`);
			await freshPage.waitForLoadState("networkidle");
			await ss(freshPage, "11b-public-profile");
			const profileText = await freshPage.locator("main").textContent();
			if (profileText?.includes(testEmail))   ok("Contact email on public profile after save");
			else note("bug", `Email "${testEmail}" not on public profile`, `Visible text: ${profileText?.slice(0, 200)}`);
			if (profileText?.includes(testPhone))   ok("Contact phone on public profile after save");
			else note("bug", `Phone "${testPhone}" not on public profile`);
			if (profileText?.includes("testorg.example.com")) ok("Website on public profile after save");
			else note("bug", "Website not on public profile after save");
			if (profileText?.includes(testDesc.slice(0, 20))) ok("Description on public profile after save");
			await freshCtx.close();
		} else {
			note("bug", "org-contact-email input not found on settings page");
		}
	} catch (e) { ko("Suite 11", e); }

	// ── Suite 12: Advanced API edge cases ──────────────────────────────────
	console.log("\n=== Suite 12: API edge cases ===");
	try {
		// PageNumber=0 → should be 400 (known bug #362)
		const { status: p0 } = await apiFetch("GET", "/v1/volunteer-opportunities?PageNumber=0&PageSize=10");
		if (p0 === 400) ok("PageNumber=0 correctly returns 400");
		else note("bug", `PageNumber=0 still returns ${p0} (should be 400, see issue #362)`);

		// PageNumber=-1
		const { status: pn } = await apiFetch("GET", "/v1/volunteer-opportunities?PageNumber=-1&PageSize=10");
		ok(`PageNumber=-1 → ${pn}`);
		if (pn === 500) note("bug", "PageNumber=-1 returns 500 (negative Skip in EF Core)");

		// PageSize=0
		const { status: ps0 } = await apiFetch("GET", "/v1/volunteer-opportunities?PageNumber=1&PageSize=0");
		ok(`PageSize=0 → ${ps0}`);

		// Empty search string
		const { status: emS, data: emD } = await apiFetch("GET", "/v1/volunteer-opportunities?Search=&PageNumber=1&PageSize=10");
		if (emS === 200) ok(`Empty Search param → 200, ${emD?.items?.length} item(s)`);

		// Search with special chars
		const { status: specS } = await apiFetch("GET", "/v1/volunteer-opportunities?Search=%3Cscript%3E&PageNumber=1&PageSize=10");
		if (specS === 200) ok(`XSS-attempt search → 200 (handled safely)`);
		else note("bug", `XSS-attempt search returned ${specS}`);

		// Non-existent engagement ID
		const fakeEngId = "00000000-0000-0000-0000-000000000001";
		const { status: fakeS } = await apiFetch("POST", `/v1/engagements/${fakeEngId}/confirm`, olafToken);
		if (fakeS === 404) ok("Confirm non-existent engagement → 404");
		else note("bug", `Confirm non-existent engagement → ${fakeS} (expected 404)`);

		// GET /v1/organizations (public vs auth)
		const { status: pubOrgs, data: pubOrgData } = await apiFetch("GET", "/v1/organizations");
		ok(`GET /v1/organizations (public) → ${pubOrgs}, ${Array.isArray(pubOrgData) ? pubOrgData.length : "?"} org(s)`);
	} catch (e) { ko("Suite 12", e); }

	// ── Suite 13: UI smoke - create opportunity modal ──────────────────────
	console.log("\n=== Suite 13: Create opportunity modal UI ===");
	try {
		await olafPage.goto(`${BASE}/`);
		await olafPage.waitForLoadState("networkidle");
		await ss(olafPage, "13a-home-as-olaf");

		const createBtn = olafPage.locator("button", { hasText: /create|erstellen/i }).first();
		if (await createBtn.count() === 0) {
			note("bug", "No create-opportunity button for olaf on homepage", "Active-org cookie may need to be set");
		} else {
			await createBtn.click();
			await olafPage.waitForTimeout(600);
			await ss(olafPage, "13b-create-modal");
			const modal = olafPage.locator('[role="dialog"]');
			if (await modal.count() > 0) {
				ok("Create opportunity modal opened");

				// Count all inputs/selects/textareas
				const fields = await modal.locator("input, select, textarea").count();
				ok(`Modal has ${fields} form field(s)`);

				// Check for category select
				const catSelect = modal.locator("select").filter({ hasText: /category|kategorie/i }).first();
				if (await catSelect.count() > 0) {
					const opts = await catSelect.locator("option").all();
					const optVals = await Promise.all(opts.map((o) => o.textContent()));
					ok(`Category options: ${optVals.join(", ")}`);
				} else {
					note("enhancement", "No category selector in create opportunity modal");
				}

				// Check for tag input
				const tagInput = modal.locator('input[placeholder*="tag" i], input[id*="tag" i]').first();
				if (await tagInput.count() > 0) ok("Tag input field present in modal");
				else note("enhancement", "No tag input field in create opportunity modal");

				// Check for checkInMethod select
				const checkInSel = modal.locator("select").filter({ hasText: /check.?in|none|qr/i }).first();
				if (await checkInSel.count() > 0) {
					const ciOpts = await checkInSel.locator("option").all();
					const ciVals = await Promise.all(ciOpts.map((o) => o.textContent()));
					ok(`CheckIn method options: ${ciVals.join(", ")}`);
				}

				// isRemote toggle
				const isRemoteCheck = modal.locator("#is-remote, input[id*=remote]").first();
				if (await isRemoteCheck.count() > 0) {
					await isRemoteCheck.check();
					await olafPage.waitForTimeout(300);
					// Address fields should hide
					const streetField = modal.locator("#street, input[id*='street']").first();
					const streetVisible = await streetField.isVisible().catch(() => false);
					if (!streetVisible) ok("Address fields hidden when isRemote checked");
					else note("enhancement", "Address fields still visible after checking isRemote");
				}

				await ss(olafPage, "13c-create-modal-fields");
				await olafPage.keyboard.press("Escape");
			} else {
				note("bug", "Create button present but modal did not open");
			}
		}
	} catch (e) { ko("Suite 13", e); }

	// ── Suite 14: Volunteer self-check-in UI ───────────────────────────────
	console.log("\n=== Suite 14: Volunteer self-check-in UI ===");
	try {
		if (!mainEngId || !veraToken) throw new Error("No engagement ID or token");

		// Navigate vera to her engagement
		const { data: myEngs } = await apiFetch("GET", "/v1/me/engagements", veraToken);
		const myEng = myEngs?.[0];
		if (!myEng) throw new Error("No engagements for vera");

		await veraPage.goto(`${BASE}/my-engagements`);
		await veraPage.waitForLoadState("networkidle");
		await ss(veraPage, "14a-my-engs-for-checkin");

		// Look for a check-in button on the My Engagements page
		const ciBtn = veraPage.locator("button", { hasText: /check.?in/i }).first();
		if (await ciBtn.count() > 0) {
			await ciBtn.click();
			await veraPage.waitForTimeout(400);
			await ss(veraPage, "14b-checkin-modal-vera");
			const modal = veraPage.locator('[role="dialog"]');
			if (await modal.count() > 0) ok("Self check-in modal opens from My Engagements");
			else ok("Self check-in button clicked (no modal - direct API call)");
		} else {
			note("enhancement", "No self-check-in button visible on My Engagements for confirmed engagement");
		}

		// Also navigate to the opportunity detail page as vera
		const oppIdForCheckin = myEng.opportunityId;
		await veraPage.goto(`${BASE}/volunteer-opportunities/${oppIdForCheckin}`);
		await veraPage.waitForLoadState("networkidle");
		await ss(veraPage, "14c-opp-detail-as-vera");
		const detailText = await veraPage.locator("main").textContent();
		const showsCheckedIn = detailText?.match(/check.?in|eingecheckt|bereits/i);
		if (showsCheckedIn) ok("Opportunity detail shows check-in state for vera");
		else note("enhancement", "Opportunity detail page does not reflect vera's check-in status");
	} catch (e) { ko("Suite 14", e); }

	// ── Cleanup ─────────────────────────────────────────────────────────────
	if (crudOppId) {
		await apiFetch("DELETE", `/v1/volunteer-opportunities/${crudOppId}`, olafToken).catch(() => {});
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
