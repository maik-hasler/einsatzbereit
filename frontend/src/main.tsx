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
// Imported before global.css (not from CalendarWidget, which only lives on
// the lazy-loaded OrgDashboardPage chunk) so react-big-calendar's default,
// low-contrast stylesheet always ends up earlier than global.css's brand
// overrides for the same classes (.rbc-active, .rbc-off-range, etc.) in the
// final CSS output - same specificity, so cascade order alone decides which
// wins, and a lazy-chunk-scoped import made that order load-timing-
// dependent instead of fixed.
import "react-big-calendar/lib/css/react-big-calendar.css";
import "./styles/global.css";

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
