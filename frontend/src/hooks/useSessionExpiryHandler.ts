import { useEffect, useRef } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useLocation } from "react-router";
import { dispatchToast } from "../lib/toastBus";
import { subscribeSessionExpired } from "../lib/sessionExpiryBus";
import { signinLocaleArgs } from "../lib/authLocale";
import {
	recordAuthRecoveryAttempt,
	AUTH_RECOVERY_REDIRECT_LIMIT,
} from "../lib/authRecovery";
import {
	useSetSessionExpiredFlag,
	useSetAuthRecoveryFailedFlag,
} from "../contexts/AuthStatusContext";

export function useSessionExpiryHandler() {
	const auth = useAuth();
	const { t } = useTranslation();
	const location = useLocation();
	const setSessionExpired = useSetSessionExpiredFlag();
	const setAuthRecoveryFailed = useSetAuthRecoveryFailedFlag();
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
			if (!authRef.current.isAuthenticated) return;

			if (handledRef.current) return;
			handledRef.current = true;

			// Bounded recovery (#2208): a redirect through Keycloak only counts
			// as "handled" once an authenticated API call actually succeeds
			// (cleared in api-instance.ts) - so a second consecutive expiry
			// without one in between means the redirect isn't fixing anything
			// (e.g. a ValidIssuers mismatch) and would otherwise loop forever.
			if (recordAuthRecoveryAttempt() > AUTH_RECOVERY_REDIRECT_LIMIT) {
				setAuthRecoveryFailed(true);
				return;
			}

			setSessionExpired(true);
			dispatchToast("error", t("error.sessionExpired"));

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
	}, [auth, t, setSessionExpired, setAuthRecoveryFailed]);
}
