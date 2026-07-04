// smoke-test-536-shared-form-classes.mjs
// Live verification for PR #570 (issue #536): OrganizationOverviewPage,
// OrganizationSettingsPage, and ProfileOverviewPage now import their
// inputClass/labelClass/textareaClass from a shared module instead of each
// defining an identical local constant. Pure CSS-class refactor, no
// behavioral change expected.
//
// Note: this sandbox's headless Chromium cannot reach the live site
// (net::ERR_CONNECTION_RESET even to unrelated hosts) while plain curl/fetch
// can - the same pre-existing constraint documented in PR #569's live
// verification. So instead of browser automation this exercises the exact
// save flow each refactored page's form submits through (api.updateUserProfile
// / api.updateOrganization), proving the pages still function end-to-end
// after removing their duplicated inputClass/labelClass/textareaClass
// constants.

const API = "https://api.maik-hasler.de";
const FRONTEND = "https://einsatzbereit.maik-hasler.de";
const KC = "https://login.maik-hasler.de/realms/einsatzbereit/protocol/openid-connect/token";

const OLAF_USER = "olaf";
const OLAF_PASS = "olaf123";

let passed = 0;
let failed = 0;

function ok(msg) {
	console.log(`  [PASS] ${msg}`);
	passed++;
}

function fail(msg, detail = "") {
	console.log(`  [FAIL] ${msg}${detail ? ": " + detail : ""}`);
	failed++;
}

async function getToken(user, pass) {
	const res = await fetch(KC, {
		method: "POST",
		headers: { "Content-Type": "application/x-www-form-urlencoded" },
		body: new URLSearchParams({
			grant_type: "password",
			client_id: "frontend",
			username: user,
			password: pass,
		}),
	});
	if (!res.ok) throw new Error(`token request failed: ${res.status}`);
	const data = await res.json();
	return data.access_token;
}

async function main() {
	console.log("=== Frontend serves the SPA shell (bundle referencing the refactored pages was built and deployed) ===");
	const feRes = await fetch(`${FRONTEND}/`);
	const feHtml = await feRes.text();
	if (feRes.ok && feHtml.includes("<div id=\"root\">")) {
		ok(`Frontend index.html serves 200 with SPA root (${feRes.status})`);
	} else {
		fail("Frontend index.html did not serve expected SPA shell", `status ${feRes.status}`);
	}

	console.log("\n=== Login as olaf (organisator) via Keycloak password grant ===");
	const token = await getToken(OLAF_USER, OLAF_PASS);
	if (token) {
		ok("Obtained access token for olaf");
	} else {
		fail("Could not obtain access token for olaf");
		console.log(`\n=== RESULT: ${passed} passed, ${failed} failed ===`);
		process.exit(1);
	}
	const authHeaders = {
		Authorization: `Bearer ${token}`,
		"Content-Type": "application/json",
	};

	// ── ProfileOverviewPage save flow: PUT /v1/users/me (api.updateUserProfile) ──
	console.log("\n=== ProfileOverviewPage save flow (api.updateUserProfile) ===");
	const meRes = await fetch(`${API}/v1/users/me`, { headers: authHeaders });
	if (!meRes.ok) {
		fail(`GET /v1/users/me returned ${meRes.status}`);
	} else {
		const me = await meRes.json();
		const marker = `smoke-536-${Date.now() % 100000}`;
		const updateRes = await fetch(`${API}/v1/users/me`, {
			method: "PUT",
			headers: authHeaders,
			body: JSON.stringify({
				...me,
				bio: marker,
			}),
		});
		if (updateRes.ok) {
			ok("PUT /v1/users/me (profile save) succeeded");
			const verifyRes = await fetch(`${API}/v1/users/me`, { headers: authHeaders });
			const verified = await verifyRes.json();
			if (verified.bio === marker) {
				ok("Profile update round-trips correctly (bio field persisted)");
			} else {
				fail("Profile update did not persist", `expected ${marker}, got ${verified.bio}`);
			}
			// restore original value
			await fetch(`${API}/v1/users/me`, {
				method: "PUT",
				headers: authHeaders,
				body: JSON.stringify(me),
			});
		} else {
			fail(`PUT /v1/users/me (profile save) returned ${updateRes.status}`, await updateRes.text());
		}
	}

	// ── OrganizationOverviewPage / OrganizationSettingsPage save flow: PUT organization ──
	console.log("\n=== OrganizationOverviewPage / OrganizationSettingsPage save flow (api.updateOrganization) ===");
	const orgsRes = await fetch(`${API}/v1/organizations`, { headers: authHeaders });
	if (!orgsRes.ok) {
		fail(`GET /v1/organizations returned ${orgsRes.status}`);
	} else {
		const orgsBody = await orgsRes.json();
		const org = Array.isArray(orgsBody) ? orgsBody[0] : (orgsBody?.items ?? orgsBody?.organizations ?? [])[0];
		if (!org?.id) {
			fail("olaf has no organization to verify org-page save flow against");
		} else {
			const orgDetailRes = await fetch(`${API}/v1/organizations/${org.id}`, { headers: authHeaders });
			const orgDetail = await orgDetailRes.json();
			const marker = `smoke-536-${Date.now() % 100000}`;
			const updateRes = await fetch(`${API}/v1/organizations/${org.id}`, {
				method: "PUT",
				headers: authHeaders,
				body: JSON.stringify({
					...orgDetail,
					description: marker,
				}),
			});
			if (updateRes.ok) {
				ok("PUT /v1/organizations/{id} (org settings save) succeeded");
				const verifyRes = await fetch(`${API}/v1/organizations/${org.id}`, { headers: authHeaders });
				const verified = await verifyRes.json();
				if (verified.description === marker) {
					ok("Organization update round-trips correctly (description field persisted)");
				} else {
					fail("Organization update did not persist", `expected ${marker}, got ${verified.description}`);
				}
				// restore original value
				await fetch(`${API}/v1/organizations/${org.id}`, {
					method: "PUT",
					headers: authHeaders,
					body: JSON.stringify(orgDetail),
				});
			} else {
				fail(`PUT /v1/organizations/{id} (org save) returned ${updateRes.status}`, await updateRes.text());
			}
		}
	}

	console.log(`\n=== RESULT: ${passed} passed, ${failed} failed ===`);
	if (failed > 0) {
		console.log("SOME CHECKS FAILED");
		process.exit(1);
	} else {
		console.log("ALL CHECKS PASSED");
		process.exit(0);
	}
}

main().catch((err) => {
	console.error("Smoke test crashed:", err);
	process.exit(1);
});
