import { UserManager, WebStorageStateStore } from "oidc-client-ts";
import type { ExtraSigninRequestArgs } from "oidc-client-ts";
import { runtimeConfig } from "./runtimeConfig";

let registrationUserManager: UserManager | null = null;

// Keycloak exposes a separate registration endpoint that accepts the same
// OIDC authorization params as the login endpoint and completes with the
// same authorization-code callback. A dedicated UserManager lets us override
// just authorization_endpoint (via metadataSeed, which wins over the fetched
// discovery document) while sharing sessionStorage-backed state/user stores
// with the app's default UserManager (main.tsx) so /callback can still
// validate it.
function getRegistrationUserManager(): UserManager {
	if (!registrationUserManager) {
		registrationUserManager = new UserManager({
			authority: runtimeConfig.keycloakAuthorityUrl,
			client_id: runtimeConfig.keycloakClientId,
			redirect_uri: window.location.origin + "/callback",
			scope: "openid profile email",
			userStore: new WebStorageStateStore({ store: window.sessionStorage }),
			metadataSeed: {
				authorization_endpoint: `${runtimeConfig.keycloakAuthorityUrl}/protocol/openid-connect/registrations`,
			},
		});
	}
	return registrationUserManager;
}

export function signinRedirectForRegistration(
	args?: ExtraSigninRequestArgs,
): Promise<void> {
	return getRegistrationUserManager().signinRedirect(args);
}
