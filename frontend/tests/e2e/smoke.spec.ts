import { test, expect } from '@playwright/test';
import { loginAs } from '../helpers/auth';
import { resetState } from '../helpers/cleanup';

test.beforeEach(async () => {
  await resetState();
});

test('homepage loads and auth flow completes', async ({ page }) => {
  await loginAs(page, 'hannah', 'hannah123');

  await expect(page).toHaveTitle(/Einsatzbereit/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Einsatzbereit');
  // Confirm auth completed — login button must be gone
  await expect(page.getByRole('button', { name: 'Anmelden' })).not.toBeVisible();
});
