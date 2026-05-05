/// <reference types="vite/client" />
/// <reference types="vite-plugin-svgr/client" />

interface ImportMetaEnv {
	readonly VITE_KEYCLOAK_AUTHORITY_URL: string;
	readonly VITE_KEYCLOAK_CLIENT_ID: string;
	readonly VITE_API_URL: string;
}

interface ImportMeta {
	readonly env: ImportMetaEnv;
}
