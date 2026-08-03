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
// Imported before global.css (not from CalendarWidget, which only lives on
// the lazy-loaded OrgDashboardPage chunk) so react-big-calendar's default,
// low-contrast stylesheet always ends up earlier than global.css's brand
// overrides for the same classes (.rbc-active, .rbc-off-range, etc.) in the
// final CSS output - same specificity, so cascade order alone decides which
// wins, and a lazy-chunk-scoped import made that order load-timing-
// dependent instead of fixed.
import "react-big-calendar/lib/css/react-big-calendar.css";
import "@fontsource-variable/source-sans-3";
import "./styles/global.css";

const oidcConfig = {
	authority: runtimeConfig.keycloakAuthorityUrl,
	client_id: runtimeConfig.keycloakClientId,
	redirect_uri: window.location.origin + "/callback",
	post_logout_redirect_uri: window.location.origin,
	scope: "openid profile email",
	automaticSilentRenew: true,
	// sessionStorage, not localStorage: tokens (incl. refresh_token, since the
	// realm has "rememberMe": true) must not survive tab close or browser
	// restart on a shared/kiosk machine - a realistic setting for a
	// volunteer-coordination app used at events (#1171). Playwright seeds
	// sessionStorage directly via page.addInitScript instead of relying on
	// storageState (see AuthHelper.FastSignInAsync in backend/tests/VisualTests).
	userStore: new WebStorageStateStore({ store: window.sessionStorage }),
	onSigninCallback: async (user: User | undefined) => {
		// Only fall back to the Keycloak login page's locale on a browser
		// that has never had an explicit in-app language choice - otherwise a
		// user who picked German via the header's LanguageSelector would have
		// it silently reverted to whatever locale their Keycloak login
		// session carries on every subsequent signin (#1253). This is a
		// dedicated flag (set only by LanguageSelector's onClick) rather than
		// i18next's own "i18nextLng" localStorage cache, since the language
		// detector populates that cache from the browser's Accept-Language on
		// first load too - which would make nearly every session look like it
		// already had a "choice" and defeat this guard.
		const hasExplicitLanguageChoice =
			localStorage.getItem("einsatzbereit:language-explicit") === "true";
		const keycloakLocale = user?.profile?.locale;
		if (
			!hasExplicitLanguageChoice &&
			keycloakLocale &&
			keycloakLocale !== i18n.language
		) {
			await i18n.changeLanguage(keycloakLocale);
		}
		const returnTo = (user?.state as { returnTo?: string })?.returnTo ?? "/";
		window.location.replace(returnTo);
	},
};

// Every floating promise in this codebase (signinRedirect/signoutRedirect
// calls, ProtectedRoute's redirect, etc.) previously failed completely
// invisibly to the user on rejection - this is the single, app-wide net that
// makes an otherwise-silent failure at least visible as a toast (#1243).
window.addEventListener("unhandledrejection", (event) => {
	console.error("[unhandledrejection]", event.reason);
	dispatchToast("error", i18n.t("error.serverError"));
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
