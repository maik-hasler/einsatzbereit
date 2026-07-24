---
name: live-verify
description: >
  Writes and runs a throwaway Playwright script against live staging
  (https://einsatzbereit.maik-hasler.de) to prove a fix or feature actually
  works in production - step 6 of the mandatory deploy-and-verify flow in
  root AGENTS.md. Use once deploy-staging succeeds, or whenever asked to
  smoke-test staging, verify on live, or run "/live-verify".
---

# Live verify

Step 6 of the mandatory deploy-and-verify flow (root `AGENTS.md`, "Mandatory:
Deploy and verify"). Two hard gates: the health check, and a live Playwright
script that must exit 0 with every assertion green.

## 1. Health check

```bash
curl -sf https://api.maik-hasler.de/health   # must return HTTP 200
```

## 2. Write the script to a scratch directory - never `scripts/`

There is no committed `scripts/` directory and no root `package.json`
anymore (`wiki/bundle/decisions/scripts-folder-removed.md`). Write the
Playwright script to a scratch directory outside the repo (this session's
scratchpad, or `/tmp`) and delete it once it has served its purpose - it is
throwaway proof the change works right now, never a durable artifact (that's
step 7's C# TUnit test, below).

```bash
cd <scratch-dir> && npm init -y && npm install playwright
```

Chromium is pre-installed at `/opt/pw-browsers` (`PLAYWRIGHT_BROWSERS_PATH`,
`PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1`), so `npx playwright install chromium`
is not needed.

## 3. Launch args - the sandbox's egress proxy re-terminates TLS

Chromium's default ClientHello does not survive the sandbox's
TLS-reterminating proxy. Use this launch exactly, including the
`existsSync` fallback - a hardcoded `executablePath` fails outright outside
the sandbox instead of degrading gracefully:

```js
import { chromium } from "playwright";
import { existsSync } from "node:fs";

const SANDBOX_CHROMIUM = "/opt/pw-browsers/chromium";
const browser = existsSync(SANDBOX_CHROMIUM)
  ? await chromium.launch({
      executablePath: SANDBOX_CHROMIUM,
      proxy: { server: process.env.HTTPS_PROXY ?? "http://127.0.0.1:42149" },
      args: [
        "--no-sandbox",
        "--disable-setuid-sandbox",
        "--disable-http2",
        "--disable-quic",
        "--ssl-version-max=tls1.2",
        "--disable-features=PostQuantumKyber,EncryptedClientHello",
      ],
    })
  : await chromium.launch();
const context = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await context.newPage();
```

## 4. Log in - two steps, in order

```js
await page.getByRole("button", { name: /sign in|anmelden/i }).click();
await page.fill("#username", "vera");
await page.click("#kc-login");
await page.fill("#password", "vera123");
await page.click("#kc-login");
await page.waitForLoadState("networkidle", { timeout: 30000 });
```

The sign-in button label is English or German depending on locale. Live
Keycloak is this two-step form; local Aspire Keycloak (step 7's
`AuthHelper.LoginAsync`) is single-step - don't carry one flow's assumption
to the other.

## 5. Exercise the changed behaviour, then clean up

Drive the actual flow the fix/feature touches, assert on it, exit 0 on
success. Delete the scratch script once done - it never gets committed.

## Then: the durable record

Add the same assertions as an automated C# TUnit test in
`backend/tests/VisualTests/` (step 7 of the mandatory flow, runs against the
local Aspire stack in CI). That test is the durable, reviewable record of
the fix; this scratch script is not.

## Related

- `wiki/bundle/process/deploy-verify-flow.md` - the full 9-step flow this is step 6 of
- `wiki/bundle/process/live-playwright-scripts.md` - the same recipe as wiki knowledge, for non-Claude-Code agents
- `wiki/bundle/decisions/scripts-folder-removed.md` - why this is scratch-only and not a committed helper
