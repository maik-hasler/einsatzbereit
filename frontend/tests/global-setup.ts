import { chromium } from '@playwright/test';
import { execSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';

const ROOT = resolve(process.cwd(), '..');
const AUTH_DIR = resolve(process.cwd(), 'tests/.auth');

export default async function globalSetup() {
  // CI starts the compose stack explicitly so image builds can be cached.
  // Locally we boot the same stack here. `--wait` blocks on healthchecks for
  // postgres/keycloak/frontend; backend has no healthcheck so we poll it below.
  if (!process.env.CI) {
    execSync('docker compose up -d --wait', { cwd: ROOT, stdio: 'inherit' });
  }

  await waitFor(
    'http://localhost:8080/realms/einsatzbereit/.well-known/openid-configuration',
    'Keycloak',
  );

  await waitFor(
    'http://localhost:5000/v1/volunteer-opportunities?pageNumber=1&pageSize=1',
    'Backend',
  );

  mkdirSync(AUTH_DIR, { recursive: true });
  const browser = await chromium.launch();
  await Promise.all([
    saveAuthState(browser, 'olaf', 'olaf123', `${AUTH_DIR}/olaf.json`),
    saveAuthState(browser, 'hannah', 'hannah123', `${AUTH_DIR}/hannah.json`),
  ]);
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

async function waitFor(url: string, name: string, timeoutMs = 120_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const res = await fetch(url);
      if (res.ok) return;
    } catch {
      // not ready yet
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error(`${name} did not become ready within timeout`);
}
