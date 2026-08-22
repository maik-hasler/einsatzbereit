import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import svgr from "vite-plugin-svgr";
import { VitePWA } from "vite-plugin-pwa";
import { compression } from "vite-plugin-compression2";

const __dirname = dirname(fileURLToPath(import.meta.url));

// Shared across every locale manifest below - the installed icon is the same
// image regardless of the visitor's language.
const manifestIcons = [
	{
		src: "/icons/icon-192.png",
		sizes: "192x192",
		type: "image/png",
	},
	{
		src: "/icons/icon-512.png",
		sizes: "512x512",
		type: "image/png",
	},
	{
		src: "/icons/icon-512.png",
		sizes: "512x512",
		type: "image/png",
		purpose: "maskable",
	},
];

// Two fully self-contained manifest objects rather than one base object with
// locale overrides spread in - scripts/check-pwa-manifest.js validates each
// locale by slicing its literal object text out of this file, which only
// works if every field (screenshots/shortcuts included) is written out
// in-place instead of inherited through a spread.
const deManifest = {
	// The installed app's identity, pinned independently of start_url
	// (#1799). Without an explicit id, the browser derives one from
	// start_url, so moving the entry point later would look like a
	// brand new app to an already-installed client - it would install
	// a second copy alongside the first rather than update it.
	id: "/",
	name: "Einsatzbereit",
	short_name: "Einsatzbereit",
	description:
		"Einsatzbereit verbindet engagierte Freiwillige mit regionalen Hilfsangeboten. Finde lokale Einsätze, hilf spontan und mach einen Unterschied in deiner Gemeinde.",
	lang: "de",
	start_url: "/",
	display: "standalone",
	background_color: "#ffffff",
	theme_color: "#2d8a5e",
	icons: manifestIcons,
	// Captured from the real app so the Android install prompt shows
	// an actual listing (Chrome's "richer install UI") instead of the
	// bare name + icon it falls back to without screenshots. Chrome
	// only qualifies for that UI when at least one narrow (mobile)
	// screenshot is present, and only shows the wide ones on desktop,
	// hence both form factors. Its other constraints, all enforced by
	// scripts/check-pwa-manifest.js: every dimension between 320 and
	// 3840 px, the longer side at most 2.3x the shorter, and one
	// shared aspect ratio per form factor (16:9 wide, 9:16 narrow).
	//
	// These live in public/screenshots/ rather than public/icons/ so
	// workbox.globPatterns above ("icons/*.png") does not sweep half a
	// megabyte of install-prompt artwork into the service worker's
	// precache - the browser fetches them at install time, and a
	// visitor who never installs never needs them at all.
	screenshots: [
		{
			src: "/screenshots/wide-home.png",
			sizes: "1920x1080",
			type: "image/png",
			form_factor: "wide",
			label: "Startseite mit Suche nach Einsätzen in deiner Nähe",
		},
		{
			src: "/screenshots/wide-opportunities.png",
			sizes: "1920x1080",
			type: "image/png",
			form_factor: "wide",
			label: "Übersicht der gefundenen Einsätze mit Filtern",
		},
		{
			src: "/screenshots/narrow-home.png",
			sizes: "1080x1920",
			type: "image/png",
			form_factor: "narrow",
			label: "Startseite mit Suche nach Einsätzen in deiner Nähe",
		},
		{
			src: "/screenshots/narrow-opportunities.png",
			sizes: "1080x1920",
			type: "image/png",
			form_factor: "narrow",
			label: "Liste der gefundenen Einsätze",
		},
		{
			src: "/screenshots/narrow-detail.png",
			sizes: "1080x1920",
			type: "image/png",
			form_factor: "narrow",
			label: "Detailansicht eines Einsatzes mit Ort und Zeitraum",
		},
	],
	// Long-press / jump-list entries on the installed app. Both point
	// at static paths, which is all a build-time manifest can express:
	// the organizer dashboard was considered too (#1799) and left out
	// for exactly that reason - its URL is organization-scoped
	// (/app/:organizationId/dashboard, resolved at runtime from the
	// user's memberships and the active-org cookie, see
	// lib/activeOrg.ts), so there is no single URL to bake in here.
	// /my-signups is the member-facing shortcut instead; signed-out
	// users hitting it land in Keycloak via ProtectedRoute and come
	// back to it, which is the right behaviour for a shortcut only a
	// signed-in member would tap.
	shortcuts: [
		{
			name: "Einsätze finden",
			short_name: "Einsätze",
			description: "Freiwilligeneinsätze in deiner Nähe durchsuchen",
			url: "/opportunities",
			icons: [
				{
					src: "/icons/shortcut-search.png",
					sizes: "96x96",
					type: "image/png",
				},
			],
		},
		{
			name: "Meine Anmeldungen",
			short_name: "Anmeldungen",
			description: "Deine Anmeldungen zu Einsätzen ansehen",
			url: "/my-signups",
			icons: [
				{
					src: "/icons/shortcut-signups.png",
					sizes: "96x96",
					type: "image/png",
				},
			],
		},
	],
};

// English counterpart of deManifest (#1923) - frontend/src/i18n.ts swaps the
// <link rel="manifest"> href between the two manifestFilenames below as the
// visitor's active i18next language changes, so an English-speaking visitor
// installs an app whose OS-level name/description/shortcuts are in English
// too instead of always getting the German one.
const enManifest = {
	id: "/",
	name: "Einsatzbereit",
	short_name: "Einsatzbereit",
	description:
		"Einsatzbereit connects committed volunteers with regional volunteer opportunities. Find local opportunities, help spontaneously, and make a difference in your community.",
	lang: "en",
	start_url: "/",
	display: "standalone",
	background_color: "#ffffff",
	theme_color: "#2d8a5e",
	icons: manifestIcons,
	screenshots: [
		{
			src: "/screenshots/wide-home.png",
			sizes: "1920x1080",
			type: "image/png",
			form_factor: "wide",
			label: "Home page with search for opportunities near you",
		},
		{
			src: "/screenshots/wide-opportunities.png",
			sizes: "1920x1080",
			type: "image/png",
			form_factor: "wide",
			label: "Overview of matching opportunities with filters",
		},
		{
			src: "/screenshots/narrow-home.png",
			sizes: "1080x1920",
			type: "image/png",
			form_factor: "narrow",
			label: "Home page with search for opportunities near you",
		},
		{
			src: "/screenshots/narrow-opportunities.png",
			sizes: "1080x1920",
			type: "image/png",
			form_factor: "narrow",
			label: "List of matching opportunities",
		},
		{
			src: "/screenshots/narrow-detail.png",
			sizes: "1080x1920",
			type: "image/png",
			form_factor: "narrow",
			label: "Detail view of an opportunity with location and time period",
		},
	],
	shortcuts: [
		{
			name: "Find opportunities",
			short_name: "Opportunities",
			description: "Search volunteer opportunities near you",
			url: "/opportunities",
			icons: [
				{
					src: "/icons/shortcut-search.png",
					sizes: "96x96",
					type: "image/png",
				},
			],
		},
		{
			name: "My sign-ups",
			short_name: "Sign-ups",
			description: "View your volunteer opportunity sign-ups",
			url: "/my-signups",
			icons: [
				{
					src: "/icons/shortcut-signups.png",
					sizes: "96x96",
					type: "image/png",
				},
			],
		},
	],
};

// Emits an extra static *.webmanifest asset alongside the one VitePWA itself
// generates from `manifest:`/`manifestFilename` below (#1923) - VitePWA only
// manages a single manifest per plugin instance, so the second locale is
// written directly into the bundle here instead. Runs in `generateBundle`
// (before Rollup writes dist/ to disk), same phase VitePWA emits its own
// manifest asset in, so by the time VitePWA's `closeBundle` hook globs
// workbox.globPatterns against the files on disk (see below), this file is
// already there too.
function emitLocaleManifest(
	fileName: string,
	manifest: Record<string, unknown>,
): Plugin {
	const source = JSON.stringify(manifest, null, 2);
	return {
		name: `emit-${fileName}`,
		generateBundle() {
			this.emitFile({ type: "asset", fileName, source });
		},
	};
}

// silent-renew.html (#2042) is a second HTML entry alongside index.html (see
// build.rollupOptions.input below), so VitePWA's registerSW/manifest-link
// injection and the single-CSS-bundle build (cssCodeSplit: false above) both
// attach to it the same as they do to index.html - a PWA install manifest
// link, a service-worker registration script, and the entire app's
// stylesheet, none of which this page (renders nothing, exists purely to
// relay a URL back to its opener) needs. Left in place, that's dead weight
// re-fetched on every automaticSilentRenew hidden-iframe cycle (every ~4
// minutes) - stripped here instead, after VitePWA's own transform has run.
function stripPwaChromeFromSilentRenew(): Plugin {
	return {
		name: "strip-pwa-chrome-from-silent-renew",
		// VitePWA's own html injection runs as an `enforce: "post"` plugin, not
		// just a `transformIndexHtml` `order: "post"` hook - Vite buckets hooks
		// by their enclosing plugin's `enforce` first, so without this too, our
		// (merely order:"post") hook would run in the earlier "normal" bucket,
		// before VitePWA has injected anything to strip.
		enforce: "post",
		transformIndexHtml: {
			order: "post",
			handler(html, ctx) {
				if (!ctx.filename.endsWith("silent-renew.html")) return html;
				return html
					.replace(/\s*<link rel="manifest"[^>]*>/, "")
					.replace(
						/\s*<script id="vite-plugin-pwa:register-sw"[^>]*><\/script>/,
						"",
					)
					.replace(/\s*<link rel="stylesheet"[^>]*>/, "");
			},
		},
	};
}

export default defineConfig({
	plugins: [
		react(),
		tailwindcss(),
		svgr(),
		// Pre-compress build output so nginx can serve a .gz sibling directly
		// (gzip_static in nginx.conf.template) instead of re-gzipping every
		// asset from scratch on each cold-cache request. Matches nginx's
		// gzip_types/gzip_min_length so only the same file types/sizes it
		// would otherwise compress on the fly get a pre-built .gz.
		compression({
			algorithms: ["gzip"],
			include: /\.(js|mjs|css|json|svg)$/,
			threshold: 1024,
		}),
		VitePWA({
			registerType: "autoUpdate",
			workbox: {
				// Routes are lazy-loaded (see src/App.tsx) so each one builds into
				// its own "assets/<PageName>-<hash>.js" chunk instead of one
				// monolithic entry bundle. Only the entry chunk, the stable
				// vendor-react/vendor-router chunks (see manualChunks below), and
				// small static assets are precached - route chunks are excluded
				// here and instead runtime-cached below, so a one-line change to a
				// single page rehashes and re-downloads only that page's small
				// chunk on the next release, not the whole app. CSS is a separate
				// glob from JS: build.cssCodeSplit is off (see the comment on
				// `build` below), so there is always exactly one "style-<hash>.css"
				// covering the whole app - unlike JS it isn't named "index-"/
				// "vendor-" prefixed, and there are no per-route CSS files to
				// accidentally sweep in here.
				globPatterns: [
					"index.html",
					"favicon.svg",
					"manifest.*.webmanifest",
					"icons/*.png",
					"assets/{index,vendor}-*.js",
					"assets/*.css",
				],
				// config.js is runtime config (docker-entrypoint.d/99-runtime-config.sh
				// envsubst's it at container start, see frontend/AGENTS.md) - it can
				// change between releases with no build-time hash to bust a stale
				// precache entry, unlike everything above. None of the globPatterns
				// above would currently match a root-level "config.js" file anyway,
				// but excluding it explicitly guards against a future globPatterns
				// change (e.g. widening to a "**/*.js"-style pattern) silently
				// reintroducing that staleness.
				globIgnores: ["config.js"],
				// Route chunks (everything else under assets/) aren't precached, so
				// serve them stale-while-revalidate: instant load from cache once
				// visited once, with a background refetch keeping the cache warm
				// for the next visit.
				runtimeCaching: [
					{
						urlPattern: /\/assets\/.+\.js$/,
						handler: "StaleWhileRevalidate",
						options: {
							cacheName: "route-chunks",
							expiration: {
								maxEntries: 60,
								maxAgeSeconds: 30 * 24 * 60 * 60,
							},
						},
					},
				],
				navigateFallback: "/index.html",
				// /silent-renew.html (#2042) is a real static file, not a client
				// route - without this, the service worker's SPA-shell fallback
				// would intercept the hidden iframe's navigation to it and serve
				// index.html instead, booting the full app right back into the
				// iframe it exists to avoid.
				navigateFallbackDenylist: [/^\/v1\//, /^\/silent-renew\.html$/],
			},
			// Default/fallback manifest (#1923) - served at manifest.de.webmanifest
			// and injected into index.html's <link rel="manifest"> by VitePWA,
			// matching the German default everything else end-user-facing serves
			// (index.html's <html lang="de">, see CONTRIBUTING.md's Language
			// Convention). frontend/src/i18n.ts swaps that link's href to
			// manifest.en.webmanifest (emitted by emitLocaleManifest below) once
			// the visitor's active i18next language resolves to English.
			manifest: deManifest,
			manifestFilename: "manifest.de.webmanifest",
		}),
		emitLocaleManifest("manifest.en.webmanifest", enManifest),
		stripPwaChromeFromSilentRenew(),
	],
	// react/react-dom and react-router are shared by (almost) every lazy
	// route chunk - splitting them into their own stably-named vendor
	// chunks means a release that only touches page code invalidates just
	// that page's small chunk, not a framework bundle that every route
	// depends on. Named explicitly (rather than left to automatic shared-
	// chunk inference) so the PWA precache globs above can target them.
	build: {
		// Per-chunk CSS splitting put CalendarWidget's react-big-calendar
		// stylesheet import into its own lazy-loaded CSS file, injected into
		// <head> only once that route chunk loads - i.e. *after* the main
		// stylesheet containing global.css's brand overrides for those same
		// react-big-calendar classes. Since both rule sets have equal CSS
		// specificity, load order decides the winner, and the later-injected
		// library defaults started beating our overrides (color-contrast
		// regression: calendar buttons reverted to react-big-calendar's
		// unstyled, low-contrast colors). Route JS still splits into its own
		// chunks (that's the actual point of the lazy() calls above) - only
		// CSS stays bundled into the single entry stylesheet, restoring the
		// deterministic single-file cascade order that existed before routes
		// were code-split.
		cssCodeSplit: false,
		rollupOptions: {
			// silent-renew.html (#2042) is a second, standalone entry point -
			// deliberately outside globPatterns/navigateFallback above, so it's
			// fetched from the network like any other static file rather than
			// precached or served the SPA shell. "index" is named explicitly to
			// keep the main entry's chunk name unchanged (globPatterns'
			// "assets/{index,vendor}-*.js" and the frontend-checks.yml smoke test
			// both still expect "assets/index-*.js").
			input: {
				index: resolve(__dirname, "index.html"),
				silentRenew: resolve(__dirname, "silent-renew.html"),
			},
			output: {
				manualChunks(id) {
					if (
						/[\\/]node_modules[\\/](react|react-dom|scheduler)[\\/]/.test(id)
					) {
						return "vendor-react";
					}
					if (/[\\/]node_modules[\\/]react-router[\\/]/.test(id)) {
						return "vendor-router";
					}
					return undefined;
				},
			},
		},
	},
	server: { port: 4321 },
});
