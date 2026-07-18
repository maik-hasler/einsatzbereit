---
type: "process"
title: "Writing live-staging Playwright scripts"
description: "Import the shared live-browser helper and handle the two-step live login; a plain chromium.launch() dies under the sandbox egress proxy."
tags:
  - playwright
  - sandbox
  - keycloak
  - staging
  - deploy-verify
timestamp: 2026-07-18
---

# Writing live-staging Playwright scripts

A live-staging smoke script exercises the changed behaviour end-to-end against `https://einsatzbereit.maik-hasler.de` and must exit 0. Step 6 of the deploy-verify flow runs one. The two things that break a fresh script are the browser launch (TLS handshake) and the login (form shape), both covered below.

# Import the helper, do not roll your own launch

Start every new script by importing from `scripts/lib/live-browser.mjs`:

```js
import { launchLiveBrowser, loginKeycloak } from "./lib/live-browser.mjs";

const { browser, context, page } = await launchLiveBrowser();
```

`launchLiveBrowser()` exists because the sandbox routes egress through a proxy that re-terminates TLS, and Chromium's default ClientHello does not survive that. The helper pins the launch args that do survive: `--disable-http2`, `--disable-quic`, `--ssl-version-max=tls1.2`, and `--disable-features=PostQuantumKyber,EncryptedClientHello` (disabling post-quantum Kyber and Encrypted Client Hello), plus the proxy server (`HTTPS_PROXY` or the default `http://127.0.0.1:42149`) and `--no-sandbox`. The returned context is created with `ignoreHTTPSErrors: true`.

Most pre-existing scripts in `scripts/` predate this helper and call plain `chromium.launch()`. Do not copy one as a template. A plain launch sends the default ClientHello, fails the handshake against the proxy, and the script dies before it reaches the app. Import the helper instead.

# Live login is two parts, and each part is two steps

Getting logged in on live requires two separate actions, in order:

1. On the app page, click the button matching `/sign in|anmelden/i` (the label is English or German depending on locale). This navigates to the hosted Keycloak login page.
2. Once on Keycloak, call `loginKeycloak(page, username, password)`.

`loginKeycloak` itself drives a two-step Keycloak form: fill `#username`, click `#kc-login`, then fill `#password`, click `#kc-login`, then wait for network idle. Do not assume both fields are on one page on live.

```js
await page.getByRole("button", { name: /sign in|anmelden/i }).click();
await loginKeycloak(page, "vera", "vera123");
```

# Local and live Keycloak differ; do not cross the wires

Live Keycloak is the two-step form described above. Local Aspire Keycloak is single-step (username and password on one page), and the C# `AuthHelper.LoginAsync` in `backend/tests/VisualTests/` handles that. Applying the local single-step assumption to a live script, or the live two-step flow to the local stack, breaks the login and the verification fails for the wrong reason. Keep the `.mjs` live script on `loginKeycloak` and the C# port on `AuthHelper.LoginAsync`.

# Install Playwright without dirtying the lockfile

The root `package.json` already pins the Playwright version. Install with:

```bash
npm install && npx playwright install chromium
```

Never run `npm install --save-dev playwright`. That rewrites the pin to a caret range and dirties `package-lock.json` for no reason. Bare `npm install` respects the existing pin.

# Related

- [deploy-verify-flow](/process/deploy-verify-flow.md) - why: step 6 of the verify flow runs one of these scripts
- [keycloak-realm-config](/reference/keycloak-realm-config.md) - why: supplies the test users and the login form structure the script drives
- [sandbox-limitations](/gotchas/sandbox-limitations.md) - why: the egress-proxy TLS behavior the launch args work around is a sandbox constraint

# Citations

- scripts/lib/live-browser.mjs
- AGENTS.md (root, "Notes on live Playwright scripts")
- AGENTS.md:140
- AGENTS.md:149
