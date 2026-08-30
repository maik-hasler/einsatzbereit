#!/usr/bin/env node

import { readFileSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(__dirname, "..");

const viteConfig = readFileSync(join(frontendDir, "vite.config.ts"), "utf8");

let ok = true;
function fail(message) {
	console.error(message);
	ok = false;
}

function sliceBalanced(source, startIndex, open, close) {
	const from = source.indexOf(open, startIndex);
	if (from === -1) return null;
	let depth = 0;
	for (let i = from; i < source.length; i++) {
		if (source[i] === open) depth++;
		else if (source[i] === close) {
			depth--;
			if (depth === 0) return source.slice(from, i + 1);
		}
	}
	return null;
}

function topLevelObjects(arrayBlock) {
	const objects = [];
	let depth = 0;
	let start = -1;
	for (let i = 0; i < arrayBlock.length; i++) {
		if (arrayBlock[i] === "{") {
			if (depth === 0) start = i;
			depth++;
		} else if (arrayBlock[i] === "}") {
			depth--;
			if (depth === 0 && start !== -1) {
				objects.push(arrayBlock.slice(start, i + 1));
				start = -1;
			}
		}
	}
	return objects;
}

const workboxIndex = viteConfig.indexOf("workbox: {");
const workboxBlock =
	workboxIndex === -1
		? null
		: sliceBalanced(viteConfig, workboxIndex, "{", "}");
if (!workboxBlock) {
	fail("Could not find the VitePWA `workbox: { ... }` block in vite.config.ts.");
}

const runtimeCachingIndex = workboxBlock
	? workboxBlock.indexOf("runtimeCaching: [")
	: -1;
const runtimeCachingBlock =
	runtimeCachingIndex === -1
		? null
		: sliceBalanced(workboxBlock, runtimeCachingIndex, "[", "]");
if (!runtimeCachingBlock) {
	fail("Could not find workbox.runtimeCaching in vite.config.ts.");
}

const entries = runtimeCachingBlock ? topLevelObjects(runtimeCachingBlock) : [];

// /config.js is the app's boot prerequisite: index.html loads it before the
// bundle, lib/runtimeConfig.ts reads the API/Keycloak origins out of it, and
// ConfigGate refuses to render anything without them (#2207). It is
// deliberately excluded from the precache (its contents are templated at
// container start, so a build-time revision would pin every deployment to the
// image's own origins) - but for a long stretch it was excluded from
// runtimeCaching too, which left it the single uncached file standing between
// an offline reload and the fully warm cache behind it: every route, in both
// locales, dead-ended on "configuration missing" (#2317). The pairing is the
// invariant worth guarding, so both halves are checked here.
const configJsPrecached =
	workboxBlock && !/globIgnores:\s*\[[^\]]*["']config\.js["']/.test(workboxBlock);
if (configJsPrecached) {
	fail(
		'vite.config.ts\'s workbox.globIgnores no longer excludes "config.js" - it is rendered ' +
			"from environment variables at container start (docker-entrypoint.d/99-runtime-config.sh), " +
			"so precaching it pins every deployment to whatever API/Keycloak origins the image was " +
			"built against, served cache-first and keyed by a build-time revision (#2207).",
	);
}

const configEntry = entries.find(
	(entry) => /urlPattern:\s*\(/.test(entry) && /["']\/config\.js["']/.test(entry),
);
if (!configEntry) {
	fail(
		"vite.config.ts's workbox.runtimeCaching has no function-based `urlPattern` matching " +
			'"/config.js" - excluded from the precache (correctly) and from runtimeCaching too, it is ' +
			"never cached at all, so every offline reload and every cold start of the installed PWA " +
			'dead-ends on the "configuration missing" screen while index.html, the bundles, the route ' +
			"chunks and the API responses all sit warm in the cache (#2317).",
	);
} else if (
	!/handler:\s*["'](NetworkFirst|StaleWhileRevalidate)["']/.test(configEntry)
) {
	fail(
		'The /config.js runtime caching rule in vite.config.ts does not use "NetworkFirst" or ' +
			'"StaleWhileRevalidate" - a cache-first strategy would keep booting the app against the ' +
			"origins of a previous deployment long after they changed.",
	);
}

// The API is served from a separate host that is only known at container
// start (config.js, injected by docker-entrypoint.d/99-runtime-config.sh -
// see #2207), not at service-worker build time. A bare RegExp `urlPattern`
// only ever matches same-origin requests (Workbox's own documented
// limitation), so a route meant to cache cross-origin API calls silently
// never fires unless it's a function matcher that tests `url.pathname`
// instead of relying on origin. This is exactly the kind of regression that
// looks correct in a diff and passes `tsc`/`eslint` while doing nothing at
// runtime - hence a dedicated static check (#2233) alongside the actual
// build-output assertion in this file below.
function findFunctionMatcherFor(pattern) {
	return entries.find(
		(entry) =>
			entry.includes(pattern) && /urlPattern:\s*\(/.test(entry),
	);
}

const listEntry = findFunctionMatcherFor("volunteer-opportunities$");
if (!listEntry) {
	fail(
		"vite.config.ts's workbox.runtimeCaching has no function-based `urlPattern` matching " +
			'"/v1/volunteer-opportunities$" - without a runtime caching rule for the opportunity ' +
			"list response, a previously visited list is empty offline instead of showing the last " +
			"fetched page (#2233). It must be a function matcher (not a bare RegExp literal), since a " +
			"RegExp only matches same-origin requests and the API is served from a separate, " +
			"runtime-configured origin.",
	);
} else if (!/handler:\s*["'](NetworkFirst|StaleWhileRevalidate)["']/.test(listEntry)) {
	fail(
		"The opportunity-list runtime caching rule in vite.config.ts does not use \"NetworkFirst\" " +
			'or "StaleWhileRevalidate" - a cache-first strategy would keep serving a stale list ' +
			"indefinitely even when back online.",
	);
} else if (!/expiration:\s*\{/.test(listEntry)) {
	fail(
		"The opportunity-list runtime caching rule in vite.config.ts has no `expiration` bound - " +
			"an unbounded cache grows forever and never lets go of removed/expired opportunities.",
	);
}

const detailEntry = findFunctionMatcherFor(
	"volunteer-opportunities\\/[0-9a-fA-F-]+$",
);
if (!detailEntry) {
	fail(
		"vite.config.ts's workbox.runtimeCaching has no function-based `urlPattern` matching " +
			'"/v1/volunteer-opportunities/{id}$" - without a runtime caching rule for the opportunity ' +
			"detail response, a previously visited opportunity is empty offline instead of showing the " +
			"address/time/contact details from the last visit (#2233's core reproduction). It must be " +
			"a function matcher (not a bare RegExp literal) for the same cross-origin reason as the " +
			"list rule above.",
	);
} else {
	if (!/handler:\s*["'](NetworkFirst|StaleWhileRevalidate)["']/.test(detailEntry)) {
		fail(
			"The opportunity-detail runtime caching rule in vite.config.ts does not use " +
				'"NetworkFirst" or "StaleWhileRevalidate".',
		);
	}
	if (!/expiration:\s*\{/.test(detailEntry)) {
		fail(
			"The opportunity-detail runtime caching rule in vite.config.ts has no `expiration` bound.",
		);
	}
	// GetVolunteerOpportunityDetails personalizes its response with the
	// caller's own CurrentUserEngagement (sign-up/check-in status). Caching
	// that under a URL-only key would let a second account signed in on the
	// same shared/public device read back the first account's engagement
	// once a cache hit wins the race - so the cache key must be widened by
	// the request's own Authorization header.
	if (!/cacheKeyWillBeUsed/.test(detailEntry)) {
		fail(
			"The opportunity-detail runtime caching rule in vite.config.ts has no " +
				"`cacheKeyWillBeUsed` plugin - GetVolunteerOpportunityDetails personalizes its response " +
				"with the caller's own CurrentUserEngagement, so caching it under a URL-only key would " +
				"let a second account on the same shared/public device read back the first account's " +
				"sign-up/check-in status from the cache.",
		);
	} else if (!/headers\.get\(["']Authorization["']\)/.test(detailEntry)) {
		fail(
			"The opportunity-detail runtime caching rule's `cacheKeyWillBeUsed` plugin in " +
				"vite.config.ts no longer reads the request's Authorization header into the cache key.",
		);
	}
}

if (ok) {
	console.log(
		"workbox.runtimeCaching has function-based (cross-origin-safe), bounded, NetworkFirst/" +
			"StaleWhileRevalidate rules for both the opportunity list and detail API responses, and " +
			"the detail rule's cache key accounts for per-user personalization. /config.js is kept " +
			"out of the precache and covered by a runtime rule instead, so an offline reload still " +
			"boots.",
	);
} else {
	process.exit(1);
}
