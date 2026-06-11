/**
 * Smoke test: Keycloak login theme redesign
 *
 * Verifies on the live staging environment:
 *  1. Login page loads without errors
 *  2. Real brand logo is present (img tag pointing to logo.svg)
 *  3. Page background is the brand-50 green tint (not pure white)
 *  4. Standard label-above-input layout (no floating labels)
 *  5. Registration page loads without first/last name fields
 *  6. Health gate passes
 */

import { chromium } from 'playwright';

const BASE_URL  = 'https://einsatzbereit.maik-hasler.de';
const API_URL   = 'https://api.maik-hasler.de';
const KC_URL    = 'https://login.maik-hasler.de/realms/einsatzbereit';

let passed = 0;
let failed = 0;

function assert(label, condition) {
  if (condition) {
    console.log(`  PASS  ${label}`);
    passed++;
  } else {
    console.error(`  FAIL  ${label}`);
    failed++;
  }
}

// ── Health gate ───────────────────────────────────────────────────────────────
console.log('\n[1] Health gate');
{
  const res = await fetch(`${API_URL}/health`);
  assert('GET /health returns 200', res.status === 200);
}

// ── Browser checks ────────────────────────────────────────────────────────────
const browser = await chromium.launch();
const context = await browser.newContext({ ignoreHTTPSErrors: true });
const page    = await context.newPage();

// ── Login page ────────────────────────────────────────────────────────────────
console.log('\n[2] Keycloak login page');
await page.goto(`${KC_URL}/protocol/openid-connect/auth?client_id=frontend&redirect_uri=${encodeURIComponent(BASE_URL + '/callback')}&response_type=code&scope=openid`);
await page.waitForLoadState('networkidle');

// Logo - should be an <img> with src ending in logo.svg, not an inline SVG
const logoImg = await page.$('img.auth-logo');
assert('Logo is an <img> element with class auth-logo', logoImg !== null);
if (logoImg) {
  const src = await logoImg.getAttribute('src');
  assert('Logo src points to logo.svg', src?.includes('logo.svg') ?? false);
}

// Background color should be brand-50 (#f0faf5), not pure white
const bgColor = await page.evaluate(() => {
  return getComputedStyle(document.body).backgroundColor;
});
// #f0faf5 = rgb(240, 250, 245)
assert(
  `Page background is brand-50 (rgb(240, 250, 245)), got: ${bgColor}`,
  bgColor === 'rgb(240, 250, 245)'
);

// Standard label-above-input: label should NOT be position:absolute (no floating labels)
const labelPosition = await page.evaluate(() => {
  const label = document.querySelector('.form-label');
  return label ? getComputedStyle(label).position : null;
});
assert(
  `Form label is not position:absolute (got: ${labelPosition})`,
  labelPosition !== 'absolute'
);

// Username and password inputs exist
const usernameInput = await page.$('#username');
const passwordInput = await page.$('#password');
assert('Username input present', usernameInput !== null);
assert('Password input present', passwordInput !== null);

// Card has a visible border (branded card, not plain white on white)
const cardBorder = await page.evaluate(() => {
  const card = document.querySelector('.auth-card');
  return card ? getComputedStyle(card).borderTopStyle : null;
});
assert(`Auth card has a border (got: ${cardBorder})`, cardBorder === 'solid');

// ── Registration page ─────────────────────────────────────────────────────────
console.log('\n[3] Registration page - no first/last name fields');
await page.goto(`${KC_URL}/protocol/openid-connect/registrations?client_id=frontend&redirect_uri=${encodeURIComponent(BASE_URL + '/callback')}&response_type=code&scope=openid`);
await page.waitForLoadState('networkidle');

const firstNameField = await page.$('#firstName');
const lastNameField  = await page.$('#lastName');
const emailField     = await page.$('#email');
const usernameField  = await page.$('#username');
const regPassword    = await page.$('#password');

assert('firstName field is NOT present on registration form', firstNameField === null);
assert('lastName field is NOT present on registration form', lastNameField === null);
assert('email field IS present on registration form', emailField !== null);
assert('username field IS present on registration form', usernameField !== null);
assert('password field IS present on registration form', regPassword !== null);

await browser.close();

// ── Summary ───────────────────────────────────────────────────────────────────
console.log(`\nResults: ${passed} passed, ${failed} failed`);
if (failed > 0) {
  console.error('Smoke test FAILED');
  process.exit(1);
}
console.log('Smoke test PASSED');
