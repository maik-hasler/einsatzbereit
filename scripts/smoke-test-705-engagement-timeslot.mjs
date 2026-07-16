/**
 * Smoke test for #705: "My Profile -> Activity" engagement cards only ever
 * showed the "Signed up: <date>" line, never the opportunity's own
 * scheduled time slot - forcing a volunteer to open the opportunity's own
 * detail page just to see when to show up for a "Scheduled slots"
 * opportunity.
 *
 * Verifies against the live staging environment:
 * - A Confirmed engagement with a time slot shows a "Scheduled: <range>"
 *   line on its "My Engagements" card, distinct from the existing
 *   "Signed up: <date>" line.
 *
 * Setup (org/opportunity/time slot/sign-up/confirm) is done directly
 * against the API as olaf (organizer), mirroring smoke-test-572-calendar.mjs.
 *
 * Run: node scripts/smoke-test-705-engagement-timeslot.mjs
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

		const orgName = `Smoke705 ${Date.now()}`;
		const orgRes = await authed("/v1/organizations", {
			method: "POST",
			body: JSON.stringify({ name: orgName }),
		});
		if (!orgRes.ok) throw new Error(`Create org failed: ${orgRes.status} ${await orgRes.text()}`);
		const org = await orgRes.json();
		const orgId = org.id?.value ?? org.id;
		console.log(`OK  Created organization "${orgName}" (${orgId})`);

		const oppTitle = `Smoke705 Scheduled Slot Test ${Date.now()}`;
		const oppRes = await authed("/v1/volunteer-opportunities", {
			method: "POST",
			body: JSON.stringify({
				title: oppTitle,
				description: "Created by smoke-test-705-engagement-timeslot.mjs",
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

		// --- UI: "My Engagements" shows the new "Scheduled:" time slot line ---
		await page.goto(`${BASE}/profile?tab=engagements`, { waitUntil: "networkidle" });
		const row = page.locator("li", { hasText: oppTitle });
		await row.waitFor({ state: "visible", timeout: 15000 });
		console.log("OK  Confirmed engagement visible in My Engagements");

		const scheduledText = await row.getByText(/^Scheduled:/).textContent();
		if (!scheduledText) {
			throw new Error('Engagement card is missing the "Scheduled: <range>" line (#705 not fixed)');
		}
		console.log(`OK  Engagement card shows the opportunity's own scheduled time slot: "${scheduledText.trim()}"`);

		const signedUpCount = await row.getByText(/^Signed up:/).count();
		if (signedUpCount === 0) {
			throw new Error('Engagement card unexpectedly lost the existing "Signed up: <date>" line');
		}
		console.log('OK  Existing "Signed up:" line is still present alongside the new "Scheduled:" line');

		console.log("\nALL CHECKS PASSED");
	} finally {
		await browser.close();
	}
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
