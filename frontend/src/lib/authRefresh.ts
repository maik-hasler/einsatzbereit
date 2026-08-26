import type { AuthContextProps } from "react-oidc-context";

// Keycloak bakes role claims into the access token at issue time, so a role
// the backend just granted (e.g. organizer) isn't visible to the caller
// until the next token refresh - without this, the caller's very next
// request to a role-gated endpoint 403s for up to accessTokenLifespan (#2206).
export async function refreshAccessTokenAfterRoleGrant(
	auth: Pick<AuthContextProps, "signinSilent">,
): Promise<void> {
	try {
		await auth.signinSilent();
	} catch (error) {
		console.error(
			"[auth] failed to refresh access token after a role grant",
			error,
		);
	}
}
