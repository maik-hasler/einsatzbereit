import { defineConfig } from "vitest/config";
import svgr from "vite-plugin-svgr";

export default defineConfig({
	// Deliberately not the app's full plugin list. esbuild already applies
	// tsconfig's `jsx: "react-jsx"`, so `@vitejs/plugin-react` buys nothing a
	// test run needs (its job is Fast Refresh), and Tailwind produces classes
	// that jsdom has no layout engine to apply anyway. `svgr` is the exception:
	// without it an `import Icon from "./x.svg?react"` is an unresolved module
	// and the component under test fails to import at all, which reads as a
	// broken test rather than a missing transform (see NotFoundPage.tsx).
	plugins: [svgr()],
	test: {
		environment: "jsdom",
		setupFiles: ["./src/test/setup.ts"],
		coverage: {
			provider: "v8",
			// Was `src/lib/**/*.ts`, which was right while this suite was only the
			// pure-function tests. #2148 moved component, page and hook coverage
			// down here, and a report scoped to `lib/` cannot see any of it - it
			// measured 385 statements while the suite exercises thousands.
			include: ["src/**/*.{ts,tsx}"],
			exclude: [
				// The tests and their harness, which would report as covering
				// themselves.
				"src/**/*.test.{ts,tsx}",
				"src/test/**",
				// NSwag-generated, never hand-edited (see frontend/AGENTS.md), and
				// large enough to dominate the totals either way.
				"src/client/api-client.ts",
				// Composition roots and ambient declarations - nothing to assert.
				"src/main.tsx",
				"src/vite-env.d.ts",
				"src/**/*.d.ts",
			],
		},
	},
});
