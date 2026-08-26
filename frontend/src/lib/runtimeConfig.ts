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

function resolve(key: keyof AppConfig, fallback: string): string {
	const value = window.__APP_CONFIG__?.[key];
	if (value && !value.startsWith("${")) {
		return value;
	}
	return fallback;
}

const keycloakAuthorityUrl = resolve(
	"KEYCLOAK_AUTHORITY_URL",
	import.meta.env.VITE_KEYCLOAK_AUTHORITY_URL,
);
const keycloakClientId = resolve(
	"KEYCLOAK_CLIENT_ID",
	import.meta.env.VITE_KEYCLOAK_CLIENT_ID,
);
const apiUrl = resolve("API_URL", import.meta.env.VITE_API_URL);

export const runtimeConfig = {
	keycloakAuthorityUrl,
	keycloakClientId,
	apiUrl,

	toastLifetimeMs: Number(
		resolve(
			"TOAST_LIFETIME_MS",
			import.meta.env.VITE_TOAST_LIFETIME_MS ?? "5000",
		),
	),

	// False when neither the container's /config.js nor the image's own
	// build-time fallback provided a real value for one of the three fields
	// above (#2207) - e.g. an offline PWA cold start that never reached
	// /config.js, on an image built without the (now default-less) VITE_*
	// build args. ConfigGate (src/components/ConfigGate.tsx) refuses to
	// render the app in that state instead of quietly running against an
	// empty API/Keycloak origin.
	isConfigured: Boolean(keycloakAuthorityUrl && keycloakClientId && apiUrl),
};
