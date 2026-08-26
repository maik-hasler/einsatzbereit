interface AppConfig {
	KEYCLOAK_AUTHORITY_URL: string;
	KEYCLOAK_CLIENT_ID: string;
	API_URL: string;
	TOAST_LIFETIME_MS: string;
	APP_VERSION: string;
}

declare global {
	interface Window {
		__APP_CONFIG__?: Partial<AppConfig>;
	}
}

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
	appVersion: resolve("APP_VERSION", import.meta.env.VITE_APP_VERSION ?? "dev"),

	toastLifetimeMs: Number(
		resolve(
			"TOAST_LIFETIME_MS",
			import.meta.env.VITE_TOAST_LIFETIME_MS ?? "5000",
		),
	),
};
