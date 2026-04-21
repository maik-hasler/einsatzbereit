import { test, expect, type APIRequestContext } from '@playwright/test';
import { getToken } from '../helpers/auth';
import { resetState } from '../helpers/cleanup';

const API = 'http://localhost:5000';

// The list component hardcodes pageSize=10. 11 opportunities forces a second page.
const PAGE_SIZE = 10;
const TOTAL = PAGE_SIZE + 1;

test.beforeEach(async () => {
  await resetState();
});

async function createOrgViaApi(token: string, request: APIRequestContext): Promise<string> {
  const res = await request.post(`${API}/v1/organizations`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { name: 'Pagination Test Org' },
  });
  expect(res.status()).toBe(200);
  const org = await res.json() as { id: { value: string } };
  return org.id.value;
}

async function seedOpportunities(
  token: string,
  orgId: string,
  request: APIRequestContext,
  count: number,
): Promise<void> {
  for (let i = 1; i <= count; i++) {
    const res = await request.post(`${API}/v1/volunteer-opportunities`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        title: `Pagination Bedarf ${i}`,
        description: 'Test',
        organizationId: orgId,
        street: 'Teststraße',
        houseNumber: `${i}`,
        zipCode: '12345',
        city: 'Berlin',
        occurrence: 'OneTime',
        participationType: 'Waitlist',
      },
    });
    expect(res.status()).toBe(200);
  }
}

test.describe('pagination UI controls', () => {
  test('prev button is disabled on the first page', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await seedOpportunities(token, orgId, request, TOTAL);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Pagination Bedarf 1');

    await expect(page.getByRole('button', { name: '← Zurück' })).toBeDisabled();
  });

  test('next button is enabled on the first page when more pages exist', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await seedOpportunities(token, orgId, request, TOTAL);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Pagination Bedarf 1');

    await expect(page.getByRole('button', { name: 'Weiter →' })).toBeEnabled();
  });

  test('page indicator shows "1 / 2" on first page with 11 items', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await seedOpportunities(token, orgId, request, TOTAL);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Pagination Bedarf 1');

    await expect(page.getByText('1 / 2')).toBeVisible();
  });

  test('clicking "Weiter →" shows page 2 items', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await seedOpportunities(token, orgId, request, TOTAL);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Pagination Bedarf 1');

    await page.getByRole('button', { name: 'Weiter →' }).click();
    await page.waitForLoadState('networkidle');

    // Page 2 has exactly 1 item (the 11th); page 1 items gone
    await expect(page.getByText(`Pagination Bedarf ${TOTAL}`)).toBeVisible();
    await expect(page.getByText('Pagination Bedarf 1')).not.toBeVisible();
  });

  test('page indicator updates to "2 / 2" after navigating to page 2', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await seedOpportunities(token, orgId, request, TOTAL);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Pagination Bedarf 1');

    await page.getByRole('button', { name: 'Weiter →' }).click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('2 / 2')).toBeVisible();
  });

  test('"Weiter →" is disabled on the last page', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await seedOpportunities(token, orgId, request, TOTAL);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Pagination Bedarf 1');

    await page.getByRole('button', { name: 'Weiter →' }).click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('button', { name: 'Weiter →' })).toBeDisabled();
  });

  test('"← Zurück" is enabled on the last page', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await seedOpportunities(token, orgId, request, TOTAL);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Pagination Bedarf 1');

    await page.getByRole('button', { name: 'Weiter →' }).click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('button', { name: '← Zurück' })).toBeEnabled();
  });

  test('clicking "← Zurück" from page 2 returns to page 1', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await seedOpportunities(token, orgId, request, TOTAL);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Pagination Bedarf 1');

    await page.getByRole('button', { name: 'Weiter →' }).click();
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: '← Zurück' }).click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Pagination Bedarf 1')).toBeVisible();
    await expect(page.getByText('1 / 2')).toBeVisible();
  });

  test('pagination controls not shown when all items fit on one page', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    // Only 3 items — single page, no controls
    await seedOpportunities(token, orgId, request, 3);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Pagination Bedarf 1');

    await expect(page.getByRole('button', { name: 'Weiter →' })).not.toBeVisible();
    await expect(page.getByRole('button', { name: '← Zurück' })).not.toBeVisible();
  });

  test('search filter resets to page 1', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const orgId = await createOrgViaApi(token, request);
    await seedOpportunities(token, orgId, request, TOTAL);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=Pagination Bedarf 1');

    // Navigate to page 2
    await page.getByRole('button', { name: 'Weiter →' }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('2 / 2')).toBeVisible();

    // Apply a search filter — should reset to page 1
    await page.getByPlaceholder('Suche…').fill('Pagination Bedarf');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('1 /')).toBeVisible();
  });
});
