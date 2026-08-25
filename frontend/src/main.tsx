import i18n from "./i18n";
import React, { Suspense } from "react";
import ReactDOM from "react-dom/client";
import { AuthProvider } from "react-oidc-context";
import { WebStorageStateStore, type User } from "oidc-client-ts";
import { BrowserRouter } from "react-router";
import App from "./App";
import ErrorBoundary from "./components/ErrorBoundary";
import { ToastProvider } from "./contexts/ToastContext";
import { runtimeConfig } from "./lib/runtimeConfig";
import { dispatchToast } from "./lib/toastBus";
import { handleUnhandledRejection } from "./lib/unhandledRejection";

import "react-big-calendar/lib/css/react-big-calendar.css";
import "@fontsource-variable/source-sans-3";

import "@fontsource/barlow-condensed/700.css";
import "./styles/global.css";

const oidcConfig = {
	authority: runtimeConfig.keycloakAuthorityUrl,
	client_id: runtimeConfig.keycloakClientId,
	redirect_uri: window.location.origin + "/callback",
	post_logout_redirect_uri: window.location.origin,
	scope: "openid profile email",
	automaticSilentRenew: true,

	silent_redirect_uri: window.location.origin + "/silent-renew.html",
	// sessionStorage, not localStorage: tokens (incl. refresh_token, since the
	// realm has "rememberMe": true) must not survive tab close or browser
	// restart on a shared/kiosk machine - a realistic setting for a
	// volunteer-coordination app used at events (#1171). Playwright seeds
	// sessionStorage directly via page.addInitScript instead of relying on
	// storageState (see AuthHelper.FastSignInAsync in backend/tests/VisualTests).
	userStore: new WebStorageStateStore({ store: window.sessionStorage }),
	onSigninCallback: async (user: User | undefined) => {
		const hasExplicitLanguageChoice =
			localStorage.getItem("einsatzbereit:language-explicit") === "true";
		const keycloakLocale = user?.profile?.locale;
		if (
			!hasExplicitLanguageChoice &&
			keycloakLocale &&
			keycloakLocale !== i18n.language
		) {
			dispatchToast(
				"info",
				i18n.t("language.switchedFromAccount", {
					language: i18n.t(`language.${keycloakLocale}`),
				}),
			);
			await i18n.changeLanguage(keycloakLocale);

			await new Promise((resolve) => setTimeout(resolve, 2000));
		}
		const returnTo = (user?.state as { returnTo?: string })?.returnTo ?? "/";
		window.location.replace(returnTo);
	},
};

window.addEventListener("unhandledrejection", (event) => {
	handleUnhandledRejection(event.reason);
});

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
	<React.StrictMode>
		<ErrorBoundary>
			<Suspense fallback={null}>
				<ToastProvider>
					<AuthProvider {...oidcConfig}>
						<BrowserRouter>
							<App />
						</BrowserRouter>
					</AuthProvider>
				</ToastProvider>
			</Suspense>
		</ErrorBoundary>
	</React.StrictMode>,
);
