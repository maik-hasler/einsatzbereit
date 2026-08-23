import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import svgr from "vite-plugin-svgr";
import { VitePWA } from "vite-plugin-pwa";
import { compression } from "vite-plugin-compression2";

const __dirname = dirname(fileURLToPath(import.meta.url));

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

const deManifest = {
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

		compression({
			algorithms: ["gzip"],
			include: /\.(js|mjs|css|json|svg)$/,
			threshold: 1024,
		}),
		VitePWA({
			registerType: "autoUpdate",
			workbox: {
				globPatterns: [
					"index.html",
					"favicon.svg",
					"manifest.*.webmanifest",
					"icons/*.png",
					"assets/{index,vendor}-*.js",
					"assets/*.css",
				],

				globIgnores: ["config.js"],

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

			manifest: deManifest,
			manifestFilename: "manifest.de.webmanifest",
		}),
		emitLocaleManifest("manifest.en.webmanifest", enManifest),
		stripPwaChromeFromSilentRenew(),
	],

	build: {
		cssCodeSplit: false,
		rollupOptions: {
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
