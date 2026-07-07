// Smoke test for #628 (GetOpportunityFeedback 500s on every "Manage
// applications" page) and #629 ("Manage engagements" link shows a doubled
// arrow). Verifies:
//   1. GET /v1/volunteer-opportunities/{id}/feedback returns 200 with an
//      empty summary for a fresh opportunity with no engagements, instead
//      of the 500 reported in #628.
//   2. The org dashboard's "Engagements" tab renders exactly one arrow
//      indicator (the SVG icon) next to "Manage engagements", not two.
// Creates a throwaway organization + opportunity and deletes both at the
// end so this script doesn't leave junk data on the live site (see #630).
// Run: node scripts/smoke-test-628-629-feedback-and-arrow.mjs

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
	if (!healthRes.ok) throw new Error(`Health check failed: ${healthRes.status}`);
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
		body: JSON.stringify({ name: `Smoke628629 Org ${Date.now()}` }),
	});
	if (!orgRes.ok)
		throw new Error(`Create organization failed: ${orgRes.status} ${await orgRes.text()}`);
	const org = await orgRes.json();
	const orgId = org.id.value;
	console.log(`OK  Created throwaway organization ${orgId}`);

	let opportunityId;
	try {
		const oppRes = await fetch(`${API}/v1/volunteer-opportunities`, {
			method: "POST",
			headers: authHeaders,
			body: JSON.stringify({
				title: `Smoke628629 Opportunity ${Date.now()}`,
				description: "Automated smoke test for #628/#629.",
				organizationId: orgId,
				isRemote: true,
				occurrence: "OneTime",
				participationType: "IndividualContact",
				checkInMethod: "None",
				isDraft: false,
			}),
		});
		if (!oppRes.ok)
			throw new Error(`Create opportunity failed: ${oppRes.status} ${await oppRes.text()}`);
		const opportunity = await oppRes.json();
		opportunityId = opportunity.id;
		console.log(`OK  Created published opportunity ${opportunityId} with zero engagements`);

		// --- 1. #628: feedback endpoint must return 200 + empty summary ---
		const feedbackRes = await fetch(
			`${API}/v1/volunteer-opportunities/${opportunityId}/feedback`,
			{ headers: authHeaders },
		);
		if (!feedbackRes.ok) {
			throw new Error(
				`Expected 200 from GetOpportunityFeedback, got ${feedbackRes.status}: ${await feedbackRes.text()}`,
			);
		}
		const feedback = await feedbackRes.json();
		if (feedback.feedbackCount !== 0 || feedback.averageRating !== null || feedback.items.length !== 0) {
			throw new Error(`Expected empty feedback summary, got ${JSON.stringify(feedback)}`);
		}
		console.log("OK  GetOpportunityFeedback returns 200 with an empty summary (#628 fixed)");

		// --- 2. #629: exactly one arrow indicator on the manage-engagements link ---
		const { browser, page } = await launchLiveBrowser();
		try {
			await page.goto(BASE, { waitUntil: "networkidle" });
			await page.click("text=/sign in|anmelden/i");
			await page.waitForURL(/\/realms\//, { timeout: 30000 });
			await loginKeycloak(page, "olaf", "olaf123");

			await page.goto(`${BASE}/organizations/${orgId}/dashboard?tab=engagements`, {
				waitUntil: "networkidle",
			});

			const link = page
				.locator("li", { hasText: "Smoke628629 Opportunity" })
				.getByRole("link", { name: /manage engagements/i });
			await link.waitFor({ state: "visible", timeout: 15000 });

			const linkText = (await link.innerText()).trim();
			if (linkText.includes("→") || linkText.includes("->")) {
				throw new Error(
					`Expected the link text to have no literal arrow (SVG icon supplies it), got "${linkText}"`,
				);
			}
			const svgCount = await link.locator("svg").count();
			if (svgCount !== 1) {
				throw new Error(`Expected exactly one SVG arrow icon on the link, found ${svgCount}`);
			}
			console.log(
				`OK  "Manage engagements" link renders exactly one arrow (text: "${linkText}", svg icons: ${svgCount}) (#629 fixed)`,
			);
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
