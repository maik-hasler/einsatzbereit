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
    data: { name: 'Navigation Test Org' },
  });
  expect(res.status()).toBe(200);
  const org = await res.json() as { id: { value: string } };
  return org.id.value;
}

async function createOpportunityViaApi(
  token: string,
  organizationId: string,
  request: APIRequestContext,
  title = 'Navigation Test Bedarf',
): Promise<string> {
  const res = await request.post(`${API}/v1/volunteer-opportunities`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      title,
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

test('clicking an opportunity in the list navigates to its detail page', async ({ page, request }) => {
  const token = await getToken('olaf', 'olaf123');
  const orgId = await createOrgViaApi(token, request);
  const oppId = await createOpportunityViaApi(token, orgId, request);

  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await page.waitForSelector('text=Navigation Test Bedarf');

  await page.getByText('Navigation Test Bedarf').click();

  await expect(page).toHaveURL(`http://localhost:4321/volunteer-opportunities/${oppId}`);
  await expect(page.getByRole('heading', { name: 'Navigation Test Bedarf' })).toBeVisible();
});

test('header brand logo navigates back to home from detail page', async ({ page, request }) => {
  const token = await getToken('olaf', 'olaf123');
  const orgId = await createOrgViaApi(token, request);
  const oppId = await createOpportunityViaApi(token, orgId, request);

  await page.goto(`/volunteer-opportunities/${oppId}`);
  await page.waitForLoadState('networkidle');

  await page.getByRole('link', { name: 'Einsatzbereit' }).click();

  await expect(page).toHaveURL('http://localhost:4321/');
});

test('back button on detail page returns to home', async ({ page, request }) => {
  const token = await getToken('olaf', 'olaf123');
  const orgId = await createOrgViaApi(token, request);
  const oppId = await createOpportunityViaApi(token, orgId, request);

  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await page.waitForSelector('text=Navigation Test Bedarf');
  await page.getByText('Navigation Test Bedarf').click();
  await expect(page).toHaveURL(`http://localhost:4321/volunteer-opportunities/${oppId}`);

  await page.getByRole('button', { name: '← Zurück' }).click();

  await expect(page).toHaveURL('http://localhost:4321/');
});

test('back button on engagement management page returns to detail page', async ({ page, request }) => {
  const token = await getToken('olaf', 'olaf123');
  const orgId = await createOrgViaApi(token, request);
  const oppId = await createOpportunityViaApi(token, orgId, request);

  await page.goto(`/volunteer-opportunities/${oppId}/engagements`);
  await page.waitForLoadState('networkidle');

  await page.getByRole('button', { name: '← Zurück' }).click();

  await expect(page).toHaveURL(`http://localhost:4321/volunteer-opportunities/${oppId}`);
});

test.describe('as user (hannah)', () => {
  test.use({ storageState: 'tests/.auth/hannah.json' });

  test('"Meine Engagements" in user dropdown navigates to my-engagements', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Benutzermenü' }).click();
    await page.getByRole('link', { name: 'Meine Engagements' }).click();

    await expect(page).toHaveURL('http://localhost:4321/my-engagements');
    await expect(page.getByRole('heading', { name: 'Meine Engagements' })).toBeVisible();
  });

  test('"Bedarfe erkunden" on my-engagements empty state navigates home', async ({ page }) => {
    await page.goto('/my-engagements');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Bedarfe erkunden' }).click();

    await expect(page).toHaveURL('http://localhost:4321/');
  });
});

test.describe('as organisator (olaf)', () => {
  test.use({ storageState: 'tests/.auth/olaf.json' });

  test('"Bewerbungen verwalten" link on detail page navigates to management page', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    const oppId = await createOpportunityViaApi(token, orgId, request);

    await page.goto(`/volunteer-opportunities/${oppId}`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Bewerbungen verwalten →' }).click();

    await expect(page).toHaveURL(`http://localhost:4321/volunteer-opportunities/${oppId}/engagements`);
  });
});
