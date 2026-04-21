import { test, expect, type APIRequestContext } from '@playwright/test';
import { getToken } from '../helpers/auth';
import { resetState } from '../helpers/cleanup';
import { seedTimeSlot } from '../helpers/db';

const API = 'http://localhost:5000';

test.beforeEach(async () => {
  await resetState();
});

async function createOrgViaApi(token: string, request: APIRequestContext): Promise<string> {
  const res = await request.post(`${API}/v1/organizations`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { name: 'Waitlist Test Org' },
  });
  expect(res.status()).toBe(200);
  const org = await res.json() as { id: { value: string } };
  return org.id.value;
}

async function createOpportunityViaApi(
  token: string,
  organizationId: string,
  request: APIRequestContext,
  overrides: Record<string, unknown> = {},
): Promise<string> {
  const res = await request.post(`${API}/v1/volunteer-opportunities`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      title: 'Waitlist Test Bedarf',
      description: 'Test Beschreibung',
      organizationId,
      street: 'Teststraße',
      houseNumber: '1',
      zipCode: '12345',
      city: 'Berlin',
      occurrence: 'OneTime',
      participationType: 'Waitlist',
      ...overrides,
    },
  });
  expect(res.status()).toBe(200);
  const opp = await res.json() as { id: string };
  return opp.id;
}

// ── Waitlist opportunity type ────────────────────────────────────────────────

test.describe('as user (hannah)', () => {
  test.use({ storageState: 'tests/.auth/hannah.json' });

  test('Waitlist opportunity shows "Auf Warteliste eintragen" button', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('button', { name: 'Auf Warteliste eintragen' })).toBeVisible();
  });

  test('sign-up modal heading matches "Auf Warteliste eintragen" for Waitlist type', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Auf Warteliste eintragen' }).click();

    await expect(page.getByRole('heading', { name: 'Auf Warteliste eintragen' })).toBeVisible();
  });

  test('Waitlist modal shows "Keine Zeitslots" and disabled submit when no slots exist', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Auf Warteliste eintragen' }).click();

    await expect(page.getByText('Keine Zeitslots verfügbar.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Anmelden' })).toBeDisabled();
  });

  test('Waitlist modal shows time slot select when slots exist', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    // Seed a time slot directly via DB since no API endpoint exists yet
    await seedTimeSlot(
      oppId,
      '2027-06-01T09:00:00+00:00',
      '2027-06-01T17:00:00+00:00',
      10,
    );

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Auf Warteliste eintragen' }).click();

    await expect(page.locator('select')).toBeVisible();
    // At least one real option beyond the placeholder
    const optionCount = await page.locator('select option').count();
    expect(optionCount).toBeGreaterThan(1);
  });

  test('Waitlist sign-up with time slot succeeds', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await seedTimeSlot(
      oppId,
      '2027-06-01T09:00:00+00:00',
      '2027-06-01T17:00:00+00:00',
      10,
    );

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Auf Warteliste eintragen' }).click();
    // Select first real option (index 1 skips the placeholder)
    await page.locator('select').selectOption({ index: 1 });
    await page.getByRole('button', { name: 'Anmelden' }).click();

    await expect(page.getByText('Anmeldung erfolgreich!')).toBeVisible();
  });
});

// ── Waitlist badge in list ───────────────────────────────────────────────────

test('Waitlist opportunity shows "Warteliste" badge in list', async ({ request, page }) => {
  const token = await getToken('olaf', 'olaf123');
  const orgId = await createOrgViaApi(token, request);
  await createOpportunityViaApi(token, orgId, request);

  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await page.waitForSelector('text=Waitlist Test Bedarf');

  await expect(page.getByText('Warteliste')).toBeVisible();
});

test('Recurring opportunity shows "Regelmäßig" badge in list', async ({ request, page }) => {
  const token = await getToken('olaf', 'olaf123');
  const orgId = await createOrgViaApi(token, request);
  await createOpportunityViaApi(token, orgId, request, {
    title: 'Recurring Test Bedarf',
    occurrence: 'Recurring',
  });

  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await page.waitForSelector('text=Recurring Test Bedarf');

  await expect(page.getByText('Regelmäßig')).toBeVisible();
});

test('Waitlist opportunity shows "Warteliste" badge on detail page', async ({ request, page }) => {
  const token = await getToken('olaf', 'olaf123');
  const orgId = await createOrgViaApi(token, request);
  const oppId = await createOpportunityViaApi(token, orgId, request);

  await page.goto(`/volunteer-opportunities/${oppId}`);
  await page.waitForLoadState('networkidle');

  await expect(page.getByText('Warteliste')).toBeVisible();
});
