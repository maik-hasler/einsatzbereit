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
			include: ["src/lib/**/*.ts"],
		},
	},
});
