import { test, expect, type APIRequestContext } from '@playwright/test';
import { getToken } from '../helpers/auth';
import { resetState } from '../helpers/cleanup';

const API = 'http://localhost:5000';

test.beforeEach(async () => {
  await resetState();
});

async function createOrgViaApi(token: string, request: APIRequestContext): Promise<string> {
  const res = await request.post(`${API}/v1/organizations`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { name: 'Auth Guard Test Org' },
  });
  expect(res.status()).toBe(200);
  const org = await res.json() as { id: { value: string } };
  return org.id.value;
}

async function createOpportunityViaApi(
  token: string,
  organizationId: string,
  request: APIRequestContext,
): Promise<string> {
  const res = await request.post(`${API}/v1/volunteer-opportunities`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      title: 'Auth Guard Test Bedarf',
      description: 'Test',
      organizationId,
      street: 'Teststraße',
      houseNumber: '1',
      zipCode: '12345',
      city: 'Berlin',
      occurrence: 'OneTime',
      participationType: 'IndividualContact',
    },
  });
  expect(res.status()).toBe(200);
  const opp = await res.json() as { id: string };
  return opp.id;
}

// ── Unauthenticated redirects ────────────────────────────────────────────────

test('/my-engagements redirects unauthenticated user to Keycloak', async ({ page }) => {
  await page.goto('/my-engagements');
  await page.waitForURL('**/realms/einsatzbereit/**', { timeout: 15000 });
  expect(page.url()).toMatch(/realms\/einsatzbereit/);
});

test('/volunteer-opportunities/:id/engagements redirects unauthenticated user to Keycloak', async ({ page, request }) => {
  const token = await getToken('olaf', 'olaf123');
  const orgId = await createOrgViaApi(token, request);
  const oppId = await createOpportunityViaApi(token, orgId, request);

  await page.goto(`/volunteer-opportunities/${oppId}/engagements`);
  await page.waitForURL('**/realms/einsatzbereit/**', { timeout: 15000 });
  expect(page.url()).toMatch(/realms\/einsatzbereit/);
});

// ── Header UI state ──────────────────────────────────────────────────────────

test('unauthenticated: header shows Anmelden and Registrieren, no avatar', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');

  await expect(page.getByRole('button', { name: 'Anmelden' }).first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Registrieren' }).first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Benutzermenü' })).not.toBeVisible();
});

test.describe('as user (hannah)', () => {
  test.use({ storageState: 'tests/.auth/hannah.json' });

  test('authenticated: header shows avatar, no login button', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('button', { name: 'Benutzermenü' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Anmelden' })).not.toBeVisible();
  });

  test('sign-up button visible on detail page for non-organisator user', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('button', { name: 'Interesse bekunden' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bearbeiten' })).not.toBeVisible();
    await expect(page.getByRole('button', { name: 'Löschen' })).not.toBeVisible();
  });
});

test.describe('as organisator (olaf)', () => {
  test.use({ storageState: 'tests/.auth/olaf.json' });

  test('sign-up button hidden for organisator on detail page', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('button', { name: 'Interesse bekunden' })).not.toBeVisible();
    await expect(page.getByRole('button', { name: 'Bearbeiten' })).toBeVisible();
  });

  test('"Bedarf erstellen" button visible for organisator with active org', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    await createOrgViaApi(token, request);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Auth Guard Test Org');

    await expect(page.getByTestId('create-opportunity-btn')).toBeVisible();
  });
});

// ── API contract: auth enforcement ───────────────────────────────────────────

test('POST /v1/volunteer-opportunities/:id/engagements returns 401 without token', async ({ request }) => {
  const response = await request.post(`${API}/v1/volunteer-opportunities/${crypto.randomUUID()}/engagements`, {
    data: { type: 'IndividualContact', message: 'test' },
  });
  expect(response.status()).toBe(401);
});

test('GET /v1/me/engagements returns 401 without token', async ({ request }) => {
  const response = await request.get(`${API}/v1/me/engagements`);
  expect(response.status()).toBe(401);
});

test('PUT /v1/engagements/:id/confirm returns 401 without token', async ({ request }) => {
  const response = await request.put(`${API}/v1/engagements/${crypto.randomUUID()}/confirm`);
  expect(response.status()).toBe(401);
});

test('PUT /v1/engagements/:id/confirm returns 403 for non-organisator', async ({ request }) => {
  const token = await getToken('hannah', 'hannah123');
  const response = await request.put(`${API}/v1/engagements/${crypto.randomUUID()}/confirm`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(response.status()).toBe(403);
});
