#!/usr/bin/env node

import { readFileSync, readdirSync, statSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname, relative } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(__dirname, "..");
const srcDir = join(frontendDir, "src");

const viteConfig = readFileSync(join(frontendDir, "vite.config.ts"), "utf8");
const mainTsx = readFileSync(join(srcDir, "main.tsx"), "utf8");
const appTsx = readFileSync(join(frontendDir, "src/App.tsx"), "utf8");
const appLayoutTsx = readFileSync(
	join(frontendDir, "src/layouts/AppLayout.tsx"),
	"utf8",
);
const orgAppLayoutTsx = readFileSync(
	join(frontendDir, "src/layouts/OrgAppLayout.tsx"),
	"utf8",
);

function listSourceFiles(dir) {
	let files = [];
	for (const entry of readdirSync(dir)) {
		const full = join(dir, entry);
		if (statSync(full).isDirectory()) {
			files = files.concat(listSourceFiles(full));
		} else if (/\.(ts|tsx)$/.test(entry)) {
			files.push(full);
		}
	}
	return files;
}

let ok = true;
function fail(message) {
	console.error(message);
	ok = false;
}

const workboxMatch = viteConfig.match(/workbox:\s*\{([\s\S]*?)\n\t\t\t\t\},/);
if (!workboxMatch) {
	fail("Could not find the VitePWA `workbox: { ... }` block in vite.config.ts.");
} else {
	const workboxBlock = workboxMatch[1];

	// 1. globPatterns must not be a bare catch-all - that's precisely what
	// re-bundles route chunks into the precache manifest and defeats the
	// code splitting below.
	if (/globPatterns\s*:\s*\[\s*["']\*\*\/\*\.\{[^}]*\}["']\s*\]/.test(workboxBlock)) {
		fail(
			"workbox.globPatterns in vite.config.ts is a single \"**/*.{...}\" catch-all - this " +
				"precaches every JS chunk (including per-route chunks), not just the entry + vendor " +
				"chunks, re-creating the whole-bundle precache regression from issue #1403.",
		);
	}
	if (!/globPatterns\s*:\s*\[/.test(workboxBlock)) {
		fail("Could not find workbox.globPatterns in vite.config.ts.");
	}

	if (!/runtimeCaching\s*:\s*\[/.test(workboxBlock)) {
		fail(
			"workbox.runtimeCaching is missing from vite.config.ts - route chunks excluded from " +
				"globPatterns need a runtime caching strategy or they won't be cached by the " +
				"service worker at all.",
		);
	} else if (!/handler\s*:\s*["']StaleWhileRevalidate["']/.test(workboxBlock)) {
		fail(
			"workbox.runtimeCaching in vite.config.ts does not use the \"StaleWhileRevalidate\" " +
				"handler for route chunks - see the suggested fix in issue #1403.",
		);
	}
}

const buildMatch = viteConfig.match(/build:\s*\{[\s\S]*?manualChunks\s*\([\s\S]*?\n\t\t\t\t\},/);
if (!buildMatch) {
	fail(
		"Could not find a build.rollupOptions.output.manualChunks function in vite.config.ts - " +
			"react/react-dom and react-router need to be split into their own stable vendor " +
			"chunks so a page-only change doesn't invalidate framework code too.",
	);
} else {
	const manualChunksBlock = buildMatch[0];
	if (!/vendor-react/.test(manualChunksBlock)) {
		fail("manualChunks in vite.config.ts no longer groups react/react-dom into a vendor-react chunk.");
	}
	if (!/vendor-router/.test(manualChunksBlock)) {
		fail("manualChunks in vite.config.ts no longer groups react-router into a vendor-router chunk.");
	}
}

const lazyPageImports = appTsx.match(/lazy\(\s*\(\)\s*=>\s*import\(["']\.\/pages\//g) ?? [];
if (lazyPageImports.length < 9) {
	fail(
		`Found only ${lazyPageImports.length} lazy-loaded page import(s) in src/App.tsx - route ` +
			"pages must be lazy-loaded (React.lazy) for the build to code-split per route, or the " +
			"whole-bundle precache regression from issue #1403 comes back.",
	);
}

if (!/^import HomePage from ["']\.\/pages\/HomePage["'];?$/m.test(appTsx)) {
	fail(
		"src/App.tsx no longer has a plain `import HomePage from \"./pages/HomePage\"` - if it's " +
			"lazy-loaded again, it and Header's shared useSharedOrgFetch(\"organizations:...\") call " +
			"will race and fire GET /v1/organizations twice per authenticated home page load (#1396).",
	);
}
if (/lazy\(\s*\(\)\s*=>\s*import\(["']\.\/pages\/HomePage["']/.test(appTsx)) {
	fail(
		"src/App.tsx lazy-loads HomePage - see the comment above it for why this races Header's " +
			"shared useSharedOrgFetch(\"organizations:...\") call and must stay a plain eager import.",
	);
}

if (!/<Suspense\b/.test(appLayoutTsx)) {
	fail(
		"src/layouts/AppLayout.tsx renders lazy route elements via <Outlet /> but has no " +
			"<Suspense> boundary around it - they will throw without a Suspense ancestor to catch them.",
	);
}
if (!/<Suspense\b/.test(orgAppLayoutTsx)) {
	fail(
		"src/layouts/OrgAppLayout.tsx renders lazy route elements via <Outlet /> but has no " +
			"<Suspense> boundary around it - they will throw without a Suspense ancestor to catch them.",
	);
}

if (!/cssCodeSplit\s*:\s*false/.test(viteConfig)) {
	fail(
		"vite.config.ts's build.cssCodeSplit is not explicitly \"false\" - without it, CSS gets " +
			"split per route chunk again, which can silently flip the cascade order between a " +
			"third-party stylesheet and global.css's overrides for the same classes depending on " +
			"which chunk happens to load first.",
	);
}

const thirdPartyCssImport = /import\s+["'](?!\.)[^"']+\.css["'];?/;
for (const file of listSourceFiles(srcDir)) {
	if (file === join(srcDir, "main.tsx")) continue;
	const content = readFileSync(file, "utf8");
	if (thirdPartyCssImport.test(content)) {
		fail(
			`${relative(frontendDir, file)} imports a third-party stylesheet directly - move it to ` +
				"main.tsx (before the global.css import, if it needs to lose the cascade to a brand " +
				"override there) instead, or it risks the same load-order-dependent cascade bug fixed " +
				"for react-big-calendar's stylesheet.",
		);
	}
}

const rbcImportIndex = mainTsx.indexOf(
	'"react-big-calendar/lib/css/react-big-calendar.css"',
);
const globalCssImportIndex = mainTsx.indexOf('"./styles/global.css"');
if (rbcImportIndex === -1) {
	fail(
		"src/main.tsx no longer imports react-big-calendar's stylesheet - if CalendarWidget.tsx " +
			"imports it directly again instead, the cascade-order bug from issue #1403 comes back.",
	);
} else if (globalCssImportIndex === -1) {
	fail("src/main.tsx no longer imports ./styles/global.css.");
} else if (rbcImportIndex > globalCssImportIndex) {
	fail(
		"src/main.tsx imports react-big-calendar's stylesheet after global.css - it must come " +
			"before, so global.css's brand overrides for the same classes win the cascade.",
	);
}

if (ok) {
	console.log(
		"PWA precache is scoped to entry + vendor chunks, route chunks are lazy-loaded and runtime-cached.",
	);
} else {
	process.exit(1);
}
