import { UserManager } from "oidc-client-ts";
import { runtimeConfig } from "./lib/runtimeConfig";

// main.tsx's silent_redirect_uri (#2042) - loaded in a hidden iframe by
// automaticSilentRenew/signinSilent() every renewal cycle. Deliberately not
// /callback: that would boot the entire SPA (React, i18n, fonts, the whole
// route tree) inside the iframe just to relay one URL back to the parent
// window. signinSilentCallback() below only forwards the callback URL via
// postMessage to window.parent - the actual token exchange and storage
// happens in the parent window's own UserManager instance (main.tsx), not
// here, so this page needs nothing else.
new UserManager({
	authority: runtimeConfig.keycloakAuthorityUrl,
	client_id: runtimeConfig.keycloakClientId,
	redirect_uri: window.location.origin + "/callback",
})
	.signinSilentCallback()
	.catch((error: unknown) => {
		console.error("[silent-renew]", error);
	});
