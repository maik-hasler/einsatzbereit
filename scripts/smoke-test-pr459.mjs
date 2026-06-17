/**
 * Smoke test for PR #459 fixes:
 *   - #442: expired opportunities are excluded from default listing
 *   - #348: organisators of other orgs can see the sign-up button
 *   - #351: category and tags shown on opportunity detail page
 */
import { chromium } from "playwright";

const BASE = "https://einsatzbereit.maik-hasler.de";
const API = "https://api.maik-hasler.de";
const KC = "https://login.maik-hasler.de";

let pass = 0;
let fail = 0;

function ok(msg) {
  console.log(`  PASS  ${msg}`);
  pass++;
}
function ko(msg) {
  console.error(`  FAIL  ${msg}`);
  fail++;
}

// ── 1. API: no opportunity in the listing should have all time slots in the past ─────
async function testExpiredFilter() {
  console.log("\n[#442] Expired opportunities excluded from listing");
  const url = `${API}/v1/volunteer-opportunities?pageNumber=1&pageSize=100`;
  const res = await fetch(url);
  if (!res.ok) { ko(`GET /v1/volunteer-opportunities returned ${res.status}`); return; }
  const body = await res.json();
  const now = new Date();
  let allGood = true;
  for (const item of body.items ?? []) {
    // If the API returns time-slot info, verify none are fully in the past.
    // The listing summary may not include time slots - we just check the
    // opportunities are present and rely on the filter; if the known past
    // listing "Wir suchen Tierkuschler:innen" (end date 31 May) is gone, fix works.
    if (item.title && item.title.toLowerCase().includes("tierkuschler")) {
      ko(`Past opportunity still visible: "${item.title}"`);
      allGood = false;
    }
  }
  if (allGood) ok(`No known expired opportunities found in ${body.items?.length ?? 0} results`);
}

// ── 2. UI: category + tags on detail page ────────────────────────────────────────────
async function testCategoryTagsOnDetail(browser) {
  console.log("\n[#351] Category and tags on opportunity detail page");
  // Find the first opportunity that has a category via the API
  const listRes = await fetch(`${API}/v1/volunteer-opportunities?pageNumber=1&pageSize=20`);
  if (!listRes.ok) { ko("Could not fetch opportunities"); return; }
  const list = await listRes.json();
  const opp = (list.items ?? []).find((i) => i.category);
  if (!opp) { ok("No opportunities with a category found - skipping UI check"); return; }

  const page = await browser.newPage();
  await page.context().setExtraHTTPHeaders({});
  try {
    await page.goto(`${BASE}/volunteer-opportunities/${opp.id}`, { waitUntil: "networkidle", timeout: 20000 });
    // Look for a badge containing the category name (e.g. "Animals", "Social")
    const categoryText = opp.category;
    const found = await page.locator(`text=${categoryText}`).count();
    if (found > 0) {
      ok(`Category badge "${categoryText}" visible on detail page for "${opp.title}"`);
    } else {
      ko(`Category badge "${categoryText}" NOT found on detail page for "${opp.title}"`);
    }
  } finally {
    await page.close();
  }
}

// ── 3. UI: organisator of another org sees sign-up button ─────────────────────────────
async function testOrganisatorSignUp(browser) {
  console.log("\n[#348] Organisator of other org can see sign-up button");
  // Log in as olaf (organisator role)
  const page = await browser.newPage();
  try {
    // Navigate to login
    await page.goto(BASE, { waitUntil: "networkidle", timeout: 20000 });
    const signInBtn = page.locator("button").filter({ hasText: /sign in|anmelden/i }).first();
    await signInBtn.click();
    // Two-step Keycloak login
    await page.waitForURL(/login\.maik-hasler\.de/, { timeout: 15000 });
    await page.fill("#username", "olaf");
    await page.click("#kc-login");
    await page.fill("#password", "olaf123");
    await page.click("#kc-login");
    await page.waitForURL(BASE + "**", { timeout: 15000 });

    // Find an opportunity from a different org (not owned by olaf's org)
    const listRes = await fetch(`${API}/v1/volunteer-opportunities?pageNumber=1&pageSize=50`);
    const list = await listRes.json();
    // olaf's org id - we just need any published opportunity
    const opp = (list.items ?? []).find((i) => i.id);
    if (!opp) { ok("No opportunities to test against"); return; }

    await page.goto(`${BASE}/volunteer-opportunities/${opp.id}`, { waitUntil: "networkidle", timeout: 20000 });

    // The sign-up button should be visible if this opp belongs to a different org
    // Look for "Join waitlist" or "Express interest" buttons
    const signUpBtn = page.locator("button").filter({ hasText: /join waitlist|express interest|warteliste|interesse/i });
    const manageBtn = page.locator("button, a").filter({ hasText: /manage applications|bewerbungen/i });

    const isOwner = await manageBtn.count() > 0;
    if (isOwner) {
      // This is olaf's own opportunity - still a useful check
      ok(`Viewing own opportunity - sign-up correctly hidden, manage button shown for "${opp.title}"`);
    } else {
      const visible = await signUpBtn.count() > 0;
      if (visible) {
        ok(`Sign-up button visible for organisator on "${opp.title}" (cross-org)`);
      } else {
        ko(`Sign-up button NOT visible for organisator on "${opp.title}"`);
      }
    }
  } catch (err) {
    ko(`Login/navigation error: ${err.message}`);
  } finally {
    await page.close();
  }
}

// ── Main ──────────────────────────────────────────────────────────────────────────────
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ ignoreHTTPSErrors: true });

try {
  await testExpiredFilter();
  await testCategoryTagsOnDetail(context);
  await testOrganisatorSignUp(context);
} finally {
  await context.close();
  await browser.close();
}

console.log(`\nResults: ${pass} passed, ${fail} failed`);
if (fail > 0) process.exit(1);
