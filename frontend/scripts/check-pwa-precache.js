#!/usr/bin/env node
// Guards against the regression class behind issue #1403: the PWA service
// worker precached the entire build as one unit (globPatterns: "**/*.js"
// etc.), so a one-line change to any single page rehashed the whole ~1.3 MB
// bundle and forced every returning visitor to re-download all of it on the
// next deploy. The fix is route-based code splitting (src/App.tsx lazy()
// imports) plus scoping the precache manifest to only the entry chunk and
// the stable vendor chunks, with route chunks served via a runtime
// StaleWhileRevalidate cache instead. Purely static checks - no build
// required.
import { readFileSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(__dirname, "..");

const viteConfig = readFileSync(join(frontendDir, "vite.config.ts"), "utf8");
const appTsx = readFileSync(join(frontendDir, "src/App.tsx"), "utf8");
const appLayoutTsx = readFileSync(
	join(frontendDir, "src/layouts/AppLayout.tsx"),
	"utf8",
);
const orgAppLayoutTsx = readFileSync(
	join(frontendDir, "src/layouts/OrgAppLayout.tsx"),
	"utf8",
);

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

	// 2. Route/page chunks (everything under assets/ not matched by
	// globPatterns) must be served via runtime caching instead, or they'd
	// simply never be cached by the service worker at all.
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

// 3. manualChunks must isolate react/react-dom and react-router into their
// own stably-named chunks, or vendor code stays entangled with page code
// and every deploy invalidates far more than the changed page.
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

// 4. Route pages in App.tsx must actually be lazy-loaded, otherwise
// manualChunks/globPatterns have nothing to split in the first place - all
// page code would stay bundled into the single entry chunk.
const lazyPageImports = appTsx.match(/lazy\(\s*\(\)\s*=>\s*import\(["']\.\/pages\//g) ?? [];
if (lazyPageImports.length < 10) {
	fail(
		`Found only ${lazyPageImports.length} lazy-loaded page import(s) in src/App.tsx - route ` +
			"pages must be lazy-loaded (React.lazy) for the build to code-split per route, or the " +
			"whole-bundle precache regression from issue #1403 comes back.",
	);
}
// Suspense boundaries live in the layouts (around each <Outlet />), not in
// App.tsx itself, so both layouts that render lazy route elements need one.
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

if (ok) {
	console.log(
		"PWA precache is scoped to entry + vendor chunks, route chunks are lazy-loaded and runtime-cached.",
	);
} else {
	process.exit(1);
}
