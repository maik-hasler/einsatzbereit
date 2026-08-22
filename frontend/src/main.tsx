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
// Display face for the hero headline and major section headings only (see
// --font-display in global.css) - Barlow Condensed's tall, tight letterforms
// come from California DMV road-signage lettering, which reads as
// operational/dispatch-board rather than "warm startup serif," matching what
// "Einsatzbereit" (readiness) already means in German. Only the
// 700 (bold) cut is loaded - every heading that uses it sets font-bold.
import "@fontsource/barlow-condensed/700.css";
import "./styles/global.css";

const oidcConfig = {
	authority: runtimeConfig.keycloakAuthorityUrl,
	client_id: runtimeConfig.keycloakClientId,
	redirect_uri: window.location.origin + "/callback",
	post_logout_redirect_uri: window.location.origin,
	scope: "openid profile email",
	automaticSilentRenew: true,
	// A dedicated, minimal page (src/silentRenew.ts) rather than defaulting to
	// /callback (#2042) - oidc-client-ts's hidden iframe for automaticSilentRenew/
	// signinSilent() would otherwise boot the full SPA every renewal cycle, and
	// the CSP's frame-src has to allow 'self' for this origin's own iframe to
	// load at all (see frontend/nginx.conf.template).
	silent_redirect_uri: window.location.origin + "/silent-renew.html",
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
			// Announce the switch before it happens (#1842) - otherwise it's an
			// invisible side effect of a token refresh, indistinguishable from
			// the UI randomly changing language mid-session. Computed while
			// i18n.language is still the pre-switch language, so the sentence
			// itself stays readable through the change it's describing.
			dispatchToast(
				"info",
				i18n.t("language.switchedFromAccount", {
					language: i18n.t(`language.${keycloakLocale}`),
				}),
			);
			await i18n.changeLanguage(keycloakLocale);
			// Give the toast a moment to actually paint before the redirect below
			// tears the page down - the window.location.replace navigation is a
			// real document unload, not a client-side route change, so without
			// this the toast would at best flash for a frame or never render at
			// all. Same race and same fix useSessionExpiryHandler.ts already
			// applies to its own toast-then-navigate sequence.
			await new Promise((resolve) => setTimeout(resolve, 2000));
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
