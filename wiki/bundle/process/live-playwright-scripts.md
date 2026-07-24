---
type: "process"
title: "Writing live-staging Playwright scripts"
description: "Write a throwaway script in a scratch directory, never scripts/ - inline the launch args and handle the two-step live login. A plain chromium.launch() dies under the sandbox egress proxy."
tags:
  - playwright
  - sandbox
  - keycloak
  - staging
  - deploy-verify
timestamp: 2026-07-24
---

# Writing live-staging Playwright scripts

A live-staging smoke script exercises the changed behaviour end-to-end against `https://einsatzbereit.maik-hasler.de` and must exit 0. Step 6 of the deploy-verify flow runs one. Write it to a scratch directory outside the repo checkout (this session's scratchpad, or `/tmp`) and delete it once it has served its purpose - there is no `scripts/` directory to commit it to anymore ([scripts-folder-removed](/decisions/scripts-folder-removed.md) has the why). Claude Code should prefer `.claude/skills/live-verify/SKILL.md` over reading this page end to end - it's the self-contained, actionable copy of the recipe below, same relationship as `.claude/skills/{ingest,query,lint}/` to `wiki/AGENTS.md`. This page is the copy other agents and humans read directly. The two things that break a fresh script are the browser launch (TLS handshake) and the login (form shape), both covered below.

# Launch args, inlined - there is no shared helper anymore

Copy this launch snippet into the scratch script:

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

`executablePath` points at the sandbox's pre-installed Chromium, used only when that path exists. The sandbox routes egress through a proxy that re-terminates TLS, and Chromium's default ClientHello does not survive that without the pinned `args`. Outside the sandbox (a local machine with direct internet access) that binary doesn't exist, so the `existsSync` check falls back to a plain `chromium.launch()` - skipping this check was a real trap in the old shared helper's absence: a hardcoded `executablePath` fails outright on a machine without it, instead of degrading gracefully.

# Live login is two parts, and each part is two steps

Getting logged in on live requires two separate actions, in order:

1. On the app page, click the button matching `/sign in|anmelden/i` (the label is English or German depending on locale). This navigates to the hosted Keycloak login page.
2. Once on Keycloak, fill the two-step form.

```js
async function loginKeycloak(page, username, password) {
  await page.fill("#username", username);
  await page.click("#kc-login");
  await page.fill("#password", password);
  await page.click("#kc-login");
  await page.waitForLoadState("networkidle", { timeout: 30000 });
}

await page.getByRole("button", { name: /sign in|anmelden/i }).click();
await loginKeycloak(page, "vera", "vera123");
```

# Local and live Keycloak differ; do not cross the wires

Live Keycloak is the two-step form described above. Local Aspire Keycloak is single-step (username and password on one page), and the C# `AuthHelper.LoginAsync` in `backend/tests/VisualTests/` handles that. Applying the local single-step assumption to a live script, or the live two-step flow to the local stack, breaks the login and the verification fails for the wrong reason. Keep the `.mjs` live script on the two-step form and the C# port on `AuthHelper.LoginAsync`.

# Install Playwright in the scratch dir, not the repo

Nothing pins a Playwright version at the repo root anymore. Once per session:

```bash
cd <scratch-dir> && npm init -y && npm install playwright
```

Chromium is already pre-installed at `/opt/pw-browsers` (`PLAYWRIGHT_BROWSERS_PATH`, `PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1`), so `npx playwright install chromium` is not needed.

# Related

- [deploy-verify-flow](/process/deploy-verify-flow.md) - why: step 6 of the verify flow runs one of these scripts
- [scripts-folder-removed](/decisions/scripts-folder-removed.md) - why: explains why this page no longer points at a committed `scripts/lib/live-browser.mjs`
- [keycloak-realm-config](/reference/keycloak-realm-config.md) - why: supplies the test users and the login form structure the script drives
- [sandbox-limitations](/gotchas/sandbox-limitations.md) - why: the egress-proxy TLS behavior the launch args work around is a sandbox constraint

# Citations

- AGENTS.md (root, "Mandatory: Deploy and verify every bug fix / feature")
- #791
