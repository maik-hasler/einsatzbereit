// Smoke test for #655:
//
// Once a volunteer opportunity is deleted, EngagementCreated/withdrawal
// notifications generated for it before the deletion keep pointing at the
// now-gone opportunity. Two user-facing dead ends resulted:
//
// - The notification bell interpolated the (now-null) relatedTitle straight
//   into the notification text, rendering "New application received for"
//   with nothing after "for".
// - Clicking through to the opportunity's "Manage applications" page
//   rendered the backend's raw ProblemDetails message ("Volunteer
//   opportunity '{guid}' not found.") instead of the app's friendly
//   NotFoundPage.
//
// Fix: Header.tsx falls back to a translated placeholder ("a deleted
// opportunity" / "einen geloeschten Bedarf") when relatedTitle is null, and
// EngagementManagementPage detects a 404 on its engagements fetch (via the
// new isApiNotFoundError() helper) and renders NotFoundPage.
//
// Run: node scripts/smoke-test-655-deleted-opportunity.mjs

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

async function createOpportunity(token, orgId, title) {
	const res = await fetch(`${API}/v1/volunteer-opportunities`, {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify({
			title,
			description: "Automated smoke test opportunity for #655.",
			organizationId: orgId,
			isRemote: true,
			occurrence: "OneTime",
			participationType: "IndividualContact",
			checkInMethod: "None",
			isDraft: false,
		}),
	});
	if (!res.ok)
		throw new Error(
			`Create opportunity failed: ${res.status} ${await res.text()}`,
		);
	return res.json();
}

async function applyToOpportunity(token, opportunityId, message) {
	const res = await fetch(
		`${API}/v1/volunteer-opportunities/${opportunityId}/engagements`,
		{
			method: "POST",
			headers: {
				Authorization: `Bearer ${token}`,
				"Content-Type": "application/json",
			},
			body: JSON.stringify({ message }),
		},
	);
	if (!res.ok)
		throw new Error(`Apply failed: ${res.status} ${await res.text()}`);
	return res.json();
}

async function deleteOpportunity(token, opportunityId) {
	const res = await fetch(`${API}/v1/volunteer-opportunities/${opportunityId}`, {
		method: "DELETE",
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok)
		throw new Error(`Delete failed: ${res.status} ${await res.text()}`);
}

async function getNotifications(token) {
	const res = await fetch(`${API}/v1/notifications`, {
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok) throw new Error(`GET /notifications failed: ${res.status}`);
	return res.json();
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

	// --- Setup: olaf creates an opportunity, vera applies, olaf deletes it ---
	const olafToken = await getToken("olaf", "olaf123");
	const orgsRes = await fetch(`${API}/v1/organizations`, {
		headers: { Authorization: `Bearer ${olafToken}` },
	});
	if (!orgsRes.ok)
		throw new Error(`GET /organizations failed: ${orgsRes.status}`);
	const orgs = await orgsRes.json();
	if (!Array.isArray(orgs) || orgs.length === 0)
		throw new Error("olaf has no organizations - cannot run this smoke test");
	const orgId = orgs[0].id;

	const opportunity = await createOpportunity(
		olafToken,
		orgId,
		`Smoke655 DeletedOpportunity ${Date.now()}`,
	);
	console.log(`OK  Created opportunity ${opportunity.id}`);

	const veraToken = await getToken("vera", "vera123");
	await applyToOpportunity(
		veraToken,
		opportunity.id,
		"Smoke test application for #655.",
	);
	console.log("OK  vera applied - EngagementCreated notification queued for olaf");

	await deleteOpportunity(olafToken, opportunity.id);
	console.log("OK  olaf deleted the opportunity");

	// === Ground-truth data check: relatedTitle stays null, actionUrl survives ===
	const notifications = await getNotifications(olafToken);
	const notification = notifications.find(
		(n) =>
			n.kind === "EngagementCreated" &&
			n.actionUrl === `/volunteer-opportunities/${opportunity.id}/engagements`,
	);
	if (!notification)
		throw new Error(
			"Could not find the EngagementCreated notification for the deleted opportunity",
		);
	if (notification.relatedTitle !== null && notification.relatedTitle !== undefined) {
		throw new Error(
			`Expected relatedTitle to be null for a deleted opportunity, got "${notification.relatedTitle}"`,
		);
	}
	console.log(
		"OK  Notification for the deleted opportunity has relatedTitle: null, actionUrl intact",
	);

	// === UI check: notification bell shows the placeholder, not a bare string ===
	{
		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "olaf", "olaf123");
			const bell = page.getByTestId("notification-bell");
			await bell.waitFor({ timeout: 15000 });
			await bell.click();
			const panel = page.getByTestId("notification-panel");
			await panel.waitFor({ timeout: 5000 });

			const bodyText = await panel.innerText();
			if (!bodyText.includes("a deleted opportunity")) {
				throw new Error(
					`Expected notification panel to contain the "a deleted opportunity" placeholder, got: ${bodyText.slice(0, 500)}`,
				);
			}
			if (/for\s*\n/.test(bodyText) || /for\s*$/m.test(bodyText)) {
				throw new Error(
					"Notification text still looks truncated (ends with a bare 'for')",
				);
			}
			console.log(
				'OK  Notification bell renders "a deleted opportunity" placeholder instead of a bare string',
			);

			// === UI check: clicking through renders NotFoundPage, not a raw error ===
			await page.goto(
				`${BASE}/volunteer-opportunities/${opportunity.id}/engagements`,
				{ waitUntil: "networkidle" },
			);
			await page
				.getByRole("heading", { name: /page not found|seite nicht gefunden/i })
				.waitFor({ timeout: 15000 });
			const rawErrorLocator = page.getByText(/Volunteer opportunity .* not found/i);
			if ((await rawErrorLocator.count()) > 0) {
				throw new Error(
					"Raw ProblemDetails error text is still visible on the page",
				);
			}
			console.log(
				"OK  Manage applications page for the deleted opportunity renders NotFoundPage, not the raw error",
			);
		} finally {
			await browser.close();
		}
	}

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
