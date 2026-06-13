/**
 * Smoke test: Keycloak login theme redesign
 *
 * Verifies on the live staging environment:
 *  1. Health gate passes
 *  2. Login page loads with brand-50 background (no white override from common/keycloak)
 *  3. Real brand logo present (img.auth-logo pointing to logo.svg)
 *  4. Label-above-input layout (no floating labels / position:absolute labels)
 *  5. Primary button is clearly green (not dark / near-black)
 *  6. Forgot password link present
 *  7. Password reset page loads and has a submit button
 *  8. Registration page loads with correct fields and "Already have an account?" link
 */

import { chromium } from 'playwright';

const BASE_URL = 'https://einsatzbereit.maik-hasler.de';
const API_URL  = 'https://api.maik-hasler.de';
const KC_URL   = 'https://login.maik-hasler.de/realms/einsatzbereit';

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

const loginUrl = `${KC_URL}/protocol/openid-connect/auth?client_id=frontend&redirect_uri=${encodeURIComponent(BASE_URL + '/callback')}&response_type=code&scope=openid`;

// ── Login page ────────────────────────────────────────────────────────────────
console.log('\n[2] Login page structure');
await page.goto(loginUrl);
await page.waitForLoadState('networkidle');

// Logo
const logoImg = await page.$('img.auth-logo');
assert('Logo is <img class="auth-logo">', logoImg !== null);
if (logoImg) {
	const src = await logoImg.getAttribute('src');
	assert('Logo src points to logo.svg', src?.includes('logo.svg') ?? false);
}

// Background color should be brand-50 (#f0faf5 = rgb(240, 250, 245))
const bgColor = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
assert(
	`Page background is brand-50 rgb(240, 250, 245) - got: ${bgColor}`,
	bgColor === 'rgb(240, 250, 245)'
);

// Labels must be static/relative - not absolute (floating label pattern)
const labelPosition = await page.evaluate(() => {
	const label = document.querySelector('.form-label');
	return label ? getComputedStyle(label).position : null;
});
assert(
	`Form label is NOT position:absolute - got: ${labelPosition}`,
	labelPosition !== null && labelPosition !== 'absolute'
);

// Username input on step 1 (live KC uses two-step login: username then password)
const usernameInput = await page.$('#username');
assert('Username input present on step 1', usernameInput !== null);

// Auth card has a border (branded, not plain)
const cardBorder = await page.evaluate(() => {
	const card = document.querySelector('.auth-card');
	return card ? getComputedStyle(card).borderTopStyle : null;
});
assert(`Auth card has border - got: ${cardBorder}`, cardBorder === 'solid');

console.log('\n[3] Login page button color (must be green, not dark/black)');
const btnBg = await page.evaluate(() => {
	const btn = document.querySelector('.btn-primary');
	return btn ? getComputedStyle(btn).backgroundColor : null;
});
// brand-600 = #2d8a5e = rgb(45, 138, 94) - green channel must dominate red
const btnBgParsed = btnBg?.match(/rgb\((\d+),\s*(\d+),\s*(\d+)\)/);
const isGreen = btnBgParsed
	? parseInt(btnBgParsed[2]) > 80 && parseInt(btnBgParsed[2]) > parseInt(btnBgParsed[1]) * 1.5
	: false;
assert(`Primary button is visibly green - got: ${btnBg}`, isGreen);

// ── Step 2: advance to password page ─────────────────────────────────────────
console.log('\n[4] Login step 2 - password page');
if (usernameInput) {
	await page.fill('#username', 'vera');
	await page.click('input[type="submit"]');
	await page.waitForLoadState('networkidle');
}

const passwordInput = await page.$('#password');
assert('Password input present on step 2', passwordInput !== null);

const forgotLink = await page.$('a[href*="reset-credentials"]');
assert('Forgot password link present on step 2', forgotLink !== null);

// ── Password reset page ───────────────────────────────────────────────────────
console.log('\n[5] Password reset page');
if (forgotLink) {
	await forgotLink.click();
	await page.waitForLoadState('networkidle');

	const resetInput = await page.$('#username');
	const submitBtn  = await page.$('input[type="submit"]');
	const backLink   = await page.$('.card-footer a');

	assert('Reset page has email/username input', resetInput !== null);
	assert('Reset page has submit button', submitBtn !== null);
	assert('Reset page has back-to-login link', backLink !== null);

	await page.goto(loginUrl);
	await page.waitForLoadState('networkidle');
}

// ── Registration page ─────────────────────────────────────────────────────────
console.log('\n[6] Registration page');
const regUrl = `${KC_URL}/protocol/openid-connect/registrations?client_id=frontend&redirect_uri=${encodeURIComponent(BASE_URL + '/callback')}&response_type=code&scope=openid`;
await page.goto(regUrl);
await page.waitForLoadState('networkidle');

const firstNameField = await page.$('#firstName');
const lastNameField  = await page.$('#lastName');
const emailField     = await page.$('#email');
const usernameField  = await page.$('#username');
const regPassword    = await page.$('#password');

assert('firstName NOT present (custom register form)', firstNameField === null);
assert('lastName NOT present (custom register form)', lastNameField === null);
assert('email field present', emailField !== null);
assert('username field present', usernameField !== null);
assert('password field present', regPassword !== null);

// "Already have an account?" link should not show raw message key
const cardFooter = await page.$('.card-footer');
if (cardFooter) {
	const footerText = await cardFooter.innerText();
	assert(
		`Card footer does not show raw key "alreadyHaveAccount" - got: "${footerText.trim()}"`,
		!footerText.includes('alreadyHaveAccount')
	);
	assert(
		'Card footer contains sign-in link',
		await page.$('.card-footer a') !== null
	);
}

await browser.close();

// ── Summary ───────────────────────────────────────────────────────────────────
console.log(`\nResults: ${passed} passed, ${failed} failed`);
if (failed > 0) {
	console.error('Smoke test FAILED');
	process.exit(1);
}
console.log('Smoke test PASSED');
