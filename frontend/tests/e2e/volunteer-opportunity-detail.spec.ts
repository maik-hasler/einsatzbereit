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
    data: { name: 'Detail Test Organisation' },
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
      title: 'Katastrophenschutz Helfer',
      description: 'Unterstützung bei Katastrophenschutzeinsätzen',
      organizationId,
      street: 'Hauptstraße',
      houseNumber: '1',
      zipCode: '10115',
      city: 'Berlin',
      occurrence: 'OneTime',
      participationType: 'IndividualContact',
      ...overrides,
    },
  });
  expect(res.status()).toBe(200);
  const opp = await res.json() as { id: string };
  return opp.id;
}

test('detail page loads opportunity data', async ({ page, request }) => {
  const token = await getToken('olaf', 'olaf123');
  const orgId = await createOrgViaApi(token, request);
  const oppId = await createOpportunityViaApi(token, orgId, request);

  await page.goto(`/volunteer-opportunities/${oppId}`);
  await page.waitForLoadState('networkidle');

  await expect(page.getByRole('heading', { name: 'Katastrophenschutz Helfer' })).toBeVisible();
  await expect(page.getByText('Detail Test Organisation')).toBeVisible();
  await expect(page.getByText('Unterstützung bei Katastrophenschutzeinsätzen')).toBeVisible();
  await expect(page.getByText('Einmalig')).toBeVisible();
  await expect(page.getByText('Einzelkontakt')).toBeVisible();
});

test('unauthenticated user sees sign-in prompt instead of sign-up button', async ({ page, request }) => {
  const token = await getToken('olaf', 'olaf123');
  const orgId = await createOrgViaApi(token, request);
  const oppId = await createOpportunityViaApi(token, orgId, request);

  await page.goto(`/volunteer-opportunities/${oppId}`);
  await page.waitForLoadState('networkidle');

  await expect(page.getByRole('button', { name: 'anmelden' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Interesse bekunden' })).not.toBeVisible();
});

test.describe('as user (hannah)', () => {
  test.use({ storageState: 'tests/.auth/hannah.json' });

  test('sign-up button visible for IndividualContact opportunity', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('button', { name: 'Interesse bekunden' })).toBeVisible();
  });

  test('sign-up modal submits and shows success message', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Interesse bekunden' }).click();
    await expect(page.getByRole('heading', { name: 'Interesse bekunden' })).toBeVisible();

    await page.getByPlaceholder('Beschreibe kurz, warum du dich engagieren möchtest…').fill('Ich möchte helfen und mich einbringen.');
    await page.getByRole('button', { name: 'Anmelden' }).click();

    await expect(page.getByText('Anmeldung erfolgreich!')).toBeVisible();
  });

  test('sign-up button hidden after successful sign-up', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Interesse bekunden' }).click();
    await page.getByPlaceholder('Beschreibe kurz, warum du dich engagieren möchtest…').fill('Möchte helfen.');
    await page.getByRole('button', { name: 'Anmelden' }).click();

    await expect(page.getByRole('button', { name: 'Interesse bekunden' })).not.toBeVisible();
  });
});

test.describe('as organisator (olaf)', () => {
  test.use({ storageState: 'tests/.auth/olaf.json' });

  test('edit and delete buttons visible, sign-up button hidden', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('button', { name: 'Bearbeiten' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Löschen' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Interesse bekunden' })).not.toBeVisible();
  });

  test('edit modal updates title and reflects on page', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Bearbeiten' }).click();
    await expect(page.getByRole('heading', { name: 'Bedarf bearbeiten' })).toBeVisible();

    const titleInput = page.getByLabel('Titel');
    await titleInput.clear();
    await titleInput.fill('Aktualisierter Titel');
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(page.getByRole('heading', { name: 'Aktualisierter Titel' })).toBeVisible();
  });

  test('delete navigates to home after browser confirm', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    page.on('dialog', (dialog) => dialog.accept());
    await page.getByRole('button', { name: 'Löschen' }).click();

    await expect(page).toHaveURL('http://localhost:4321/');
  });

  test('manage engagements link navigates to engagement management page', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Bewerbungen verwalten →' }).click();

    await expect(page).toHaveURL(`http://localhost:4321/volunteer-opportunities/${oppId}/engagements`);
    await expect(page.getByRole('heading', { name: 'Bewerbungen verwalten' })).toBeVisible();
  });
});
