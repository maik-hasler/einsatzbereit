import { useEffect, useRef } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useLocation } from "react-router";
import { dispatchToast } from "../lib/toastBus";
import { subscribeSessionExpired } from "../lib/sessionExpiryBus";
import { signinLocaleArgs } from "../lib/authLocale";
import { useSetSessionExpiredFlag } from "../contexts/AuthStatusContext";

export function useSessionExpiryHandler() {
	const auth = useAuth();
	const { t } = useTranslation();
	const location = useLocation();
	const setSessionExpired = useSetSessionExpiredFlag();
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
	}, [auth, t, setSessionExpired]);
}
