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
    data: { name: 'E2E Test Organisation' },
  });
  expect(res.status()).toBe(200);
  const org = await res.json() as { id: { value: string } };
  return org.id.value;
}

async function createOpportunityViaApi(
  token: string,
  organizationId: string,
  request: APIRequestContext,
  title: string,
): Promise<string> {
  const res = await request.post(`${API}/v1/volunteer-opportunities`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      title,
      description: 'E2E test description',
      organizationId,
      street: 'Teststraße',
      houseNumber: '1',
      zipCode: '12345',
      city: 'Berlin',
      occurrence: 'OneTime',
      participationType: 'Waitlist',
    },
  });
  expect(res.status()).toBe(200);
  const opp = await res.json() as { id: string };
  return opp.id;
}

test('GET /v1/volunteer-opportunities without auth returns empty list', async ({ request }) => {
  const response = await request.get(`${API}/v1/volunteer-opportunities?pageNumber=1&pageSize=10`);
  expect(response.status()).toBe(200);
  const data = await response.json() as { items: unknown[]; totalItems: number };
  expect(data.items).toHaveLength(0);
  expect(data.totalItems).toBe(0);
});

test.describe('as organisator (olaf)', () => {
  test.use({ storageState: 'tests/.auth/olaf.json' });

  test('organisator creates opportunity via UI and API confirms it', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    await createOrgViaApi(token, request);

    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await page.waitForSelector('text=E2E Test Organisation');

    await page.getByTestId('create-opportunity-btn').click();
    await page.locator('#opportunity-title').fill('Katastrophenschutz Helfer');
    await page.locator('#opportunity-description').fill('Unterstützung bei Übungen');
    await page.getByPlaceholder('Musterstraße').fill('Hauptstraße');
    await page.getByPlaceholder('1a').fill('12');
    await page.getByPlaceholder('12345').fill('10115');
    await page.getByPlaceholder('Berlin').fill('Berlin');
    await page.getByTestId('modal-submit').click();

    await expect(page.locator('#opportunity-title')).not.toBeVisible();

    const response = await request.get(`${API}/v1/volunteer-opportunities?pageNumber=1&pageSize=10`);
    expect(response.status()).toBe(200);
    const data = await response.json() as { items: Array<{ title: string; occurrence: string; organizationName: string }> };
    const created = data.items.find((i) => i.title === 'Katastrophenschutz Helfer');
    expect(created).toBeDefined();
    if (!created) return;
    expect(created.occurrence).toBe('OneTime');
    expect(created.organizationName).toBe('E2E Test Organisation');
  });
});

test('pagination returns correct page slices', async ({ request }) => {
  const token = await getToken('olaf', 'olaf123');
  const orgId = await createOrgViaApi(token, request);

  await createOpportunityViaApi(token, orgId, request, 'Bedarf 1');
  await createOpportunityViaApi(token, orgId, request, 'Bedarf 2');
  await createOpportunityViaApi(token, orgId, request, 'Bedarf 3');

  const page1 = await request.get(`${API}/v1/volunteer-opportunities?pageNumber=1&pageSize=2`);
  expect(page1.status()).toBe(200);
  const data1 = await page1.json() as { items: unknown[]; totalItems: number; pageCount: number };
  expect(data1.items).toHaveLength(2);
  expect(data1.totalItems).toBe(3);
  expect(data1.pageCount).toBe(2);

  const page2 = await request.get(`${API}/v1/volunteer-opportunities?pageNumber=2&pageSize=2`);
  expect(page2.status()).toBe(200);
  const data2 = await page2.json() as { items: unknown[] };
  expect(data2.items).toHaveLength(1);
});
