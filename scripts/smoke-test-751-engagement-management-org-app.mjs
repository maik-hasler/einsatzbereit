// Smoke test for #751:
//
// EngagementManagementPage ("Manage applications" - view/confirm/cancel/
// check-in applications) used to live outside the organisation app, at
// /volunteer-opportunities/:opportunityId/engagements rendered under the
// public site layout. Organizers lost the org switcher, tab nav, and
// org-scoped breadcrumb whenever they opened it.
//
// Fix: the page is now nested under /app/:organizationId/opportunities/
// :opportunityId/engagements, rendered inside OrgAppLayout. The old public
// route was deleted outright (not redirected). Notification actionUrls for
// EngagementCreated now point at the new org-app URL. Owner-only actions
// (Edit/Delete/Publish/Manage applications) were removed entirely from the
// public VolunteerOpportunityDetailPage - all organizer tooling now lives
// exclusively in the org app, including a new Delete action on the
// Opportunities tab (VolunteerOpportunityDetailPage's delete had no other
// replacement).
//
// Run: node scripts/smoke-test-751-engagement-management-org-app.mjs

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

async function createOpportunity(token, orgId, title) {
	const res = await fetch(`${API}/v1/volunteer-opportunities`, {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify({
			title,
			description: "Automated smoke test opportunity for #751.",
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

async function getMyNotifications(token) {
	const res = await fetch(`${API}/v1/notifications`, {
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok) throw new Error(`GET /notifications failed: ${res.status}`);
	return res.json();
}

async function deleteOpportunity(token, opportunityId) {
	const res = await fetch(
		`${API}/v1/volunteer-opportunities/${opportunityId}`,
		{
			method: "DELETE",
			headers: { Authorization: `Bearer ${token}` },
		},
	);
	if (!res.ok) throw new Error(`Delete opportunity failed: ${res.status}`);
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
	const olafToken = await getToken("olaf", "olaf123");
	const orgId = await createOrganization(olafToken, `Smoke751 Org ${suffix}`);

	const opportunity = await createOpportunity(
		olafToken,
		orgId,
		`Smoke751 Opportunity ${suffix}`,
	);
	const throwawayOpportunity = await createOpportunity(
		olafToken,
		orgId,
		`Smoke751 Throwaway ${suffix}`,
	);
	console.log(`OK  Created opportunities under org ${orgId}`);

	const veraToken = await getToken("vera", "vera123");
	await applyToOpportunity(
		veraToken,
		opportunity.id,
		"Applying for #751 smoke test.",
	);
	console.log("OK  vera applied - triggers an EngagementCreated notification");

	// === Notification actionUrl points at the new org-app URL ===
	const olafNotifications = await getMyNotifications(olafToken);
	const notification = olafNotifications.find(
		(n) => n.kind === "EngagementCreated" && n.relatedEntityId,
	);
	if (!notification)
		throw new Error("Expected an EngagementCreated notification for olaf");
	const expectedActionUrl = `/app/${orgId}/opportunities/${opportunity.id}/engagements`;
	if (notification.actionUrl !== expectedActionUrl) {
		throw new Error(
			`Notification actionUrl mismatch - expected "${expectedActionUrl}", got "${notification.actionUrl}"`,
		);
	}
	console.log(
		"OK  EngagementCreated notification actionUrl resolves under /app/:organizationId/...",
	);

	// === UI checks ===
	{
		const { browser, page } = await launchLiveBrowser();
		try {
			await loginAsUser(page, "olaf", "olaf123");

			// The old public URL is gone outright - it must 404, not redirect.
			await page.goto(
				`${BASE}/volunteer-opportunities/${opportunity.id}/engagements`,
				{ waitUntil: "networkidle" },
			);
			await page
				.getByRole("heading", { name: /page not found|seite nicht gefunden/i })
				.waitFor({ timeout: 15000 });
			console.log("OK  Old /volunteer-opportunities/.../engagements URL 404s");

			// The public detail page has no owner-management actions left.
			await page.goto(`${BASE}/volunteer-opportunities/${opportunity.id}`, {
				waitUntil: "networkidle",
			});
			await page.waitForSelector("h1", { timeout: 15000 });
			for (const label of [
				"Edit",
				"Delete",
				"Publish",
				"Manage applications",
			]) {
				const count = await page
					.getByRole("button", { name: label })
					.count();
				if (count > 0) {
					throw new Error(
						`Public detail page still shows a "${label}" owner action - it should live only in the org app now`,
					);
				}
			}
			console.log(
				"OK  Public opportunity detail page has no Edit/Delete/Publish/Manage-applications actions",
			);

			// Navigate into the org app and reach engagement management via the
			// Opportunities tab's "Manage applications" link.
			await page.goto(`${BASE}/app/${orgId}/opportunities`, {
				waitUntil: "networkidle",
			});
			const row = page.locator("li", {
				hasText: `Smoke751 Opportunity ${suffix}`,
			});
			await row.waitFor({ timeout: 15000 });
			const manageLink = row.getByRole("link", {
				name: "Manage applications",
			});
			await manageLink.waitFor({ timeout: 10000 });
			const href = await manageLink.getAttribute("href");
			if (href !== expectedActionUrl) {
				throw new Error(
					`"Manage applications" link href mismatch - expected "${expectedActionUrl}", got "${href}"`,
				);
			}
			await manageLink.click();
			await page.waitForLoadState("networkidle");

			// Org-app chrome (switcher + tab nav) must still be visible, with
			// "Opportunities" active.
			await page
				.getByRole("button", { name: /switch organization/i })
				.waitFor({ timeout: 10000 });
			const opportunitiesTab = page.getByRole("link", {
				name: "Opportunities",
				exact: true,
			});
			const ariaCurrent = await opportunitiesTab.getAttribute("aria-current");
			if (ariaCurrent !== "page") {
				throw new Error(
					`Expected the Opportunities tab to be active (aria-current="page"), got "${ariaCurrent}"`,
				);
			}
			console.log(
				"OK  Engagement management keeps the org switcher and Opportunities tab active",
			);

			// The pending application is listed.
			await page.getByText("Applying for #751 smoke test.").waitFor({
				timeout: 15000,
			});
			console.log("OK  vera's pending application is listed");

			// Leaving via the tab nav returns to the opportunities list.
			await opportunitiesTab.click();
			await page.waitForURL(new RegExp(`/app/${orgId}/opportunities$`), {
				timeout: 15000,
			});
			console.log("OK  Leaving via the tab nav returns to the opportunities list");

			// The new Delete action (moved from the public detail page) works.
			const throwawayRow = page.locator("li", {
				hasText: `Smoke751 Throwaway ${suffix}`,
			});
			await throwawayRow.waitFor({ timeout: 15000 });
			await throwawayRow.getByTestId("opportunity-delete").click();
			await page.getByRole("button", { name: /yes, delete/i }).click();
			await throwawayRow.waitFor({ state: "detached", timeout: 15000 });
			console.log(
				"OK  Deleting an opportunity from the org app's Opportunities tab works",
			);
		} finally {
			await browser.close();
		}
	}

	// --- Cleanup ---
	await deleteOpportunity(olafToken, opportunity.id);
	await deleteOrganization(olafToken, orgId);
	console.log("OK  Cleaned up test opportunities and organization");

	console.log("\nALL CHECKS PASSED");
}

main().catch((err) => {
	console.error("FAIL", err.message);
	process.exit(1);
});
