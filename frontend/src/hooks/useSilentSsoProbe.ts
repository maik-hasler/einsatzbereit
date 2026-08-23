import { useEffect, useRef } from "react";
import { useAuth } from "react-oidc-context";
import { useLocation } from "react-router";

export function useSilentSsoProbe() {
	const auth = useAuth();
	const location = useLocation();
	const attemptedRef = useRef(false);

	useEffect(() => {
		if (window.self !== window.top) return;

		if (location.pathname === "/callback") return;
		if (auth.isLoading || auth.isAuthenticated || attemptedRef.current) return;
		attemptedRef.current = true;
		auth.signinSilent().catch(() => {
			// No live Keycloak SSO session behind this tab - the ordinary
			// logged-out case, nothing to react to.
		});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [auth.isLoading, auth.isAuthenticated, location.pathname]);
}
