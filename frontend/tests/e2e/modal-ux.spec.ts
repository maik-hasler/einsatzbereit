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
    data: { name: 'Modal UX Test Org' },
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
      title: 'Modal UX Test Bedarf',
      description: 'Originalbeschreibung',
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

// ── Sign-up modal ────────────────────────────────────────────────────────────

test.describe('sign-up modal (hannah)', () => {
  test.use({ storageState: 'tests/.auth/hannah.json' });

  test('cancel closes modal without submitting', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Interesse bekunden' }).click();
    await expect(page.getByRole('heading', { name: 'Interesse bekunden' })).toBeVisible();

    await page.getByRole('button', { name: 'Abbrechen' }).click();

    await expect(page.getByRole('heading', { name: 'Interesse bekunden' })).not.toBeVisible();
    await expect(page.getByText('Anmeldung erfolgreich!')).not.toBeVisible();
    // Sign-up button must still be present — no engagement was created
    await expect(page.getByRole('button', { name: 'Interesse bekunden' })).toBeVisible();
  });

  test('modal stays open with error when message is empty', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Interesse bekunden' }).click();
    // Don't fill message, submit directly — HTML required attr blocks submit
    await page.getByRole('button', { name: 'Anmelden' }).click();

    // Modal still visible (browser validation prevents submit)
    await expect(page.getByRole('heading', { name: 'Interesse bekunden' })).toBeVisible();
  });
});

// ── Edit modal ───────────────────────────────────────────────────────────────

test.describe('edit modal (olaf)', () => {
  test.use({ storageState: 'tests/.auth/olaf.json' });

  test('cancel closes edit modal without saving changes', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Bearbeiten' }).click();
    await expect(page.getByRole('heading', { name: 'Bedarf bearbeiten' })).toBeVisible();

    // Change title but then cancel
    const titleInput = page.getByLabel('Titel');
    await titleInput.clear();
    await titleInput.fill('Geänderter Titel (verworfen)');

    await page.getByRole('button', { name: 'Abbrechen' }).click();

    await expect(page.getByRole('heading', { name: 'Bedarf bearbeiten' })).not.toBeVisible();
    // Original title must still be shown
    await expect(page.getByRole('heading', { name: 'Modal UX Test Bedarf' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Geänderter Titel (verworfen)' })).not.toBeVisible();
  });

  test('edit modal closes after successful save', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Bearbeiten' }).click();
    const titleInput = page.getByLabel('Titel');
    await titleInput.clear();
    await titleInput.fill('Gespeicherter Titel');
    await page.getByRole('button', { name: 'Speichern' }).click();

    await expect(page.getByRole('heading', { name: 'Bedarf bearbeiten' })).not.toBeVisible();
    await expect(page.getByRole('heading', { name: 'Gespeicherter Titel' })).toBeVisible();
  });

  test('can re-open edit modal after closing it', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Bearbeiten' }).click();
    await page.getByRole('button', { name: 'Abbrechen' }).click();

    // Open again
    await page.getByRole('button', { name: 'Bearbeiten' }).click();
    await expect(page.getByRole('heading', { name: 'Bedarf bearbeiten' })).toBeVisible();
    // Input should reset to original value
    await expect(page.getByLabel('Titel')).toHaveValue('Modal UX Test Bedarf');
  });

  // ── Delete confirm dialog ──────────────────────────────────────────────────

  test('cancelling delete confirm dialog keeps user on detail page', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    page.on('dialog', (dialog) => dialog.dismiss());
    await page.getByRole('button', { name: 'Löschen' }).click();

    await expect(page).toHaveURL(`http://localhost:4321/volunteer-opportunities/${oppId}`);
    await expect(page.getByRole('heading', { name: 'Modal UX Test Bedarf' })).toBeVisible();
  });

  test('Löschen button re-enabled after cancelling delete dialog', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    page.on('dialog', (dialog) => dialog.dismiss());
    await page.getByRole('button', { name: 'Löschen' }).click();

    await expect(page.getByRole('button', { name: 'Löschen' })).toBeEnabled();
  });
});

// ── Create opportunity modal ──────────────────────────────────────────────────

test.describe('create opportunity modal (olaf)', () => {
  test.use({ storageState: 'tests/.auth/olaf.json' });

  test('closing create modal does not add an opportunity', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    await createOrgViaApi(token, request);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Modal UX Test Org');

    await page.getByTestId('create-opportunity-btn').click();
    await page.locator('#opportunity-title').fill('Soll nicht gespeichert werden');
    await page.getByRole('button', { name: 'Abbrechen' }).click();

    await expect(page.getByText('Soll nicht gespeichert werden')).not.toBeVisible();
  });
});
