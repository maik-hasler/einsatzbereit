import { WebStorageStateStore } from "oidc-client-ts";

/**
 * Where `oidc-client-ts` keeps the tokens.
 *
 * `sessionStorage`, not `localStorage`: tokens - including the refresh token,
 * since the realm has `rememberMe: true` - must not survive a tab close or a
 * browser restart on a shared or kiosk machine, which is a realistic setting
 * for a volunteer-coordination app used at an event (#1171). Playwright seeds
 * `sessionStorage` directly through `page.addInitScript` rather than relying
 * on `storageState` (see `AuthHelper.FastSignInAsync` in
 * `backend/tests/VisualTests`).
 *
 * Pulled out of `main.tsx` into its own module because this is the one line
 * that has to change on a runtime with no `Storage` at all: the store accepts
 * anything implementing the same shape, so a Keychain- or Keystore-backed
 * store is a substitution here rather than a change to the auth setup.
 */
export function createTokenStore(): WebStorageStateStore {
	return new WebStorageStateStore({ store: window.sessionStorage });
}
