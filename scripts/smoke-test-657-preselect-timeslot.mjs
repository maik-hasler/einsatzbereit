// Smoke test for #657 (PR #658): SignUpModal left the "Select time slot"
// dropdown on its empty placeholder even when a Waitlist opportunity had
// exactly one (non-full) time slot, forcing an avoidable extra click before
// "Sign up" was enabled. Verifies that for such an opportunity, opening the
// sign-up modal already shows the single slot selected in the dropdown and
// the "Sign up" button is immediately enabled - without submitting an
// engagement, so no engagement/notification data is created (see #630).
// Creates a throwaway organization + opportunity and deletes both at the
// end so this script doesn't leave junk data on the live site.
// Run: node scripts/smoke-test-657-preselect-timeslot.mjs

import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const KEYCLOAK = "https://login.maik-hasler.de";
const CLIENT_ID = "frontend";
const REALM = "einsatzbereit";

async function getToken(username, password) {
	const res = await fetch(
		`${KEYCLOAK}/realms/${REALM}/protocol/openid-connect/token`,
		{
			method: "POST",
			headers: { "Content-Type": "application/x-www-form-urlencoded" },
			body: new URLSearchParams({
				grant_type: "password",
				client_id: CLIENT_ID,
				username,
				password,
				scope: "openid",
			}),
		},
	);
	if (!res.ok) throw new Error(`Token request failed: ${res.status}`);
	const data = await res.json();
	if (!data.access_token) throw new Error("No access_token in response");
	return data.access_token;
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok)
		throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const token = await getToken("olaf", "olaf123");
	const authHeaders = {
		Authorization: `Bearer ${token}`,
		"Content-Type": "application/json",
	};
	console.log("OK  Got access token for olaf (organisator)");

	const orgRes = await fetch(`${API}/v1/organizations`, {
		method: "POST",
		headers: authHeaders,
		body: JSON.stringify({ name: `Smoke657 Org ${Date.now()}` }),
	});
	if (!orgRes.ok)
		throw new Error(
			`Create organization failed: ${orgRes.status} ${await orgRes.text()}`,
		);
	const org = await orgRes.json();
	const orgId = org.id.value;
	console.log(`OK  Created throwaway organization ${orgId}`);

	let opportunityId;
	try {
		const draftRes = await fetch(`${API}/v1/volunteer-opportunities`, {
			method: "POST",
			headers: authHeaders,
			body: JSON.stringify({
				title: `Smoke657 Single Slot ${Date.now()}`,
				description: "Automated smoke test for the #657 pre-select fix.",
				organizationId: orgId,
				isRemote: true,
				occurrence: "OneTime",
				participationType: "Waitlist",
				checkInMethod: "None",
				isDraft: true,
			}),
		});
		if (!draftRes.ok)
			throw new Error(
				`Create draft failed: ${draftRes.status} ${await draftRes.text()}`,
			);
		const draft = await draftRes.json();
		opportunityId = draft.id;
		console.log(`OK  Created draft opportunity ${opportunityId}`);

		const start = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000);
		const end = new Date(start.getTime() + 2 * 60 * 60 * 1000);
		const slotRes = await fetch(
			`${API}/v1/volunteer-opportunities/${opportunityId}/time-slots`,
			{
				method: "POST",
				headers: authHeaders,
				body: JSON.stringify({
					startDateTime: start.toISOString(),
					endDateTime: end.toISOString(),
					maxParticipants: 5,
					recurrenceCount: 1,
				}),
			},
		);
		if (!slotRes.ok)
			throw new Error(
				`Time slot create failed: ${slotRes.status} ${await slotRes.text()}`,
			);
		console.log("OK  Added exactly one (non-full) time slot to the draft");

		const publishRes = await fetch(
			`${API}/v1/volunteer-opportunities/${opportunityId}/publish`,
			{ method: "POST", headers: authHeaders },
		);
		if (!publishRes.ok)
			throw new Error(
				`Publish failed: ${publishRes.status} ${await publishRes.text()}`,
			);
		console.log("OK  Published the single-slot Waitlist opportunity");

		const { browser, page } = await launchLiveBrowser();
		try {
			await page.goto(BASE, { waitUntil: "networkidle" });
			await page.click("text=/sign in|anmelden/i");
			await page.waitForURL(/\/realms\//, { timeout: 30000 });
			await loginKeycloak(page, "vera", "vera123");

			await page.goto(`${BASE}/volunteer-opportunities/${opportunityId}`, {
				waitUntil: "networkidle",
			});

			await page.getByRole("button", { name: "Select a slot" }).click();

			const dialog = page.getByRole("dialog", { name: "Select a slot" });
			await dialog.waitFor({ state: "visible", timeout: 15000 });

			const combobox = dialog.getByRole("combobox", {
				name: "Select time slot",
			});
			const comboboxText = (await combobox.innerText()).trim();
			if (comboboxText === "Please select…" || comboboxText === "") {
				throw new Error(
					`Expected the time slot dropdown to be pre-selected, but it still shows the empty placeholder ("${comboboxText}")`,
				);
			}
			console.log(
				`OK  Time slot dropdown is pre-selected ("${comboboxText}") instead of the empty placeholder`,
			);

			const signUpButton = dialog.getByRole("button", { name: "Sign up" });
			const isDisabled = await signUpButton.isDisabled();
			if (isDisabled) {
				throw new Error(
					'Expected "Sign up" to be immediately enabled with the slot pre-selected, but it is disabled',
				);
			}
			console.log(
				'OK  "Sign up" is immediately enabled without any extra selection step',
			);

			// Close without submitting - no engagement/notification data needed for this check.
			await dialog.getByRole("button", { name: "Cancel" }).click();
			await dialog.waitFor({ state: "hidden", timeout: 15000 });
			console.log("OK  Closed the sign-up modal without submitting");
		} finally {
			await browser.close();
		}
	} finally {
		if (opportunityId) {
			await fetch(`${API}/v1/volunteer-opportunities/${opportunityId}`, {
				method: "DELETE",
				headers: authHeaders,
			});
		}
		await fetch(`${API}/v1/organizations/${orgId}`, {
			method: "DELETE",
			headers: authHeaders,
		});
		console.log("OK  Cleaned up throwaway opportunity and organization");
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
