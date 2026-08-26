interface AppConfig {
	KEYCLOAK_AUTHORITY_URL: string;
	KEYCLOAK_CLIENT_ID: string;
	API_URL: string;
	TOAST_LIFETIME_MS: string;
	OPERATOR_NAME: string;
	OPERATOR_ADDRESS: string;
	OPERATOR_EMAIL: string;
	OPERATOR_SITE_URL: string;
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

const operatorName = resolve("OPERATOR_NAME", "");
const operatorAddress = resolve("OPERATOR_ADDRESS", "");
const operatorEmail = resolve("OPERATOR_EMAIL", "");
const operatorSiteUrl = resolve("OPERATOR_SITE_URL", "");

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

	toastLifetimeMs: Number(
		resolve(
			"TOAST_LIFETIME_MS",
			import.meta.env.VITE_TOAST_LIFETIME_MS ?? "5000",
		),
	),

	operatorName,
	operatorAddress,
	operatorEmail,
	operatorSiteUrl,
	// DDG §5/GDPR Art. 13 need a complete legal identity - a half-filled notice
	// (e.g. a name with no way to reach them) is worse than none, so this is
	// all-or-nothing rather than per-field (einsatzbereit#2196).
	operatorConfigured: Boolean(
		operatorName && operatorAddress && operatorEmail && operatorSiteUrl,
	),
};
