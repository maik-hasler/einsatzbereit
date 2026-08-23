import { UserManager, WebStorageStateStore } from "oidc-client-ts";
import type { ExtraSigninRequestArgs } from "oidc-client-ts";
import { runtimeConfig } from "./runtimeConfig";

let registrationUserManager: UserManager | null = null;

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
