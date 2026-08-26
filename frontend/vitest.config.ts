import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { configDefaults, defineConfig } from "vitest/config";
import svgr from "vite-plugin-svgr";

const __dirname = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
	plugins: [svgr()],
	resolve: {
		alias: {
			// VitePWA (vite.config.ts) is what registers this virtual module -
			// it is not part of this test config, so Vite's resolver would
			// otherwise fail before PwaUpdatePrompt.test.tsx's own
			// vi.mock("virtual:pwa-register/react", ...) ever gets a chance to
			// intercept it. See src/test/pwaRegisterStub.ts.
			"virtual:pwa-register/react": resolve(
				__dirname,
				"src/test/pwaRegisterStub.ts",
			),
		},
	},
	test: {
		environment: "jsdom",
		setupFiles: ["./src/test/setup.ts"],
		// `pnpm mutation` leaves .stryker-tmp/ behind, and it is a full copy of
		// the project - including every *.test.tsx. Vitest 4 only excludes
		// node_modules and .git by default, so without this a contributor who has
		// run Stryker locally silently runs the suite twice (measured: 212 files
		// discovered against 106 real ones). Same hazard `pnpm lint` has, handled
		// the same way in eslint.config.js.
		exclude: [...configDefaults.exclude, "**/.stryker-tmp/**"],
		coverage: {
			provider: "v8",

			include: ["src/**/*.{ts,tsx}"],
			exclude: [
				"src/**/*.test.{ts,tsx}",
				"src/test/**",

				"src/client/api-client.ts",

				"src/main.tsx",
				"src/vite-env.d.ts",
				"src/**/*.d.ts",
			],
		},
	},
});
