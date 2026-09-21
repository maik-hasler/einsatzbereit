/**
 * Graph-level rules ESLint cannot express.
 *
 * The per-layer import direction is enforced in eslint.config.js, where a
 * violation shows up in the editor as you type. What is left over needs the
 * whole graph at once: a cycle is a property of a path, not of a single import,
 * and an orphan is defined by the absence of any edge into it. Those two live
 * here and run in CI (`pnpm check:deps`).
 *
 * Measured when this was added: 0 cycles across 379 modules and 1445 edges.
 * The rule is a ratchet on that, not a cleanup.
 */

/** @type {import('dependency-cruiser').IConfiguration} */
module.exports = {
	forbidden: [
		{
			name: "no-circular",
			severity: "error",
			comment:
				"A cycle means neither module can be read, tested or extracted without the other, and it makes module initialisation order load-bearing. The graph has none today - keep it that way by moving the shared piece into a third module both can import.",
			// Generated modules are not cycle-starting points we can act on: the
			// one cycle they form is the generator's own `runtimeConfigPath`
			// wiring (client.gen.ts imports our config factory, api-instance.ts
			// imports its type back), and its return edge is type-only.
			from: { pathNot: "^src/client/generated/" },
			// Type-only edges are erased before anything runs, so a cycle that
			// exists solely in the type graph cannot deadlock module
			// initialisation - and both of the ones here are that: the generated
			// client's internal `types.gen <-> utils.gen` pair, and the
			// `api-instance.ts <-> client.gen.ts` back-edge that the generator's
			// own `runtimeConfigPath` wiring produces (it imports our config
			// factory; we import its type). `tsPreCompilationDeps` stays on so the
			// orphan rule below still sees type-only importers.
			to: { circular: true, dependencyTypesNot: ["type-only"] },
		},
		{
			name: "no-orphans",
			severity: "error",
			comment:
				"Nothing imports this module and it is not an entry point, so it is either dead or someone forgot to wire it up. Delete it, or point something at it.",
			from: {
				orphan: true,
				pathNot: [
					// Vite/Vitest entry points and ambient declarations: real files
					// with no importer by design.
					"^src/main\\.tsx$",
					"^src/silentRenew\\.ts$",
					"^src/test/setup\\.ts$",
					// Generated: the entry points and barrels the generator emits
					// are not all reachable from application code, and none of it is
					// ours to prune.
					"^src/client/generated/",
					// Vitest discovers these; nothing imports them, by definition.
					"\\.test\\.(ts|tsx)$",
					// Resolved through vitest.config.ts's alias for
					// "virtual:pwa-register/react", which is not an import edge.
					"^src/test/pwaRegisterStub\\.ts$",
					"^src/vite-env\\.d\\.ts$",
					"\\.d\\.ts$",
				],
			},
			to: {},
		},
		{
			name: "not-to-unresolvable",
			severity: "error",
			comment:
				"This import does not resolve. Left unchecked it survives type-checking (a .d.ts may still declare it) and fails at bundle time.",
			from: {},
			to: {
				couldNotResolve: true,
				// Vite virtual modules exist only inside the bundler. VitePWA
				// provides this one at build time and vitest.config.ts aliases it
				// to src/test/pwaRegisterStub.ts; neither is a file on disk.
				pathNot: ["^virtual:"],
			},
		},
		{
			name: "no-duplicate-dep-types",
			severity: "warn",
			comment:
				"This package is listed in more than one dependency group in package.json - the effective one is whichever the resolver happens to pick.",
			from: {},
			to: { moreThanOneDependencyType: true, dependencyTypesNot: ["type-only"] },
		},
	],
	options: {
		doNotFollow: { path: "node_modules" },
		exclude: {
			path: [
				"node_modules",
				// Stryker's sandbox is a full copy of the project; walking it
				// doubles every module and invents cycles that do not exist. Same
				// hazard eslint.config.js and vitest.config.ts each guard against.
				"\\.stryker-tmp",
				"^dist",
				"^coverage",
			],
		},
		tsConfig: { fileName: "tsconfig.json" },
		tsPreCompilationDeps: true,
		enhancedResolveOptions: {
			exportsFields: ["exports"],
			conditionNames: ["import", "require", "node", "default", "types"],
			extensions: [".js", ".jsx", ".ts", ".tsx", ".d.ts"],
		},
		reporterOptions: {
			text: { highlightFocused: true },
		},
	},
};
