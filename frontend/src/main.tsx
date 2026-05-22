import "./i18n";
import React from "react";
import ReactDOM from "react-dom/client";
import { AuthProvider } from "react-oidc-context";
import { WebStorageStateStore } from "oidc-client-ts";
import { BrowserRouter } from "react-router";
import App from "./App";
import ErrorBoundary from "./components/ErrorBoundary";
import { ToastProvider } from "./contexts/ToastContext";
import { runtimeConfig } from "./lib/runtimeConfig";
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
	onSigninCallback: () => {
		window.location.replace("/");
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
