// vitest.config.ts aliases "virtual:pwa-register/react" here so the module
// resolves under Vite's plugin pipeline (VitePWA - which registers that
// virtual module - is not part of the Vitest config). Every test that
// imports PwaUpdatePrompt overrides this via vi.mock("virtual:pwa-register/
// react", ...); this default only has to exist so an *unmocked* import of
// the component elsewhere does not crash the whole module graph.
export function useRegisterSW() {
	return {
		needRefresh: [false, () => {}] as [boolean, () => void],
		offlineReady: [false, () => {}] as [boolean, () => void],
		updateServiceWorker: async () => {},
	};
}
