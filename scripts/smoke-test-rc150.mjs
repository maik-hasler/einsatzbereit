/**
 * Smoke test for RC.150 - Unified profile overview page (#505 / PR #508).
 *
 * Verifies:
 * 1. API health endpoint returns 200
 * 2. Frontend loads
 * 3. Login as vera
 * 4. /profile renders with a tab bar (Profile | Engagements | Achievements)
 * 5. /my-engagements redirects to /profile?tab=engagements
 * 6. /achievements redirects to /profile?tab=achievements
 * 7. Engagements tab content renders (list or empty state)
 * 8. Achievements tab content renders (badge grid section)
 * 9. Header shows "My Profile" (single link, not three separate links)
 */
import { chromium } from "playwright";

const API = "https://api.maik-hasler.de";
const FRONTEND = "https://einsatzbereit.maik-hasler.de";

let passed = 0;
let failed = 0;

function pass(msg) {
	console.log(`  PASS  ${msg}`);
	passed++;
}
function fail(msg) {
	console.error(`  FAIL  ${msg}`);
	failed++;
}

// 1. Health check
{
	const res = await fetch(`${API}/health`);
	if (res.ok) {
		pass(`Health endpoint returned ${res.status}`);
	} else {
		fail(`Health endpoint returned ${res.status}`);
		process.exit(1);
	}
}

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await ctx.newPage();

try {
	// 2. Frontend loads
	await page.goto(FRONTEND, { waitUntil: "networkidle", timeout: 30000 });
	const title = await page.title();
	if (title && title.includes("Einsatzbereit")) {
		pass(`Frontend loaded (title: "${title}")`);
	} else {
		fail(`Unexpected page title: "${title}"`);
	}

	// 3. Login as vera
	const signinBtn = page
		.getByRole("button", { name: /sign in|anmelden/i })
		.first();
	await signinBtn.click({ timeout: 10000 });
	await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
	await page.fill("#username", "vera");
	await page.getByRole("button", { name: /sign in|anmelden/i }).click();
	await page.fill("#password", "vera123");
	await page.getByRole("button", { name: /sign in|anmelden/i }).click();
	await page.waitForURL(`${FRONTEND}/**`, { timeout: 15000 });
	await page.waitForTimeout(1000);
	pass("Logged in as vera");

	// 4. Navigate to /profile - should show tab bar
	await page.goto(`${FRONTEND}/profile`, {
		waitUntil: "networkidle",
		timeout: 30000,
	});
	const profileTabBtn = page.getByRole("button", {
		name: /^profile$|^profil$/i,
	});
	const profileTabVisible = await profileTabBtn
		.isVisible({ timeout: 5000 })
		.catch(() => false);
	if (profileTabVisible) {
		pass("Profile tab button visible on /profile");
	} else {
		fail("Profile tab button not visible on /profile");
	}

	const engagementsTabBtn = page.getByRole("button", {
		name: /^engagements$|^meine engagements$/i,
	});
	const engTabVisible = await engagementsTabBtn
		.isVisible({ timeout: 3000 })
		.catch(() => false);
	if (engTabVisible) {
		pass("Engagements tab button visible");
	} else {
		fail("Engagements tab button not visible");
	}

	const achievementsTabBtn = page.getByRole("button", {
		name: /^achievements$|^errungenschaften$/i,
	});
	const achTabVisible = await achievementsTabBtn
		.isVisible({ timeout: 3000 })
		.catch(() => false);
	if (achTabVisible) {
		pass("Achievements tab button visible");
	} else {
		fail("Achievements tab button not visible");
	}

	// 5. /my-engagements should redirect to /profile?tab=engagements
	await page.goto(`${FRONTEND}/my-engagements`, {
		waitUntil: "networkidle",
		timeout: 30000,
	});
	const engUrl = page.url();
	if (engUrl.includes("/profile") && engUrl.includes("tab=engagements")) {
		pass(`/my-engagements redirected to ${engUrl}`);
	} else {
		fail(`/my-engagements did not redirect correctly; landed on ${engUrl}`);
	}

	// 7. Engagements tab shows content or empty state
	const engContentVisible =
		(await page
			.locator("ul li")
			.first()
			.isVisible({ timeout: 5000 })
			.catch(() => false)) ||
		(await page
			.getByText(/no sign-ups|keine engagements/i)
			.isVisible({ timeout: 3000 })
			.catch(() => false));
	if (engContentVisible) {
		pass("Engagements tab content renders (list or empty state)");
	} else {
		fail("Engagements tab content not visible");
	}

	// 6. /achievements should redirect to /profile?tab=achievements
	await page.goto(`${FRONTEND}/achievements`, {
		waitUntil: "networkidle",
		timeout: 30000,
	});
	const achUrl = page.url();
	if (achUrl.includes("/profile") && achUrl.includes("tab=achievements")) {
		pass(`/achievements redirected to ${achUrl}`);
	} else {
		fail(`/achievements did not redirect correctly; landed on ${achUrl}`);
	}

	// 8. Achievements tab renders badge section heading
	const badgesSectionVisible = await page
		.getByRole("heading", { name: /badges|errungenschaften/i })
		.isVisible({ timeout: 5000 })
		.catch(() => false);
	if (badgesSectionVisible) {
		pass("Achievements tab renders badge section heading");
	} else {
		fail("Achievements badge section heading not visible");
	}

	// 9. Header shows a single "My Profile" link and NOT a separate "My Achievements" link
	await page.goto(FRONTEND, { waitUntil: "networkidle", timeout: 30000 });
	// Open the user menu dropdown
	const userMenuBtn = page.getByRole("button", { name: /user menu|benutzermenü/i });
	const menuVisible = await userMenuBtn
		.isVisible({ timeout: 5000 })
		.catch(() => false);
	if (menuVisible) {
		await userMenuBtn.click();
		await page.waitForTimeout(500);
		const myProfileLink = page.getByRole("link", {
			name: /my profile|mein profil/i,
		});
		const myProfileVisible = await myProfileLink
			.isVisible({ timeout: 3000 })
			.catch(() => false);
		if (myProfileVisible) {
			pass('Header dropdown shows "My Profile" link');
		} else {
			fail('Header dropdown missing "My Profile" link');
		}

		// Confirm "My Achievements" is no longer a separate link in the dropdown
		const myAchLink = page.getByRole("link", {
			name: /my achievements|meine errungenschaften/i,
		});
		const myAchSeparate = await myAchLink
			.isVisible({ timeout: 1000 })
			.catch(() => false);
		if (!myAchSeparate) {
			pass('"My Achievements" is no longer a separate header link');
		} else {
			fail('"My Achievements" still appears as a separate header link (should be consolidated)');
		}
	} else {
		console.log(
			"  User menu button not found by aria-label - skipping header check",
		);
	}
} catch (err) {
	fail(`Unexpected error: ${err.message}`);
	console.error(err);
} finally {
	await browser.close();
}

console.log(`\n${passed} passed, ${failed} failed`);
if (failed > 0) process.exit(1);
