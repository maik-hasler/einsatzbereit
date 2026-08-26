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

	it("defaults the operator identity to empty strings, and reports not configured, when nothing was injected", async () => {
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.operatorName).toBe("");
		expect(runtimeConfig.operatorAddress).toBe("");
		expect(runtimeConfig.operatorEmail).toBe("");
		expect(runtimeConfig.operatorSiteUrl).toBe("");
		expect(runtimeConfig.operatorConfigured).toBe(false);
	});

	it("falls back to empty operator fields when the runtime placeholder was never substituted", async () => {
		window.__APP_CONFIG__ = { OPERATOR_NAME: "${OPERATOR_NAME}" };
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.operatorName).toBe("");
		expect(runtimeConfig.operatorConfigured).toBe(false);
	});

	it("resolves the operator identity from window.__APP_CONFIG__ once substituted by the container", async () => {
		window.__APP_CONFIG__ = {
			OPERATOR_NAME: "ACME Rescue",
			OPERATOR_ADDRESS: "1 Example Street, 12345 Example City",
			OPERATOR_EMAIL: "legal@acme-rescue.example",
			OPERATOR_SITE_URL: "https://acme-rescue.example",
		};
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.operatorName).toBe("ACME Rescue");
		expect(runtimeConfig.operatorAddress).toBe(
			"1 Example Street, 12345 Example City",
		);
		expect(runtimeConfig.operatorEmail).toBe("legal@acme-rescue.example");
		expect(runtimeConfig.operatorSiteUrl).toBe("https://acme-rescue.example");
		expect(runtimeConfig.operatorConfigured).toBe(true);
	});

	it("is not configured when only some of the operator fields are set", async () => {
		window.__APP_CONFIG__ = {
			OPERATOR_NAME: "ACME Rescue",
			OPERATOR_EMAIL: "legal@acme-rescue.example",
		};
		const { runtimeConfig } = await import("./runtimeConfig");
		expect(runtimeConfig.operatorConfigured).toBe(false);
	});
});
