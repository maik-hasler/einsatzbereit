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
				globPatterns: ["**/*.{js,css,html,ico,png,svg}"],
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
	server: { port: 4321 },
});
