import i18n from "./i18n";
import React from "react";
import ReactDOM from "react-dom/client";
import { AuthProvider } from "react-oidc-context";
import { WebStorageStateStore, type User } from "oidc-client-ts";
import { BrowserRouter } from "react-router";
import App from "./App";
import ErrorBoundary from "./components/ErrorBoundary";
import { ToastProvider } from "./contexts/ToastContext";
import { runtimeConfig } from "./lib/runtimeConfig";
import "./styles/global.css";

if ("serviceWorker" in navigator) {
	window.addEventListener("load", () => {
		navigator.serviceWorker.register("/sw.js").catch(() => {
			// Service worker registration is best-effort
		});
	});
}

const oidcConfig = {
	authority: runtimeConfig.keycloakAuthorityUrl,
	client_id: runtimeConfig.keycloakClientId,
	redirect_uri: window.location.origin + "/callback",
	post_logout_redirect_uri: window.location.origin,
	scope: "openid profile email",
	automaticSilentRenew: true,
	// Use localStorage so Playwright storageState captures the session
	userStore: new WebStorageStateStore({ store: window.localStorage }),
	onSigninCallback: (user: User | undefined) => {
		const keycloakLocale = user?.profile?.locale;
		if (keycloakLocale && keycloakLocale !== i18n.language) {
			void i18n.changeLanguage(keycloakLocale);
		}
		const returnTo = (user?.state as { returnTo?: string })?.returnTo ?? "/";
		window.location.replace(returnTo);
	},
};

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
	<React.StrictMode>
		<ErrorBoundary>
			<ToastProvider>
				<AuthProvider {...oidcConfig}>
					<BrowserRouter>
						<App />
					</BrowserRouter>
				</AuthProvider>
			</ToastProvider>
		</ErrorBoundary>
	</React.StrictMode>,
);
