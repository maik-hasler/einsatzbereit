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

				"src/client/generated/**",

				"src/main.tsx",
				"src/vite-env.d.ts",
				"src/**/*.d.ts",
			],

			// A ratchet on what is covered today, not a target. Each number sits
			// about two points under the measurement it was taken from, so an
			// ordinary refactor has room and a genuine drop does not: at zero
			// slack this gate goes red on a rounding difference and the first
			// thing anyone does is delete it.
			//
			// Measured when these were set (statements/branches/functions/lines):
			// all files 74.4/74.1/71.2/76.1, lib 94.3/93.9/89.5/94.3, client
			// 95.3/92.5/88.9/96.4, hooks 81.1/74.1/71.7/84.8, contexts
			// 85.7/83.3/76.6/89.3, layouts 83.5/75.7/77.3/87.1.
			//
			// Per-directory rather than one global number, because the folders are
			// not comparable: `lib/` is pure logic and cheap to cover, while
			// `components/` carries the ones that need a layout engine
			// (SingleMarkerMap, ImageCropModal) and are covered by
			// `backend/tests/VisualTests` instead. One average would hide both.
			thresholds: {
				statements: 72,
				branches: 72,
				functions: 69,
				lines: 74,

				"src/lib/**": {
					statements: 92,
					branches: 91,
					functions: 87,
					lines: 92,
				},
				"src/client/**": {
					statements: 93,
					branches: 90,
					functions: 86,
					lines: 94,
				},
				"src/hooks/**": {
					statements: 79,
					branches: 72,
					functions: 69,
					lines: 82,
				},
				"src/contexts/**": {
					statements: 83,
					branches: 81,
					functions: 74,
					lines: 87,
				},
				"src/layouts/**": {
					statements: 81,
					branches: 73,
					functions: 75,
					lines: 85,
				},
			},
		},
	},
});
