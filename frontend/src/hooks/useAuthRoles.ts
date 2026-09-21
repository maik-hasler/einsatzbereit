import { useAuth } from "react-oidc-context";
import { hasRole, readRoles, type KnownRole } from "../lib/authRoles";

/**
 * The signed-in user's realm roles.
 *
 * These decide what the UI offers, never what the API allows - the backend
 * re-checks every one of them on the endpoint (see its authorization
 * policies). A role check here is a way to avoid showing someone a button
 * that would fail, not a security boundary.
 */
export function useAuthRoles(): {
	roles: string[];
	has: (role: KnownRole) => boolean;
} {
	const { user } = useAuth();
	const claim = user?.profile?.roles;
	return {
		roles: readRoles(claim),
		has: (role) => hasRole(claim, role),
	};
}
