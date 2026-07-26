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

	useEffect(() => {
		handledRef.current = false;
	}, [auth.isAuthenticated]);

	useEffect(() => {
		function handleExpiry() {
			// Several concurrent API calls (or a bus event racing a silent-renew
			// failure) can all report expiry around the same time - only act once.
			if (handledRef.current) return;
			handledRef.current = true;

			dispatchToast("error", t("error.sessionExpired"));
			void auth.signinRedirect({
				...signinLocaleArgs(),
				state: {
					returnTo: locationRef.current.pathname + locationRef.current.search,
				},
			});
		}

		const unsubscribeBus = subscribeSessionExpired(handleExpiry);
		const unsubscribeSilentRenewError =
			auth.events.addSilentRenewError(handleExpiry);

		return () => {
			unsubscribeBus();
			unsubscribeSilentRenewError();
		};
	}, [auth, t]);
}
