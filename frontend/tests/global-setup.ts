import { chromium } from '@playwright/test';
import { execSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';

const ROOT = resolve(process.cwd(), '..');
const AUTH_DIR = resolve(process.cwd(), 'tests/.auth');

export default async function globalSetup() {
  if (!process.env.CI) {
    execSync('docker compose up -d --build', { cwd: ROOT, stdio: 'inherit' });
  }
  await Promise.all([
    waitFor('http://localhost:8080/realms/einsatzbereit/.well-known/openid-configuration', 'Keycloak'),
    waitFor('http://localhost:4321', 'Frontend'),
    waitFor('http://localhost:5000/v1/volunteer-opportunities?pageNumber=1&pageSize=1', 'Backend'),
  ]);

  await ensureKeycloakCors();

  mkdirSync(AUTH_DIR, { recursive: true });
  const browser = await chromium.launch();
  await saveAuthState(browser, 'olaf', 'olaf123', `${AUTH_DIR}/olaf.json`);
  await saveAuthState(browser, 'hannah', 'hannah123', `${AUTH_DIR}/hannah.json`);
  await browser.close();
}

async function saveAuthState(
  browser: Awaited<ReturnType<typeof chromium.launch>>,
  username: string,
  password: string,
  storagePath: string,
): Promise<void> {
  const context = await browser.newContext();
  const page = await context.newPage();

  await page.goto('http://localhost:4321/');
  await page.getByRole('button', { name: 'Anmelden' }).first().click();
  await page.waitForURL('**/realms/einsatzbereit/**');
  await page.fill('#username', username);
  await page.fill('#password', password);
  await page.click('#kc-login');
  await page.waitForURL('http://localhost:4321/', { timeout: 60000 });
  await page.waitForSelector('h1', { timeout: 15000 });
  await context.storageState({ path: storagePath });
  await context.close();
}

async function ensureKeycloakCors(): Promise<void> {
  const MASTER_TOKEN_URL = 'http://localhost:8080/realms/master/protocol/openid-connect/token';
  const ADMIN_CLIENTS_URL = 'http://localhost:8080/admin/realms/einsatzbereit/clients';

  const tokenRes = await fetch(MASTER_TOKEN_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: 'admin-cli',
      username: 'admin',
      password: 'admin',
    }),
  });
  const { access_token } = await tokenRes.json() as { access_token: string };

  const clientsRes = await fetch(`${ADMIN_CLIENTS_URL}?clientId=frontend`, {
    headers: { Authorization: `Bearer ${access_token}` },
  });
  const clients = await clientsRes.json() as Array<Record<string, unknown>>;
  const client = clients[0];

  // Patch webOrigins — realm JSON has "http://localhost:*" which Keycloak ignores for CORS.
  // Must use "*" (allow all) or explicit origins; the postgres volume may cache the old realm.
  const updated = { ...client, webOrigins: ['*'] };
  await fetch(`${ADMIN_CLIENTS_URL}/${client.id as string}`, {
    method: 'PUT',
    headers: { Authorization: `Bearer ${access_token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify(updated),
  });
}

async function waitFor(url: string, name: string, timeoutMs = 120_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const res = await fetch(url);
      if (res.ok) return;
    } catch {
      // not ready yet
    }
    await new Promise((r) => setTimeout(r, 2000));
  }
  throw new Error(`${name} did not become ready within timeout`);
}
