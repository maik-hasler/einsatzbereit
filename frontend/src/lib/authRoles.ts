/**
 * The realm roles a signed-in user carries, read out of the id_token.
 *
 * Keycloak delivers these through a custom mapper as a flat array on the
 * profile claim, and `oidc-client-ts` types that claim as `unknown` - so every
 * reader had the same four lines: an `Array.isArray` guard, a ternary, an
 * empty-array fallback and an `as string[]` cast. Three copies of it, each
 * free to drift, each re-deciding what a malformed claim means.
 *
 * It is also the one place the app's model of "who someone is" touches the
 * identity provider's wire format. Keeping it to one function is what makes a
 * different provider - or a native client reading the same claim from a
 * different library - a change to this file rather than to its callers.
 */
export const KNOWN_ROLES = ["user", "organisator", "admin"] as const;

export type KnownRole = (typeof KNOWN_ROLES)[number];

/**
 * Reads the roles claim defensively: anything that is not an array of strings
 * is no roles at all, never a crash and never a partial list.
 */
export function readRoles(claim: unknown): string[] {
	if (!Array.isArray(claim)) return [];
	return claim.filter((role): role is string => typeof role === "string");
}

export function hasRole(claim: unknown, role: KnownRole): boolean {
	return readRoles(claim).includes(role);
}
