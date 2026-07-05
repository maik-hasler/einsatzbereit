/**
 * Smoke test for #572: per-engagement "Add to Calendar" with direct
 * provider quick-add (Google Calendar, Outlook.com, Apple/webcal, .ics).
 *
 * Verifies against the live staging environment:
 * - A Confirmed engagement with a time slot shows an "Add to Calendar"
 *   action in "My Engagements".
 * - Opening it reveals Google Calendar, Outlook.com, Apple Calendar, and
 *   Download .ics links, each pointing at the right data.
 * - GET /v1/engagements/{id}/calendar returns a single-event .ics scoped
 *   to that engagement's time slot, with a unique filename (no more of
 *   the old hardcoded "opportunity.ics" that caused Chrome's "download
 *   again?" prompt).
 *
 * Setup (org/opportunity/time slot/sign-up/confirm) is done directly
 * against the API as olaf (organizer), since that flow is already covered
 * by other smoke tests/VisualTests - this script focuses on verifying the
 * calendar feature itself.
 *
 * Run: node scripts/smoke-test-572-calendar.mjs
 */

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";

function getAccessToken(page) {
	return page.evaluate(() => {
		for (const k of Object.keys(localStorage)) {
			if (k.includes("oidc")) {
				return JSON.parse(localStorage.getItem(k)).access_token;
			}
		}
		return null;
	});
}

async function main() {
	const apiRes = await fetch(`${API}/health`);
	if (!apiRes.ok) throw new Error(`Health check failed: ${apiRes.status}`);
	console.log("OK  API health check passed");

	const { browser, page } = await launchLiveBrowser();

	try {
		await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
		const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
		if ((await signInBtn.count()) > 0) {
			await signInBtn.first().click();
			await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
			await loginKeycloak(page, "olaf", "olaf123");
		}
		await page.waitForSelector("main", { timeout: 10000 });
		console.log("OK  Logged in as olaf");

		const token = await getAccessToken(page);
		if (!token) throw new Error("Could not read access token from localStorage");
		const authed = (path, init = {}) =>
			fetch(`${API}${path}`, {
				...init,
				headers: {
					"Content-Type": "application/json",
					Authorization: `Bearer ${token}`,
					...init.headers,
				},
			});

		// --- Create a fresh org + Waitlist opportunity + time slot, then sign
		//     up and self-confirm as olaf (organizer), so the whole setup runs
		//     under one account. ---
		const orgName = `Smoke572 ${Date.now()}`;
		const orgRes = await authed("/v1/organizations", {
			method: "POST",
			body: JSON.stringify({ name: orgName }),
		});
		if (!orgRes.ok) throw new Error(`Create org failed: ${orgRes.status} ${await orgRes.text()}`);
		const org = await orgRes.json();
		const orgId = org.id?.value ?? org.id;
		console.log(`OK  Created organization "${orgName}" (${orgId})`);

		const oppTitle = `Smoke572 Calendar Test ${Date.now()}`;
		const oppRes = await authed("/v1/volunteer-opportunities", {
			method: "POST",
			body: JSON.stringify({
				title: oppTitle,
				description: "Created by smoke-test-572-calendar.mjs",
				organizationId: orgId,
				isRemote: true,
				occurrence: "OneTime",
				participationType: "Waitlist",
				checkInMethod: "None",
				isDraft: true,
			}),
		});
		if (!oppRes.ok) throw new Error(`Create opportunity failed: ${oppRes.status} ${await oppRes.text()}`);
		const opp = await oppRes.json();
		console.log(`OK  Created opportunity "${oppTitle}" (${opp.id})`);

		const start = new Date(Date.now() + 3 * 24 * 60 * 60 * 1000);
		const end = new Date(start.getTime() + 2 * 60 * 60 * 1000);
		const slotRes = await authed(`/v1/volunteer-opportunities/${opp.id}/time-slots`, {
			method: "POST",
			body: JSON.stringify({
				startDateTime: start.toISOString(),
				endDateTime: end.toISOString(),
				maxParticipants: 5,
				recurrenceCount: 1,
			}),
		});
		if (!slotRes.ok) throw new Error(`Create time slot failed: ${slotRes.status} ${await slotRes.text()}`);
		const [slot] = await slotRes.json();
		console.log(`OK  Created time slot ${slot.id} (${start.toISOString()} - ${end.toISOString()})`);

		const publishRes = await authed(`/v1/volunteer-opportunities/${opp.id}/publish`, { method: "POST" });
		if (!publishRes.ok) throw new Error(`Publish failed: ${publishRes.status} ${await publishRes.text()}`);
		console.log("OK  Published opportunity");

		const engagementRes = await authed(`/v1/volunteer-opportunities/${opp.id}/engagements`, {
			method: "POST",
			body: JSON.stringify({ type: "Waitlist", timeSlotId: slot.id, message: null }),
		});
		if (!engagementRes.ok) throw new Error(`Sign-up failed: ${engagementRes.status} ${await engagementRes.text()}`);
		const engagement = await engagementRes.json();
		console.log(`OK  Signed up (engagement ${engagement.id})`);

		const confirmRes = await authed(`/v1/engagements/${engagement.id}/confirm`, { method: "POST" });
		if (!confirmRes.ok) throw new Error(`Confirm failed: ${confirmRes.status} ${await confirmRes.text()}`);
		console.log("OK  Confirmed engagement");

		// --- UI: "My Engagements" shows the "Add to Calendar" action ---
		await page.goto(`${BASE}/profile?tab=engagements`, { waitUntil: "networkidle" });
		const row = page.locator("li", { hasText: oppTitle });
		await row.waitFor({ state: "visible", timeout: 15000 });
		console.log("OK  Confirmed engagement visible in My Engagements");

		const calendarBtn = row.getByRole("button", { name: /add to calendar/i });
		await calendarBtn.waitFor({ state: "visible", timeout: 10000 });
		await calendarBtn.click();

		const googleLink = row.getByRole("link", { name: /google calendar/i });
		await googleLink.waitFor({ state: "visible", timeout: 5000 });
		const googleHref = await googleLink.getAttribute("href");
		const googleUrl = new URL(googleHref);
		if (googleUrl.hostname !== "calendar.google.com" || googleUrl.searchParams.get("text") !== oppTitle) {
			throw new Error(`Google Calendar link missing/incorrect: ${googleHref}`);
		}
		console.log("OK  Google Calendar quick-add link has the right title");

		const outlookLink = row.getByRole("link", { name: /outlook\.com/i });
		const outlookHref = await outlookLink.getAttribute("href");
		const outlookUrl = new URL(outlookHref);
		if (outlookUrl.hostname !== "outlook.live.com" || outlookUrl.searchParams.get("subject") !== oppTitle) {
			throw new Error(`Outlook link missing/incorrect: ${outlookHref}`);
		}
		console.log("OK  Outlook.com quick-add link has the right subject");

		const appleLink = row.getByRole("link", { name: /apple calendar/i });
		const appleHref = await appleLink.getAttribute("href");
		if (!appleHref.startsWith("webcal://") || !appleHref.includes(`/engagements/${engagement.id}/calendar`)) {
			throw new Error(`Apple Calendar webcal link missing/incorrect: ${appleHref}`);
		}
		console.log("OK  Apple Calendar webcal:// link points at the scoped engagement feed");

		const downloadLink = row.getByRole("link", { name: /download \.ics/i });
		const downloadHref = await downloadLink.getAttribute("href");
		if (downloadHref !== `${API}/v1/engagements/${engagement.id}/calendar`) {
			throw new Error(`Download .ics href unexpected: ${downloadHref}`);
		}

		// --- Backend: the scoped .ics endpoint returns a valid single-event feed ---
		const icsRes = await fetch(downloadHref);
		if (!icsRes.ok) throw new Error(`.ics download failed: ${icsRes.status}`);
		const contentType = icsRes.headers.get("content-type") ?? "";
		if (!contentType.includes("text/calendar")) {
			throw new Error(`Unexpected content-type: ${contentType}`);
		}
		const disposition = icsRes.headers.get("content-disposition") ?? "";
		if (!disposition.includes(`engagement-${engagement.id}.ics`)) {
			throw new Error(`Content-Disposition filename not scoped to the engagement: ${disposition}`);
		}
		const icsBody = await icsRes.text();
		if (!icsBody.includes("BEGIN:VCALENDAR") || !icsBody.includes("BEGIN:VEVENT")) {
			throw new Error("ICS body missing VCALENDAR/VEVENT");
		}
		if ((icsBody.match(/BEGIN:VEVENT/g) ?? []).length !== 1) {
			throw new Error("ICS body should contain exactly one event (scoped to this engagement's slot)");
		}
		if (!icsBody.includes(`UID:${engagement.id}@einsatzbereit`)) {
			throw new Error("ICS UID is not scoped to the engagement");
		}
		console.log("OK  .ics download is a single-event feed with a unique, engagement-scoped filename");

		// --- The old opportunity-level calendar link is gone from the detail page ---
		await page.goto(`${BASE}/volunteer-opportunities/${opp.id}`, { waitUntil: "networkidle" });
		const oldCalendarLink = page.locator(`a[href*="/volunteer-opportunities/${opp.id}/calendar"]`);
		if ((await oldCalendarLink.count()) !== 0) {
			throw new Error("Opportunity detail page should no longer expose an opportunity-level calendar link");
		}
		console.log("OK  Opportunity detail page no longer shows the old ambiguous calendar link");

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
