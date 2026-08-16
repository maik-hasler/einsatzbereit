import { useEffect, useRef } from "react";
import { useAuth } from "react-oidc-context";
import { useLocation } from "react-router";

/**
 * A fresh tab's sessionStorage carries no stored OIDC user (main.tsx's
 * userStore is intentionally sessionStorage-backed, per-tab) even when the
 * browser still holds a live Keycloak SSO session cookie from another tab -
 * automaticSilentRenew only renews an *already-known* session, it never
 * discovers one, so a public page's header rendered "logged out" regardless
 * of the real session state (#1929). Probing once via signinSilent() closes
 * that gap: on success it's the same USER_LOADED event automaticSilentRenew
 * already relies on, so every consumer (Header, ProtectedRoute, ...) picks
 * up the real state with no page reload. A rejection (no live session - the
 * ordinary logged-out case) is expected and silently ignored, not surfaced
 * as an error the way a renewal failure on an already-known session is
 * (useSessionExpiryHandler).
 */
export function useSilentSsoProbe() {
	const auth = useAuth();
	const location = useLocation();
	const attemptedRef = useRef(false);

	useEffect(() => {
		// signinSilent() itself loads this same app in a hidden iframe to
		// complete the round trip (oidc-client-ts's silent_redirect_uri
		// defaults to redirect_uri, i.e. /callback - see main.tsx). Without this
		// guard, that inner mount would see itself as logged out too and fire
		// its own nested probe, recursing iframes-in-iframes indefinitely.
		if (window.self !== window.top) return;
		// /callback is mid-flow (a real signin or an automatic silent renewal)
		// handling its own auth resolution - a probe here would be redundant at
		// best and, on a failed real callback, an extra pointless hidden-iframe
		// round trip.
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
