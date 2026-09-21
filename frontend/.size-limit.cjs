/**
 * Byte budgets, measured against `pnpm build` and enforced by `pnpm size`.
 *
 * Every limit below is the size measured when the budget was introduced plus
 * roughly five per cent of headroom, rounded up to a whole kilobyte. That makes
 * this a ratchet, not an aspiration: a change that grows a chunk by more than
 * its slack has to say so in the diff, and a change that shrinks one should
 * lower the number in the same commit so the slack does not quietly accumulate.
 *
 * All sizes are gzipped, because that is what crosses the wire - nginx serves
 * the precompressed `.gz` siblings vite-plugin-compression2 emits
 * (`check:nginx-gzip-static` guards the wiring).
 *
 * "First load" is not a guess: it is exactly the set VitePWA precaches
 * (`workbox.globPatterns` in vite.config.ts - index/vendor JS plus the single
 * stylesheet `cssCodeSplit: false` produces), which is what a visitor
 * downloads before any route chunk is fetched.
 */
module.exports = [
	{
		name: "First load (entry + vendor + CSS)",
		path: [
			"dist/assets/index-*.js",
			"dist/assets/vendor-react-*.js",
			"dist/assets/vendor-router-*.js",
			"dist/assets/style-*.css",
		],
		limit: "145 kB",
		gzip: true,
	},
	{
		name: "Stylesheet (cssCodeSplit: false - every route pays for all of it)",
		path: "dist/assets/style-*.css",
		limit: "24 kB",
		gzip: true,
	},
	{
		name: "Generated API client",
		path: "dist/assets/apiError-*.js",
		limit: "13 kB",
		gzip: true,
	},
	{
		name: "Form schemas (zod)",
		path: "dist/assets/schemas-*.js",
		limit: "30 kB",
		gzip: true,
	},
	{
		name: "Locale: de",
		path: "dist/assets/de-*.js",
		limit: "33 kB",
		gzip: true,
	},
	{
		name: "Locale: en",
		path: "dist/assets/en-*.js",
		limit: "30 kB",
		gzip: true,
	},
	{
		name: "Route: organizer dashboard",
		path: "dist/assets/OrgDashboardPage-*.js",
		limit: "80 kB",
		gzip: true,
	},
	{
		name: "Route: opportunity detail",
		path: "dist/assets/VolunteerOpportunityDetailPage-*.js",
		limit: "9 kB",
		gzip: true,
	},
	{
		name: "Map (leaflet)",
		path: "dist/assets/SingleMarkerMap-*.js",
		limit: "48 kB",
		gzip: true,
	},
	{
		name: "QR scanner (jsqr)",
		path: "dist/assets/QRScannerModal-*.js",
		limit: "52 kB",
		gzip: true,
	},
];
