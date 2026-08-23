import { UserManager } from "oidc-client-ts";
import { runtimeConfig } from "./lib/runtimeConfig";

new UserManager({
	authority: runtimeConfig.keycloakAuthorityUrl,
	client_id: runtimeConfig.keycloakClientId,
	redirect_uri: window.location.origin + "/callback",
})
	.signinSilentCallback()
	.catch((error: unknown) => {
		console.error("[silent-renew]", error);
	});
