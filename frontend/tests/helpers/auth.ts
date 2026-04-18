import type { Page } from '@playwright/test';

const TOKEN_URL = 'http://localhost:8080/realms/einsatzbereit/protocol/openid-connect/token';

export async function getToken(username: string, password: string): Promise<string> {
  const res = await fetch(TOKEN_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: 'frontend',
      username,
      password,
    }),
  });
  const data = await res.json() as { access_token: string };
  return data.access_token;
}

export async function loginAs(page: Page, username: string, password: string): Promise<void> {
  await page.goto('/');
  await page.getByRole('button', { name: 'Anmelden' }).first().click();
  await page.waitForURL('**/realms/einsatzbereit/**');
  await page.fill('#username', username);
  await page.fill('#password', password);
  await page.click('#kc-login');
  // onSigninCallback calls window.location.replace('/') after token exchange completes
  await page.waitForURL('http://localhost:4321/', { timeout: 30000 });
  await page.waitForSelector('h1', { timeout: 15000 });
}
