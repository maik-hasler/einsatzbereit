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
    data: { name: 'Engagements Test Organisation' },
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
      title: 'Engagement Test Bedarf',
      description: 'Test Beschreibung',
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

async function createEngagementViaApi(
  token: string,
  opportunityId: string,
  request: APIRequestContext,
): Promise<string> {
  const res = await request.post(`${API}/v1/volunteer-opportunities/${opportunityId}/engagements`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { type: 'IndividualContact', message: 'Ich möchte helfen' },
  });
  expect(res.status()).toBe(200);
  const eng = await res.json() as { id: string };
  return eng.id;
}

test.describe('My Engagements page (hannah)', () => {
  test.use({ storageState: 'tests/.auth/hannah.json' });

  test('empty state shows message and explore button', async ({ page }) => {
    await page.goto('/my-engagements');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Noch keine Anmeldungen.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bedarfe erkunden' })).toBeVisible();
  });

  test('engagement appears with Pending status after sign-up', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    await createEngagementViaApi(hannahToken, oppId, request);

    await page.goto('/my-engagements');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Ausstehend')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bedarf anzeigen →' })).toBeVisible();
  });

  test('engagement shows message text', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    await createEngagementViaApi(hannahToken, oppId, request);

    await page.goto('/my-engagements');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('"Ich möchte helfen"')).toBeVisible();
  });

  test('withdraw button changes status to Zurückgezogen', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    await createEngagementViaApi(hannahToken, oppId, request);

    await page.goto('/my-engagements');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Zurückziehen' }).click();

    await expect(page.getByText('Zurückgezogen')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Zurückziehen' })).not.toBeVisible();
  });

  test('"Bedarf anzeigen" button navigates to detail page', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    await createEngagementViaApi(hannahToken, oppId, request);

    await page.goto('/my-engagements');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Bedarf anzeigen →' }).click();

    await expect(page).toHaveURL(`http://localhost:4321/volunteer-opportunities/${oppId}`);
  });
});

test.describe('Engagement Management page (olaf)', () => {
  test.use({ storageState: 'tests/.auth/olaf.json' });

  test('empty state shows no applications message', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}/engagements`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Noch keine Bewerbungen.')).toBeVisible();
  });

  test('pending engagement is listed with confirm and cancel buttons', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    await createEngagementViaApi(hannahToken, oppId, request);

    await page.goto(`/volunteer-opportunities/${oppId}/engagements`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Ausstehend')).toBeVisible();
    await expect(page.getByText('"Ich möchte helfen"')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bestätigen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Absagen' })).toBeVisible();
  });

  test('confirm changes status to Bestätigt', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    await createEngagementViaApi(hannahToken, oppId, request);

    await page.goto(`/volunteer-opportunities/${oppId}/engagements`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Bestätigen' }).click();

    await expect(page.getByText('Bestätigt')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bestätigen' })).not.toBeVisible();
    // Stornieren button appears for confirmed engagements
    await expect(page.getByRole('button', { name: 'Stornieren' })).toBeVisible();
  });

  test('cancel from Pending changes status to Abgesagt', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    await createEngagementViaApi(hannahToken, oppId, request);

    await page.goto(`/volunteer-opportunities/${oppId}/engagements`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Absagen' }).click();

    await expect(page.getByText('Abgesagt')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Absagen' })).not.toBeVisible();
  });

  test('back button navigates away from management page', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}/engagements`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: '← Zurück' }).click();

    await expect(page).not.toHaveURL(/\/engagements/);
  });
});
