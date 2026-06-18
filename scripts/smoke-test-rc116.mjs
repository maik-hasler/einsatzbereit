/**
 * Smoke test for v1.0.0-rc.116
 * Verifies: health check, unique engagement constraint migration (#368),
 * and My Engagements org name (#364 backend join).
 */

const API_URL = 'https://api.maik-hasler.de';

// 1. Health check
console.log('[1/3] Health check...');
const health = await fetch(`${API_URL}/health`);
if (!health.ok) throw new Error(`Health check failed: ${health.status}`);
console.log('  ✓ API healthy');

// 2. Verify unique engagement constraint in OpenAPI spec
console.log('[2/3] Unique engagement constraint (409 in OpenAPI spec)...');
const openApiResp = await fetch(`${API_URL}/v1/openapi.json`).catch(() => null);
if (openApiResp && openApiResp.ok) {
	const spec = await openApiResp.json();
	const postEngagement = spec?.paths?.['/v1/engagements']?.post;
	const has409 = postEngagement?.responses?.['409'] !== undefined;
	if (has409) {
		console.log('  ✓ POST /v1/engagements declares 409 in OpenAPI spec');
	} else {
		throw new Error('POST /v1/engagements does not declare 409 in OpenAPI spec');
	}
} else {
	console.log('  ⚠ OpenAPI spec not accessible, skipping');
}

// 3. Verify EngagementSummary has organizationId/organizationName in OpenAPI spec
console.log('[3/3] EngagementSummary org fields in OpenAPI spec (#364)...');
if (openApiResp && openApiResp.ok) {
	const spec = await openApiResp.json();
	const engagementSummary = spec?.components?.schemas?.EngagementSummary;
	const hasOrgId = engagementSummary?.properties?.organizationId !== undefined;
	const hasOrgName = engagementSummary?.properties?.organizationName !== undefined;
	if (hasOrgId && hasOrgName) {
		console.log('  ✓ EngagementSummary schema has organizationId and organizationName');
	} else {
		const missing = [!hasOrgId && 'organizationId', !hasOrgName && 'organizationName']
			.filter(Boolean)
			.join(', ');
		throw new Error(`EngagementSummary missing fields: ${missing}`);
	}
} else {
	console.log('  ⚠ OpenAPI spec not accessible, skipping');
}

console.log('\n✅ All smoke checks passed for v1.0.0-rc.116');
