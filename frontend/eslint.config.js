import js from "@eslint/js";
import tseslint from "typescript-eslint";
import reactHooks from "eslint-plugin-react-hooks";
import prettier from "eslint-config-prettier";
import i18next from "eslint-plugin-i18next";
import jsxA11y from "eslint-plugin-jsx-a11y";
import tailwindcss from "eslint-plugin-tailwindcss";

// Shared by the `lib/` and `client/` blocks below. Kept out of them because
// two flat-config blocks setting the same rule replace one another rather than
// merging, so `lib/` has to state both of its restrictions in one place.
const noUiLayers = {
	group: [
		"**/components/**",
		"**/contexts/**",
		"**/hooks/**",
		"**/layouts/**",
		"**/pages/**",
		"**/test/**",
	],
	message:
		"lib/ and client/ sit below every UI layer: they may not import components, contexts, hooks, layouts or pages. This is the rule that keeps them extractable - move the shared piece down into lib/, or keep the UI-facing code in the layer that renders it.",
};

export default tseslint.config(
	js.configs.recommended,
	...tseslint.configs.strict,
	{
		plugins: { "react-hooks": reactHooks },
		rules: {
			"react-hooks/rules-of-hooks": "error",
			"react-hooks/exhaustive-deps": "warn",
		},
	},
	{
		files: ["src/**/*.{ts,tsx}"],
		plugins: { "jsx-a11y": jsxA11y },
		rules: jsxA11y.flatConfigs.recommended.rules,
	},
	{
		files: ["src/**/*.{ts,tsx}"],
		rules: {
			"no-restricted-syntax": [
				"error",
				{
					selector:
						":matches(Literal[value=/text-\\[/], TemplateElement[value.raw=/text-\\[/])",
					message:
						"Arbitrary Tailwind text size (text-[...]) bypasses the type scale defined in @theme - use a scale step (text-xs, text-sm, ...) instead, or add a named step to @theme if the scale genuinely needs one.",
				},
			],
		},
	},
	{
		files: ["src/**/*.{ts,tsx}"],
		plugins: { i18next },
		rules: {
			"i18next/no-literal-string": [
				"error",
				{
					mode: "jsx-text-only",
				},
			],
		},
	},
	{
		files: ["src/**/*.{ts,tsx}"],
		plugins: { tailwindcss },
		settings: {
			tailwindcss: {
				cssConfigPath: "./src/styles/global.css",
			},
		},
		rules: {
			"tailwindcss/classnames-order": "warn",
			"tailwindcss/no-contradicting-classname": "error",
			"tailwindcss/no-unnecessary-arbitrary-value": "warn",
			"tailwindcss/enforces-negative-arbitrary-values": "warn",
		},
	},
	{
		files: ["src/**/*.test.{ts,tsx}", "src/test/**/*.{ts,tsx}"],
		rules: {
			"i18next/no-literal-string": "off",
		},
	},
	// Tool configs that have to stay CommonJS: dependency-cruiser and size-limit
	// both load theirs with `require`, and this package is `"type": "module"`,
	// so the `.cjs` extension is what makes them loadable at all.
	{
		files: ["**/*.cjs"],
		languageOptions: {
			sourceType: "commonjs",
			globals: {
				module: "writable",
				require: "readonly",
				__dirname: "readonly",
				process: "readonly",
			},
		},
	},

	// --- Layering ------------------------------------------------------------
	//
	// The import direction below is the one this codebase already follows; the
	// rules only stop it being lost. Measured at the time they were added: zero
	// cycles across 379 modules / 1445 edges, and exactly one violation
	// (`hooks/useEditModeQuickActions.tsx`, disabled at the line with its
	// reason). The order, bottom up:
	//
	//     lib/ + client/  ->  hooks/  ->  components/ + contexts/  ->  layouts/  ->  pages/
	//
	// `lib/` and `client/` are one rank, not two: `client/api-instance.ts`
	// turns HTTP failures into toasts and session-expiry signals, so it imports
	// four `lib/` modules, while six `lib/` modules import DTO *types* back out
	// of the generated client. The type edges erase at build time, so there is
	// no runtime cycle - but writing the two as separate ranks would make one
	// of those directions a lie whichever way round they went.
	//
	// `components/` and `contexts/` are likewise one rank: a context provider
	// renders its own UI (`ToastContext.tsx` imports an icon) and a component
	// consumes contexts. Neither is above the other.
	//
	// The rule that earns the rest is the first block: nothing under `lib/` or
	// `client/` may reach into a UI layer. That is what keeps those two
	// extractable - as a package, or onto a platform with no DOM at all - and
	// it holds today with nothing to fix.
	//
	// Test files are out of scope on purpose: a layout's test legitimately
	// renders a page (`layouts/OrgAppLayout.test.tsx`), and a component test
	// reaches for `src/test/`'s harness. Layering is a statement about what
	// ships, not about what exercises it.
	//
	// One flat-config hazard worth naming: two blocks that both match a file
	// and both set `@typescript-eslint/no-restricted-imports` do not merge -
	// the later one replaces the earlier one outright. That is why `lib/`'s
	// two concerns (no UI layers, no runtime React) share a single block below
	// instead of reading as two.
	{
		files: ["src/client/**/*.{ts,tsx}"],
		ignores: ["src/**/*.test.{ts,tsx}"],
		rules: {
			"@typescript-eslint/no-restricted-imports": [
				"error",
				{ patterns: [noUiLayers] },
			],
		},
	},
	{
		files: ["src/lib/**/*.{ts,tsx}"],
		ignores: ["src/**/*.test.{ts,tsx}"],
		rules: {
			"@typescript-eslint/no-restricted-imports": [
				"error",
				{
					patterns: [noUiLayers],
					// `lib/` is the folder with no React in it. Six modules import a
					// *type* from react-oidc-context, react-big-calendar or the
					// generated client and those erase at build time, so
					// `allowTypeImports` lets them through; a runtime import does not.
					// This keeps `pnpm mutation`'s `src/lib/**` scoping honest - a hook
					// in here cannot be mutation-tested as pure logic - and is why a
					// hook that used to live here (`usePendingReportIntent`) now sits
					// under hooks/.
					paths: [
						"react",
						"react-dom",
						"react-router",
						"react-i18next",
						"react-oidc-context",
					].map((name) => ({
						name,
						allowTypeImports: true,
						message:
							"lib/ holds logic a component calls, not logic that renders or subscribes. A React hook belongs in hooks/; a type-only import is fine.",
					})),
				},
			],
		},
	},
	{
		files: ["src/hooks/**/*.{ts,tsx}"],
		ignores: ["src/**/*.test.{ts,tsx}"],
		rules: {
			"@typescript-eslint/no-restricted-imports": [
				"error",
				{
					patterns: [
						{
							group: ["**/components/**", "**/layouts/**", "**/pages/**"],
							message:
								"A hook sits below the components that call it. Take what it needs as an argument, or move the piece that renders into the component layer.",
						},
					],
				},
			],
		},
	},
	{
		files: ["src/components/**/*.{ts,tsx}", "src/contexts/**/*.{ts,tsx}"],
		ignores: ["src/**/*.test.{ts,tsx}"],
		rules: {
			"@typescript-eslint/no-restricted-imports": [
				"error",
				{
					patterns: [
						{
							group: ["**/layouts/**", "**/pages/**"],
							message:
								"A component cannot depend on the route that happens to render it - that is what makes it reusable on the next one.",
						},
					],
				},
			],
		},
	},
	{
		files: ["src/layouts/**/*.{ts,tsx}"],
		ignores: ["src/**/*.test.{ts,tsx}"],
		rules: {
			"@typescript-eslint/no-restricted-imports": [
				"error",
				{
					patterns: [
						{
							group: ["**/pages/**"],
							message:
								"A layout renders whatever route is active through <Outlet />; importing a specific page inverts that.",
						},
					],
				},
			],
		},
	},

	// --- DOM-bound libraries stay in one file each ---------------------------
	//
	// Leaflet, jsqr, react-big-calendar and qrcode.react each reach straight
	// for the DOM, and each is already used by exactly one component. That is
	// worth keeping rather than wrapping: a wrapper around a single call site
	// is indirection with nothing on the other side of it, while a second call
	// site is what actually makes the library expensive to replace - bigger in
	// the bundle of whichever route pulls it in, and a second place to change
	// on a runtime that has no DOM at all.
	//
	// The core `no-restricted-imports` rather than the typescript-eslint one on
	// purpose: the blocks above already set the latter for several of these
	// folders, and two blocks setting the same rule replace rather than merge.
	// `lib/calendarRange.ts` is exempt because its only use is a type import,
	// which the core rule cannot tell apart.
	{
		files: ["src/**/*.{ts,tsx}"],
		ignores: [
			"src/components/SingleMarkerMap.tsx",
			"src/components/QRScannerModal.tsx",
			"src/components/CheckInModal.tsx",
			"src/pages/app/OrgDashboardPage/CalendarWidget.tsx",
			"src/lib/calendarRange.ts",
			"src/**/*.test.{ts,tsx}",
		],
		rules: {
			"no-restricted-imports": [
				"error",
				{
					paths: [
						{
							name: "leaflet",
							message:
								"The map lives in components/SingleMarkerMap.tsx. Render that instead of reaching for Leaflet again - it is 45 kB gzipped, and one more import doubles the surface a non-browser runtime would have to replace.",
						},
						{
							name: "react-leaflet",
							message:
								"The map lives in components/SingleMarkerMap.tsx. Render that instead.",
						},
						{
							name: "jsqr",
							message:
								"QR decoding lives in components/QRScannerModal.tsx. Render that instead - it is 48 kB gzipped and only loads on the routes that scan.",
						},
						{
							name: "qrcode.react",
							message:
								"QR rendering lives in components/CheckInModal.tsx. Render that instead.",
						},
						{
							name: "react-big-calendar",
							message:
								"The calendar lives in pages/app/OrgDashboardPage/CalendarWidget.tsx, which is lazy-loaded precisely because this library and its date-fns locales dominate the dashboard chunk. A second importer would undo that.",
						},
					],
				},
			],
		},
	},

	// --- Browser globals in lib/ and client/ ---------------------------------
	//
	// A ratchet, not a clean sweep. Seventeen of the sixty production modules
	// under `lib/` genuinely touch the DOM today (cookies, sessionStorage,
	// `document.title`, canvas-based image cropping, online status), and
	// `client/api-instance.ts` needs `globalThis.fetch` and `Intl` - banning
	// those outright would mean moving code this change is not about.
	//
	// What the list below bans is every browser global those two folders do
	// *not* already use, so the surface cannot quietly grow. Each addition is
	// then a deliberate act with a reviewer in the loop, rather than a reflex.
	//
	// Note `no-restricted-globals` matches bare identifier references only:
	// `sessionStorage.getItem(...)` is caught, `window.sessionStorage.getItem(...)`
	// is not. Both forms exist in `lib/` today, which is itself worth knowing.
	{
		files: ["src/lib/**/*.{ts,tsx}", "src/client/**/*.{ts,tsx}"],
		ignores: ["src/**/*.test.{ts,tsx}"],
		rules: {
			"no-restricted-globals": [
				"error",
				...[
					"localStorage",
					"matchMedia",
					"IntersectionObserver",
					"ResizeObserver",
					"MutationObserver",
					"history",
					"screen",
					"location",
					"alert",
					"confirm",
					"prompt",
					"XMLHttpRequest",
					"BroadcastChannel",
					"indexedDB",
					"caches",
					"Notification",
					"visualViewport",
					"DOMParser",
					"WebSocket",
					"Worker",
					"requestAnimationFrame",
					"cancelAnimationFrame",
					"addEventListener",
					"removeEventListener",
					"dispatchEvent",
				].map((name) => ({
					name,
					message:
						"lib/ and client/ keep the browser surface they already have and grow no further - this global is not part of it. Put the DOM-bound piece in a component or a hook and pass the result down, or say in a comment why this folder is where it has to live.",
				})),
			],
		},
	},

	prettier,
	{
		// coverage/, reports/ and .stryker-tmp/ are generated: `pnpm test:coverage`
		// and `pnpm mutation` write them, and .gitignore does not stop ESLint from
		// walking into them. Stryker's sandbox in particular is a full copy of the
		// project, so without this `pnpm lint` fails on its vendored coverage
		// scripts after any local mutation run.
		ignores: [
			// Generated by @hey-api/openapi-ts and rewritten wholesale on every
			// `pnpm client:generate`. Unlike the previous generator's output it
			// carries no `/* eslint-disable */` header, and relying on one would
			// be relying on a generator's template not to change. `pnpm check`
			// still type-checks it, which is the part that matters.
			"src/client/generated/",
			"dist/",
			"node_modules/",
			"scripts/",
			"public/",
			"coverage/",
			"reports/",
			".stryker-tmp/",
		],
	},
);
