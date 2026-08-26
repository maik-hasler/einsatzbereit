import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";

describe("runtimeConfig", () => {
	beforeEach(() => {
		vi.resetModules();
		delete window.__APP_CONFIG__;
	});

	afterEach(() => {
		vi.unstubAllEnvs();
		delete window.__APP_CONFIG__;
	});

	it("uses the build-time env var when no runtime config is injected", async () => {
		vi.stubEnv("VITE_API_URL", "https://build-time.example");
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.apiUrl).toBe("https://build-time.example");
	});

	it("prefers window.__APP_CONFIG__ once it has been substituted by the container", async () => {
		vi.stubEnv("VITE_API_URL", "https://build-time.example");
		window.__APP_CONFIG__ = { API_URL: "https://runtime.example" };
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.apiUrl).toBe("https://runtime.example");
	});

	it("falls back to the build-time value when the runtime placeholder was never substituted", async () => {
		vi.stubEnv("VITE_API_URL", "https://build-time.example");
		window.__APP_CONFIG__ = { API_URL: "${VITE_API_URL}" };
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.apiUrl).toBe("https://build-time.example");
	});

	it("resolves the keycloak authority url, client id and api url independently", async () => {
		vi.stubEnv("VITE_KEYCLOAK_AUTHORITY_URL", "https://keycloak.example");
		vi.stubEnv("VITE_KEYCLOAK_CLIENT_ID", "frontend");
		vi.stubEnv("VITE_API_URL", "https://api.example");
		window.__APP_CONFIG__ = { API_URL: "https://runtime.example" };

		const { runtimeConfig } = await import("./runtimeConfig");

		expect(runtimeConfig.keycloakAuthorityUrl).toBe("https://keycloak.example");
		expect(runtimeConfig.keycloakClientId).toBe("frontend");
		expect(runtimeConfig.apiUrl).toBe("https://runtime.example");
	});

	it("defaults toastLifetimeMs to 5000 when neither the build-time env var nor runtime config is set", async () => {
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.toastLifetimeMs).toBe(5000);
	});

	it("resolves toastLifetimeMs from the build-time env var, as a number", async () => {
		vi.stubEnv("VITE_TOAST_LIFETIME_MS", "0");
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.toastLifetimeMs).toBe(0);
	});

	it("prefers window.__APP_CONFIG__'s TOAST_LIFETIME_MS once it has been substituted by the container", async () => {
		vi.stubEnv("VITE_TOAST_LIFETIME_MS", "5000");
		window.__APP_CONFIG__ = { TOAST_LIFETIME_MS: "0" };
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.toastLifetimeMs).toBe(0);
	});

	it("isConfigured is true once the API url and both keycloak values resolve to real values", async () => {
		vi.stubEnv("VITE_API_URL", "https://api.example");
		vi.stubEnv("VITE_KEYCLOAK_AUTHORITY_URL", "https://keycloak.example");
		vi.stubEnv("VITE_KEYCLOAK_CLIENT_ID", "frontend");
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.isConfigured).toBe(true);
	});

	it("isConfigured is false when every source left the API url empty - the offline cold-start fallback (#2207)", async () => {
		vi.stubEnv("VITE_API_URL", "");
		vi.stubEnv("VITE_KEYCLOAK_AUTHORITY_URL", "https://keycloak.example");
		vi.stubEnv("VITE_KEYCLOAK_CLIENT_ID", "frontend");
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.isConfigured).toBe(false);
	});

	it("isConfigured is false when only the runtime placeholder was ever provided for a value", async () => {
		vi.stubEnv("VITE_API_URL", "");
		vi.stubEnv("VITE_KEYCLOAK_AUTHORITY_URL", "https://keycloak.example");
		vi.stubEnv("VITE_KEYCLOAK_CLIENT_ID", "frontend");
		window.__APP_CONFIG__ = { API_URL: "${VITE_API_URL}" };
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.isConfigured).toBe(false);
	});
});
