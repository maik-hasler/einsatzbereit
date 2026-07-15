// Smoke test for #695:
//
// The own-profile page (/profile) was split into 4 separate tabs: Profile,
// Engagements, Achievements, Invitations. Consolidated down to 2: "Profile"
// (profile fields + achievements merged in below, matching the combined
// layout /users/:userId already uses) and "Activity" (engagements + an
// open-invitations banner above the list). Old ?tab=engagements/
// achievements/invitations deep links (used by the /my-engagements and
// /achievements redirects) must keep resolving to the right merged tab.
// The achievements share URL now points at /users/:userId instead of
// /users/:userId/achievements.
//
// Run: node scripts/smoke-test-695-profile-tabs.mjs

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

async function getUserId(token) {
	const res = await fetch(
		`${KEYCLOAK}/realms/${REALM}/protocol/openid-connect/userinfo`,
		{ headers: { Authorization: `Bearer ${token}` } },
	);
	if (!res.ok) throw new Error(`Userinfo request failed: ${res.status}`);
	const data = await res.json();
	return data.sub;
}

async function createOrganization(token, name) {
	const res = await fetch(`${API}/v1/organizations`, {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify({ name }),
	});
	if (!res.ok)
		throw new Error(
			`Create organization failed: ${res.status} ${await res.text()}`,
		);
	const org = await res.json();
	return org.id.value ?? org.id;
}

async function createInvitation(token, orgId, inviteeId) {
	const res = await fetch(`${API}/v1/organizations/${orgId}/invitations`, {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify({ inviteeId }),
	});
	if (!res.ok)
		throw new Error(
			`Create invitation failed: ${res.status} ${await res.text()}`,
		);
	return res.json();
}

async function deleteOrganization(token, orgId) {
	const res = await fetch(`${API}/v1/organizations/${orgId}`, {
		method: "DELETE",
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok) throw new Error(`Delete organization failed: ${res.status}`);
}

async function loginAsUser(page, username, password) {
	await page.goto(`${BASE}/`, { waitUntil: "networkidle" });
	const signInBtn = page.getByRole("button", { name: /sign in|anmelden/i });
	if ((await signInBtn.count()) > 0) {
		await signInBtn.first().click();
		await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
		await loginKeycloak(page, username, password);
	}
	await page.waitForSelector("main", { timeout: 10000 });
}

async function main() {
	const healthRes = await fetch(`${API}/health`);
	if (!healthRes.ok)
		throw new Error(`Health check failed: ${healthRes.status}`);
	console.log("OK  API health check passed");

	const suffix = Date.now();
	const veraToken = await getToken("vera", "vera123");
	const veraUserId = await getUserId(veraToken);

	const olafToken = await getToken("olaf", "olaf123");
	const orgId = await createOrganization(
		olafToken,
		`Smoke695 Org ${suffix}`,
	);
	const invitation = await createInvitation(olafToken, orgId, veraUserId);
	console.log(
		`OK  Created org ${orgId} and invited vera (invitation ${invitation.invitationId})`,
	);

	const { browser, page } = await launchLiveBrowser();
	try {
		await loginAsUser(page, "vera", "vera123");
		await page.goto(`${BASE}/profile`, { waitUntil: "networkidle" });

		// === Tab bar shows exactly 2 tabs, not the old 4 ===
		await page
			.getByRole("button", { name: "Profile", exact: true })
			.waitFor({ timeout: 20000 });
		await page
			.getByRole("button", { name: "Activity", exact: true })
			.waitFor({ timeout: 5000 });
		for (const oldLabel of ["Engagements", "Achievements", "Invitations"]) {
			const count = await page
				.getByRole("button", { name: oldLabel, exact: true })
				.count();
			if (count > 0) {
				throw new Error(
					`Old tab button "${oldLabel}" is still present - tabs were not consolidated`,
				);
			}
		}
		console.log(
			'OK  Tab bar shows exactly "Profile" and "Activity" - no leftover Engagements/Achievements/Invitations buttons',
		);

		// === Achievements merged directly into the Profile tab ===
		const shareBtn = page.getByRole("button", { name: "Share achievements" });
		await shareBtn.waitFor({ timeout: 20000 });
		console.log(
			'OK  "Share achievements" button is visible on the Profile tab without switching tabs',
		);

		await shareBtn.click();
		const dialog = page.locator("[role='dialog']");
		await dialog.waitFor({ timeout: 10000 });
		const shareUrlText = (await dialog.textContent()) ?? "";
		if (
			!shareUrlText.includes(`/users/${veraUserId}`) ||
			shareUrlText.includes("/achievements")
		) {
			throw new Error(
				`Share URL should point at the combined public profile (/users/${veraUserId}), dialog text was: ${shareUrlText}`,
			);
		}
		console.log(
			"OK  Share achievements URL points at the combined public profile, not the achievements-only page",
		);
		await page.keyboard.press("Escape");

		// === Activity tab merges Engagements + an open-invitations banner ===
		await page.getByRole("button", { name: "Activity", exact: true }).click();
		await page
			.locator("[data-testid='engagements-scope-upcoming']")
			.waitFor({ timeout: 15000 });
		await page.getByText("Open Invitations").waitFor({ timeout: 15000 });
		await page
			.getByText(`Smoke695 Org ${suffix}`)
			.first()
			.waitFor({ timeout: 10000 });
		console.log(
			"OK  Activity tab shows the engagements scope toggle and the open-invitations banner together",
		);

		await page.getByRole("button", { name: "Decline" }).first().click();
		await page
			.getByText(`Smoke695 Org ${suffix}`)
			.waitFor({ state: "hidden", timeout: 10000 });
		console.log("OK  Declining the invitation removes it from the banner");

		// === Legacy ?tab= deep links keep resolving to the right merged tab ===
		await page.goto(`${BASE}/profile?tab=achievements`, {
			waitUntil: "networkidle",
		});
		await page
			.getByRole("button", { name: "Share achievements" })
			.waitFor({ timeout: 20000 });
		console.log(
			"OK  Legacy /profile?tab=achievements deep link still resolves to the Profile tab",
		);

		await page.goto(`${BASE}/profile?tab=engagements`, {
			waitUntil: "networkidle",
		});
		await page
			.locator("[data-testid='engagements-scope-upcoming']")
			.waitFor({ timeout: 15000 });
		console.log(
			"OK  Legacy /profile?tab=engagements deep link still resolves to the Activity tab",
		);
	} finally {
		await browser.close();
	}

	// --- Cleanup ---
	await deleteOrganization(olafToken, orgId);
	console.log("OK  Cleaned up test organization");

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
