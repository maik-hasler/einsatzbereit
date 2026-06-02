/**
 * Smoke test: verify security headers are present on staging responses.
 * Also checks that the API health endpoint and key pages respond correctly.
 * Run: node scripts/smoke-test-security-headers.mjs
 */

const API_BASE = 'https://api.maik-hasler.de';
const FRONTEND_BASE = 'https://einsatzbereit.maik-hasler.de';

let passed = 0;
let failed = 0;

function assert(condition, label) {
	if (condition) {
		console.log(`  PASS  ${label}`);
		passed++;
	} else {
		console.error(`  FAIL  ${label}`);
		failed++;
	}
}

async function checkHeaders(url, label) {
	console.log(`\nChecking headers: ${label} (${url})`);
	const res = await fetch(url, { redirect: 'follow' });
	const h = res.headers;

	assert(res.ok || res.status === 404, `HTTP response not error (${res.status})`);
	assert(h.get('x-content-type-options') === 'nosniff', 'X-Content-Type-Options: nosniff');
	assert(h.get('x-frame-options') === 'DENY', 'X-Frame-Options: DENY');
	assert(h.get('referrer-policy') === 'strict-origin-when-cross-origin', 'Referrer-Policy');
	assert(h.get('x-xss-protection') === '1; mode=block', 'X-XSS-Protection');
	assert(
		h.get('permissions-policy')?.includes('camera=()') === true,
		'Permissions-Policy includes camera=()',
	);
	assert(
		h.get('strict-transport-security')?.includes('max-age=') === true,
		'Strict-Transport-Security',
	);

	return res;
}

async function main() {
	console.log('=== Security Headers Smoke Test ===');

	// API health
	console.log('\nChecking API health...');
	const health = await fetch(`${API_BASE}/health`);
	assert(health.ok, `GET /health returns 200 (got ${health.status})`);

	// Frontend security headers
	await checkHeaders(FRONTEND_BASE, 'Frontend root');
	await checkHeaders(`${FRONTEND_BASE}/achievements`, 'Frontend /achievements (SPA route)');

	console.log('\n=================================');
	console.log(`Results: ${passed} passed, ${failed} failed`);

	if (failed > 0) {
		process.exit(1);
	}
}

main().catch((err) => {
	console.error('Fatal error:', err);
	process.exit(1);
});
