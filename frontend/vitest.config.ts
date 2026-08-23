import { configDefaults, defineConfig } from "vitest/config";
import svgr from "vite-plugin-svgr";

export default defineConfig({
	plugins: [svgr()],
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
