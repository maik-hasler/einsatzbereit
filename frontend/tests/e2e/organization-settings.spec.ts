import { test, expect } from '@playwright/test';
import { getToken } from '../helpers/auth';
import { resetState } from '../helpers/cleanup';

const API = 'http://localhost:5000';

test.beforeEach(async () => {
  await resetState();
});

test.describe('organization settings', () => {
  test.use({ storageState: 'tests/.auth/olaf.json' });

  test('settings link appears in org switcher after creating an org', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Create an org first
    await page.getByTestId('create-org-btn').click();
    await page.getByPlaceholder('z.B. Freiwillige Feuerwehr Musterstadt').fill('Testorganisation');
    await page.getByTestId('modal-submit').click();
    await expect(page.getByPlaceholder('z.B. Freiwillige Feuerwehr Musterstadt')).not.toBeVisible();

    // Open the org switcher dropdown
    await page.getByRole('button', { name: 'Organisation wechseln' }).click();

    // Settings link should be visible
    await expect(page.getByTestId('org-settings-link')).toBeVisible();
  });

  test('settings link navigates to settings page', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    await page.getByTestId('create-org-btn').click();
    await page.getByPlaceholder('z.B. Freiwillige Feuerwehr Musterstadt').fill('Nav Test Org');
    await page.getByTestId('modal-submit').click();
    await expect(page.getByPlaceholder('z.B. Freiwillige Feuerwehr Musterstadt')).not.toBeVisible();

    await page.getByRole('button', { name: 'Organisation wechseln' }).click();
    await page.getByTestId('org-settings-link').click();

    // Should navigate to settings page
    await expect(page).toHaveURL(/\/organizations\/.+\/settings/);
    await expect(page.getByRole('heading', { name: 'Nav Test Org' })).toBeVisible();
  });

  test('settings page shows tabs for Allgemein and Mitglieder', async ({ page, request }) => {
    // Create org via API for speed
    const token = await getToken('olaf', 'olaf123');
    const res = await request.post(`${API}/v1/organizations`, {
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      data: { name: 'Einstellungen Org' },
    });
    const org = await res.json() as { id: { value: string } };

    await page.goto(`/organizations/${org.id.value}/settings`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('button', { name: 'Allgemein' })).toBeVisible();
    await expect(page.getByRole('button', { name: /Mitglieder/ })).toBeVisible();
  });

  test('can update organization name and details', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const res = await request.post(`${API}/v1/organizations`, {
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      data: { name: 'Alter Name' },
    });
    const org = await res.json() as { id: { value: string } };

    await page.goto(`/organizations/${org.id.value}/settings`);
    await page.waitForLoadState('networkidle');

    // Update name
    const nameInput = page.getByLabel('Name *');
    await nameInput.clear();
    await nameInput.fill('Neuer Name');

    // Update description
    await page.getByLabel('Beschreibung').fill('Unsere Beschreibung');

    await page.getByRole('button', { name: 'Speichern' }).click();

    // Success message should appear
    await expect(page.getByText('Änderungen gespeichert.')).toBeVisible();

    // Verify via API
    const details = await request.get(`${API}/v1/organizations/${org.id.value}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    const data = await details.json() as { name: string; description: string };
    expect(data.name).toBe('Neuer Name');
    expect(data.description).toBe('Unsere Beschreibung');
  });

  test('Mitglieder tab shows creator as organisator', async ({ page, request }) => {
    const token = await getToken('olaf', 'olaf123');
    const res = await request.post(`${API}/v1/organizations`, {
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      data: { name: 'Mitglieder Test' },
    });
    const org = await res.json() as { id: { value: string } };

    await page.goto(`/organizations/${org.id.value}/settings`);
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: /Mitglieder/ }).click();

    // olaf is the creator and should be listed as Organisator
    await expect(page.getByText('olaf')).toBeVisible();
    await expect(page.getByText('Organisator')).toBeVisible();
  });

  test('settings page shows 404 message for unknown org', async ({ page }) => {
    await page.goto(`/organizations/${crypto.randomUUID()}/settings`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('Organisation nicht gefunden.')).toBeVisible();
  });
});

test.describe('settings not accessible to plain user', () => {
  test.use({ storageState: 'tests/.auth/hannah.json' });

  test('GET /v1/organizations/{id} returns 403 for user without organisator role', async ({ request }) => {
    const token = await getToken('hannah', 'hannah123');
    const response = await request.get(`${API}/v1/organizations/${crypto.randomUUID()}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(response.status()).toBe(403);
  });
});
