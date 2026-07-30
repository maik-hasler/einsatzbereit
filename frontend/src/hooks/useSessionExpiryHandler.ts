import { useEffect, useRef } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useLocation } from "react-router";
import { dispatchToast } from "../lib/toastBus";
import { subscribeSessionExpired } from "../lib/sessionExpiryBus";
import { signinLocaleArgs } from "../lib/authLocale";

/**
 * Reacts to a Keycloak session going invalid while the UI still looks
 * logged in - either reactively (an API call came back 401, reported via
 * sessionExpiryBus) or proactively (react-oidc-context's automaticSilentRenew
 * failed in the background, e.g. the SSO session was revoked server-side).
 * Shows a toast and redirects to sign-in, preserving the current location
 * so the user lands back where they were after re-authenticating.
 */
export function useSessionExpiryHandler() {
	const auth = useAuth();
	const { t } = useTranslation();
	const location = useLocation();
	const handledRef = useRef(false);
	const locationRef = useRef(location);
	locationRef.current = location;
	const authRef = useRef(auth);
	authRef.current = auth;

	useEffect(() => {
		handledRef.current = false;
	}, [auth.isAuthenticated]);

	useEffect(() => {
		let redirectTimer: ReturnType<typeof setTimeout> | null = null;

		function handleExpiry() {
			// A stale/expired user object can still sit in localStorage from an
			// earlier login - automaticSilentRenew fires a renewal attempt for it
			// on mount regardless of what page is open, and that attempt fails
			// immediately when there's no live Keycloak SSO session behind it
			// (e.g. login_required). That is not "your active session just
			// expired" - the user was never authenticated on this page to begin
			// with, so there's nothing to interrupt them for. Only react when
			// they actually still look logged in.
			if (!authRef.current.isAuthenticated) return;

			// Several concurrent API calls (or a bus event racing a silent-renew
			// failure) can all report expiry around the same time - only act once.
			if (handledRef.current) return;
			handledRef.current = true;

			dispatchToast("error", t("error.sessionExpired"));
			// Give the toast a moment to actually render before the redirect tears
			// the page down - firing signinRedirect immediately raced the toast's
			// paint against Keycloak's top-level navigation, which occasionally
			// won (the toast never becoming visible) even though the navigation
			// itself was expected to land on a real page rather than commit.
			redirectTimer = setTimeout(() => {
				void auth.signinRedirect({
					...signinLocaleArgs(),
					state: {
						returnTo: locationRef.current.pathname + locationRef.current.search,
					},
				});
			}, 2000);
		}

		const unsubscribeBus = subscribeSessionExpired(handleExpiry);
		const unsubscribeSilentRenewError =
			auth.events.addSilentRenewError(handleExpiry);

		return () => {
			unsubscribeBus();
			unsubscribeSilentRenewError();
			if (redirectTimer !== null) clearTimeout(redirectTimer);
		};
	}, [auth, t]);
}
