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
    data: { name: 'Filter Test Organisation' },
  });
  expect(res.status()).toBe(200);
  const org = await res.json() as { id: { value: string } };
  return org.id.value;
}

async function createOpportunityViaApi(
  token: string,
  organizationId: string,
  request: APIRequestContext,
  data: Record<string, unknown>,
): Promise<void> {
  const res = await request.post(`${API}/v1/volunteer-opportunities`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      description: 'Test Beschreibung',
      organizationId,
      street: 'Teststraße',
      houseNumber: '1',
      zipCode: '12345',
      occurrence: 'OneTime',
      participationType: 'Waitlist',
      ...data,
    },
  });
  expect(res.status()).toBe(200);
}

test.describe('search and filter (anonymous)', () => {
  test('keyword search shows matching and hides non-matching', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await createOpportunityViaApi(token, orgId, request, { title: 'Feuerwehr Helfer', city: 'München' });
    await createOpportunityViaApi(token, orgId, request, { title: 'Sanitätsdienst', city: 'Berlin' });

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Feuerwehr Helfer');

    await page.getByPlaceholder('Suche…').fill('Feuerwehr');

    await expect(page.getByText('Feuerwehr Helfer')).toBeVisible();
    await expect(page.getByText('Sanitätsdienst')).not.toBeVisible();
  });

  test('city filter narrows results', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await createOpportunityViaApi(token, orgId, request, { title: 'Berlin Bedarf', city: 'Berlin' });
    await createOpportunityViaApi(token, orgId, request, { title: 'Hamburg Bedarf', city: 'Hamburg' });

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Berlin Bedarf');

    await page.getByPlaceholder('Stadt…').fill('Hamburg');

    await expect(page.getByText('Hamburg Bedarf')).toBeVisible();
    await expect(page.getByText('Berlin Bedarf')).not.toBeVisible();
  });

  test('occurrence dropdown filters to OneTime only', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await createOpportunityViaApi(token, orgId, request, { title: 'Einmaliger Bedarf', city: 'Berlin', occurrence: 'OneTime' });
    await createOpportunityViaApi(token, orgId, request, { title: 'Regelmäßiger Bedarf', city: 'Berlin', occurrence: 'Recurring' });

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Einmaliger Bedarf');

    await page.locator('select').filter({ hasText: 'Alle Häufigkeiten' }).selectOption('OneTime');

    await expect(page.getByText('Einmaliger Bedarf')).toBeVisible();
    await expect(page.getByText('Regelmäßiger Bedarf')).not.toBeVisible();
  });

  test('participationType dropdown filters to IndividualContact only', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await createOpportunityViaApi(token, orgId, request, { title: 'Warteliste Bedarf', city: 'Berlin', participationType: 'Waitlist' });
    await createOpportunityViaApi(token, orgId, request, { title: 'Kontakt Bedarf', city: 'Berlin', participationType: 'IndividualContact' });

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Warteliste Bedarf');

    await page.locator('select').filter({ hasText: 'Alle Typen' }).selectOption('IndividualContact');

    await expect(page.getByText('Kontakt Bedarf')).toBeVisible();
    await expect(page.getByText('Warteliste Bedarf')).not.toBeVisible();
  });

  test('no results message shown when search finds nothing', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await createOpportunityViaApi(token, orgId, request, { title: 'Katastrophenschutz', city: 'Berlin' });

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Katastrophenschutz');

    await page.getByPlaceholder('Suche…').fill('xyzNichtExistentBegriff');

    await expect(page.getByText('Keine Bedarfe gefunden.')).toBeVisible();
  });

  test('clearing search restores full list', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await createOpportunityViaApi(token, orgId, request, { title: 'Feuerwehr Bedarf', city: 'Berlin' });
    await createOpportunityViaApi(token, orgId, request, { title: 'THW Bedarf', city: 'Berlin' });

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Feuerwehr Bedarf');

    const searchInput = page.getByPlaceholder('Suche…');
    await searchInput.fill('Feuerwehr');
    await expect(page.getByText('THW Bedarf')).not.toBeVisible();

    await searchInput.clear();
    await expect(page.getByText('Feuerwehr Bedarf')).toBeVisible();
    await expect(page.getByText('THW Bedarf')).toBeVisible();
  });

  test('combined city and occurrence filters', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await createOpportunityViaApi(token, orgId, request, { title: 'Berlin Einmalig', city: 'Berlin', occurrence: 'OneTime' });
    await createOpportunityViaApi(token, orgId, request, { title: 'Berlin Regulär', city: 'Berlin', occurrence: 'Recurring' });
    await createOpportunityViaApi(token, orgId, request, { title: 'Hamburg Einmalig', city: 'Hamburg', occurrence: 'OneTime' });

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Berlin Einmalig');

    await page.getByPlaceholder('Stadt…').fill('Berlin');
    await page.locator('select').filter({ hasText: 'Alle Häufigkeiten' }).selectOption('OneTime');

    await expect(page.getByText('Berlin Einmalig')).toBeVisible();
    await expect(page.getByText('Berlin Regulär')).not.toBeVisible();
    await expect(page.getByText('Hamburg Einmalig')).not.toBeVisible();
  });
});
