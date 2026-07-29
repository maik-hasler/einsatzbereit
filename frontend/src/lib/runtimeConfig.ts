interface AppConfig {
	KEYCLOAK_AUTHORITY_URL: string;
	KEYCLOAK_CLIENT_ID: string;
	API_URL: string;
	TOAST_LIFETIME_MS: string;
}

declare global {
	interface Window {
		__APP_CONFIG__?: Partial<AppConfig>;
	}
}

// Reads a value injected at container start (window.__APP_CONFIG__, populated by
// envsubst on config.js). Unsubstituted placeholders (e.g. local dev, where the
// raw "${VITE_...}" template is served) fall back to Vite's build-time env.
function resolve(key: keyof AppConfig, fallback: string): string {
	const value = window.__APP_CONFIG__?.[key];
	if (value && !value.startsWith("${")) {
		return value;
	}
	return fallback;
}

export const runtimeConfig = {
	keycloakAuthorityUrl: resolve(
		"KEYCLOAK_AUTHORITY_URL",
		import.meta.env.VITE_KEYCLOAK_AUTHORITY_URL,
	),
	keycloakClientId: resolve(
		"KEYCLOAK_CLIENT_ID",
		import.meta.env.VITE_KEYCLOAK_CLIENT_ID,
	),
	apiUrl: resolve("API_URL", import.meta.env.VITE_API_URL),
	// Toasts (ToastContext.tsx) auto-dismiss after this many ms; 0 disables
	// auto-dismiss entirely. AppHost sets VITE_TOAST_LIFETIME_MS=0 for
	// Aspire-orchestrated test runs so assertions never race the dismiss
	// timer - production is unaffected, since neither this nor a runtime
	// __APP_CONFIG__ override is ever set there.
	toastLifetimeMs: Number(
		resolve(
			"TOAST_LIFETIME_MS",
			import.meta.env.VITE_TOAST_LIFETIME_MS ?? "5000",
		),
	),
};
