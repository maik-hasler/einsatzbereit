import { test, expect } from '@playwright/test';

test('homepage loads and shows heading', async ({ page }) => {
  await page.goto('/');

  await expect(page).toHaveTitle(/Einsatzbereit/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Einsatzbereit');
});
