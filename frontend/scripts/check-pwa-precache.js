#!/usr/bin/env node
// Guards against the regression class behind issue #1403: the PWA service
// worker precached the entire build as one unit (globPatterns: "**/*.js"
// etc.), so a one-line change to any single page rehashed the whole ~1.3 MB
// bundle and forced every returning visitor to re-download all of it on the
// next release. The fix is route-based code splitting (src/App.tsx lazy()
// imports) plus scoping the precache manifest to only the entry chunk and
// the stable vendor chunks, with route chunks served via a runtime
// StaleWhileRevalidate cache instead. Purely static checks - no build
// required.
//
// Also guards against a second regression that lazy-loading routes caused:
// a third-party stylesheet (react-big-calendar's) imported from a
// lazy-loaded page component ended up in that route's own CSS chunk,
// injected into <head> only when that chunk loads - i.e. *after* the main
// stylesheet's global.css brand overrides for the same classes, which have
// equal CSS specificity. Load order alone then decided the cascade winner,
// and the library's unstyled defaults started beating our overrides
// (color-contrast a11y failures on CreateVolunteerOpportunityModal/
// CreateOrganizationModal, which render the dashboard's calendar widget
// behind them). Fixed by keeping CSS out of per-route code splitting
// entirely (cssCodeSplit: false) and importing react-big-calendar's
// stylesheet eagerly from main.tsx, before global.css, so cascade order is
// fixed at the entry point instead of depending on chunk load timing.
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
// and every release invalidates far more than the changed page.
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
// page code would stay bundled into the single entry chunk. HomePage is a
// deliberate exception - see check 4b.
const lazyPageImports = appTsx.match(/lazy\(\s*\(\)\s*=>\s*import\(["']\.\/pages\//g) ?? [];
if (lazyPageImports.length < 9) {
	fail(
		`Found only ${lazyPageImports.length} lazy-loaded page import(s) in src/App.tsx - route ` +
			"pages must be lazy-loaded (React.lazy) for the build to code-split per route, or the " +
			"whole-bundle precache regression from issue #1403 comes back.",
	);
}

// 4b. HomePage specifically must stay a plain, eager import - Header (always
// eager) and HomePage share a single in-flight GET /v1/organizations request
// via useSharedOrgFetch (#1396), which only dedupes when both mount in the
// same synchronous commit. A lazy HomePage mounts late (after its chunk
// loads), missing Header's already-in-flight request and firing a second,
// redundant one.
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

// 5. CSS must not be split per route chunk - see the file header comment
// for why a lazy-chunk-scoped third-party stylesheet import previously
// broke cascade order for react-big-calendar's classes.
if (!/cssCodeSplit\s*:\s*false/.test(viteConfig)) {
	fail(
		"vite.config.ts's build.cssCodeSplit is not explicitly \"false\" - without it, CSS gets " +
			"split per route chunk again, which can silently flip the cascade order between a " +
			"third-party stylesheet and global.css's overrides for the same classes depending on " +
			"which chunk happens to load first.",
	);
}

// 6. No component may import a third-party (bare-specifier) stylesheet -
// only main.tsx may, since it's the one place guaranteed to load eagerly
// and in a fixed order relative to global.css. A CSS import inside any
// lazy-loaded page/component ties that stylesheet's cascade position to
// when React happens to load that route's chunk.
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

// 7. The one known case: react-big-calendar's stylesheet must be imported
// in main.tsx, before global.css, so global.css's overrides for the same
// classes keep winning the cascade regardless of chunk load timing.
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
