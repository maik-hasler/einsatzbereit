import { test, expect } from '@playwright/test';
import { getToken } from '../helpers/auth';
import { resetState } from '../helpers/cleanup';

const API = 'http://localhost:5000';

test.beforeEach(async () => {
  await resetState();
});

test('GET /v1/organizations without token returns 401', async ({ request }) => {
  const response = await request.get(`${API}/v1/organizations`);
  expect(response.status()).toBe(401);
});

test.describe('as organisator (olaf)', () => {
  test.use({ storageState: 'tests/.auth/olaf.json' });

  test('organisator creates organization via UI and API confirms it', async ({ page, request }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // After reset + login: no orgs → OrganizationSwitcher shows inline "Organisation erstellen" button
    await page.getByTestId('create-org-btn').click();
    await page.getByPlaceholder('z.B. Freiwillige Feuerwehr Musterstadt').fill('Freiwillige Feuerwehr Test');
    await page.getByTestId('modal-submit').click();

    // Modal closes, org switcher updates
    await expect(page.getByPlaceholder('z.B. Freiwillige Feuerwehr Musterstadt')).not.toBeVisible();

    const token = await getToken('olaf', 'olaf123');
    const response = await request.get(`${API}/v1/organizations`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(response.status()).toBe(200);
    const orgs = await response.json() as Array<{ name: string }>;
    expect(orgs.some((o) => o.name === 'Freiwillige Feuerwehr Test')).toBe(true);
  });
});

test.describe('as user (hannah)', () => {
  test.use({ storageState: 'tests/.auth/hannah.json' });

  test('user without organisator role does not see Bedarf erstellen button', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    await page.waitForSelector('text=Keine Bedarfe gefunden.');
    await expect(page.getByTestId('create-opportunity-btn')).not.toBeVisible();
  });
});
