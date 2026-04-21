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
    data: { name: 'Lifecycle Test Org' },
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
      title: 'Lifecycle Test Bedarf',
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

async function confirmEngagementViaApi(
  token: string,
  engagementId: string,
  request: APIRequestContext,
): Promise<void> {
  const res = await request.put(`${API}/v1/engagements/${engagementId}/confirm`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(res.status()).toBe(200);
}

// ── Confirmed engagement on My Engagements page ──────────────────────────────

test.describe('My Engagements — Confirmed state (hannah)', () => {
  test.use({ storageState: 'tests/.auth/hannah.json' });

  test('confirmed engagement shows "Bestätigt" status badge', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    const engId = await createEngagementViaApi(hannahToken, oppId, request);
    await confirmEngagementViaApi(olafToken, engId, request);

    await page.goto('/my-engagements');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Bestätigt')).toBeVisible();
  });

  test('withdraw button visible for Confirmed engagement', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    const engId = await createEngagementViaApi(hannahToken, oppId, request);
    await confirmEngagementViaApi(olafToken, engId, request);

    await page.goto('/my-engagements');
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('button', { name: 'Zurückziehen' })).toBeVisible();
  });

  test('withdrawing a Confirmed engagement changes status to Zurückgezogen', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    const engId = await createEngagementViaApi(hannahToken, oppId, request);
    await confirmEngagementViaApi(olafToken, engId, request);

    await page.goto('/my-engagements');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Zurückziehen' }).click();

    await expect(page.getByText('Zurückgezogen')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Zurückziehen' })).not.toBeVisible();
  });
});

// ── Stornieren (cancel confirmed) from management page ───────────────────────

test.describe('Engagement Management — Stornieren (olaf)', () => {
  test.use({ storageState: 'tests/.auth/olaf.json' });

  test('Stornieren button visible after confirming an engagement', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    const engId = await createEngagementViaApi(hannahToken, oppId, request);
    await confirmEngagementViaApi(olafToken, engId, request);

    await page.goto(`/volunteer-opportunities/${oppId}/engagements`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Bestätigt')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Stornieren' })).toBeVisible();
    // Bestätigen + Absagen buttons gone for confirmed engagements
    await expect(page.getByRole('button', { name: 'Bestätigen' })).not.toBeVisible();
  });

  test('Stornieren changes status to Abgesagt', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    const engId = await createEngagementViaApi(hannahToken, oppId, request);
    await confirmEngagementViaApi(olafToken, engId, request);

    await page.goto(`/volunteer-opportunities/${oppId}/engagements`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Stornieren' }).click();

    await expect(page.getByText('Abgesagt')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Stornieren' })).not.toBeVisible();
  });
});

// ── Withdrawn engagement cannot be re-withdrawn ───────────────────────────────

test.describe('Withdrawn engagement (hannah)', () => {
  test.use({ storageState: 'tests/.auth/hannah.json' });

  test('Zurückziehen button hidden for Withdrawn engagement', async ({ page, request }) => {
    const olafToken = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(olafToken, request);
    const oppId = await createOpportunityViaApi(olafToken, orgId, request);

    const hannahToken = await getToken('hannah', 'hannah123');
    const engId = await createEngagementViaApi(hannahToken, oppId, request);

    // Withdraw via API directly to set up state
    await request.put(`${API}/v1/engagements/${engId}/withdraw`, {
      headers: { Authorization: `Bearer ${hannahToken}` },
    });

    await page.goto('/my-engagements');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Zurückgezogen')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Zurückziehen' })).not.toBeVisible();
  });
});

// ── Full round-trip via UI ────────────────────────────────────────────────────

test('full round-trip: sign up via UI → confirm via API → stornieren via UI', async ({ browser, request }) => {
  const olafToken = await getToken('olaf', 'olaf123');
  const orgId = await request.post(`${API}/v1/organizations`, {
    headers: { Authorization: `Bearer ${olafToken}` },
    data: { name: 'Round Trip Org' },
  }).then(r => r.json()).then((o: { id: { value: string } }) => o.id.value);

  const oppId = await request.post(`${API}/v1/volunteer-opportunities`, {
    headers: { Authorization: `Bearer ${olafToken}` },
    data: {
      title: 'Round Trip Bedarf',
      description: 'Test',
      organizationId: orgId,
      street: 'Str',
      houseNumber: '1',
      zipCode: '12345',
      city: 'Berlin',
      occurrence: 'OneTime',
      participationType: 'IndividualContact',
    },
  }).then(r => r.json()).then((o: { id: string }) => o.id);

  // Hannah signs up via UI
  const hannahContext = await browser.newContext({ storageState: 'tests/.auth/hannah.json' });
  const hannahPage = await hannahContext.newPage();
  await hannahPage.goto(`/volunteer-opportunities/${oppId}`);
  await hannahPage.waitForLoadState('networkidle');
  await hannahPage.getByRole('button', { name: 'Interesse bekunden' }).click();
  await hannahPage.getByPlaceholder('Beschreibe kurz, warum du dich engagieren möchtest…').fill('Ich bin dabei!');
  await hannahPage.getByRole('button', { name: 'Anmelden' }).click();
  await expect(hannahPage.getByText('Anmeldung erfolgreich!')).toBeVisible();
  await hannahContext.close();

  // Olaf gets the engagement ID via API
  const engagementsRes = await request.get(`${API}/v1/volunteer-opportunities/${oppId}/engagements`, {
    headers: { Authorization: `Bearer ${olafToken}` },
  });
  const engagements = await engagementsRes.json() as Array<{ id: string; status: string }>;
  expect(engagements).toHaveLength(1);
  const engId = engagements[0].id;
  expect(engagements[0].status).toBe('Pending');

  // Olaf confirms via API
  await confirmEngagementViaApi(olafToken, engId, request);

  // Olaf storniert via UI
  const olafContext = await browser.newContext({ storageState: 'tests/.auth/olaf.json' });
  const olafPage = await olafContext.newPage();
  await olafPage.goto(`/volunteer-opportunities/${oppId}/engagements`);
  await olafPage.waitForLoadState('networkidle');
  await expect(olafPage.getByText('Bestätigt')).toBeVisible();
  await olafPage.getByRole('button', { name: 'Stornieren' }).click();
  await expect(olafPage.getByText('Abgesagt')).toBeVisible();
  await olafContext.close();
});
