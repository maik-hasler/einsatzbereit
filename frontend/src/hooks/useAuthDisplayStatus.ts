import { useAuth } from "react-oidc-context";
import { useSessionExpiredFlag } from "../contexts/AuthStatusContext";

export type AuthDisplayStatus =
	"signedIn" | "signedOut" | "pending" | "sessionExpired";

// isLoading covers both the initial bootstrap and the returning-visitor
// silent-SSO probe (useSilentSsoProbe) - either way the account area should
// hold a neutral state rather than assume signed-out (#2224).
export function useAuthDisplayStatus(): AuthDisplayStatus {
	const auth = useAuth();
	const sessionExpired = useSessionExpiredFlag();

	if (sessionExpired) return "sessionExpired";
	if (auth.isAuthenticated) return "signedIn";
	if (auth.isLoading) return "pending";
	return "signedOut";
}
