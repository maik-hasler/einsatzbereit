import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import svgr from "vite-plugin-svgr";
import { VitePWA } from "vite-plugin-pwa";
import { compression } from "vite-plugin-compression2";

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
				// chunk on the next deploy, not the whole app.
				globPatterns: [
					"index.html",
					"favicon.svg",
					"manifest.webmanifest",
					"icons/*.png",
					"assets/{index,vendor}-*.{js,css}",
				],
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
				navigateFallbackDenylist: [/^\/v1\//],
			},
			manifest: {
				name: "Einsatzbereit",
				short_name: "Einsatzbereit",
				description: "Volunteer coordination platform",
				start_url: "/",
				display: "standalone",
				background_color: "#ffffff",
				theme_color: "#2d8a5e",
				icons: [
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
				],
			},
		}),
	],
	// react/react-dom and react-router are shared by (almost) every lazy
	// route chunk - splitting them into their own stably-named vendor
	// chunks means a deploy that only touches page code invalidates just
	// that page's small chunk, not a framework bundle that every route
	// depends on. Named explicitly (rather than left to automatic shared-
	// chunk inference) so the PWA precache globs above can target them.
	build: {
		rollupOptions: {
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
