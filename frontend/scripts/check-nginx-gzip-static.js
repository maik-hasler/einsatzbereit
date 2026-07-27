#!/usr/bin/env node
// Guards against the regression class behind issue #1406: nginx re-gzipped
// every JS/CSS asset from scratch on each cold-cache request because no
// pre-compressed .gz siblings were emitted at build time and gzip_static
// was never enabled. Purely static checks - no Docker/nginx/build required.
import { readFileSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(__dirname, "..");

const nginxTemplate = readFileSync(
	join(frontendDir, "nginx.conf.template"),
	"utf8",
);
const viteConfig = readFileSync(join(frontendDir, "vite.config.ts"), "utf8");

let ok = true;
function fail(message) {
	console.error(message);
	ok = false;
}

// 1. nginx must be told to serve pre-compressed .gz siblings directly
// instead of compressing matching responses on the fly every time.
if (!/^\s*gzip_static\s+on;\s*$/m.test(nginxTemplate)) {
	fail(
		"nginx.conf.template is missing `gzip_static on;` - without it nginx re-gzips " +
			"every matching response from scratch on each cold-cache request instead of " +
			"serving a pre-built .gz file.",
	);
}

// 2. Something must actually produce those .gz siblings at build time, or
// gzip_static has nothing to serve and silently falls back to on-the-fly
// compression (or none, if gzip is off).
if (!/from\s+["']vite-plugin-compression2["']/.test(viteConfig)) {
	fail(
		"vite.config.ts no longer imports vite-plugin-compression2 - gzip_static in " +
			"nginx.conf.template has no pre-compressed .gz assets to serve without it.",
	);
}

const compressionCallMatch = viteConfig.match(
	/compression\(\s*\{([\s\S]*?)\}\s*\)/,
);
if (!compressionCallMatch) {
	fail("Could not find a `compression({ ... })` plugin call in vite.config.ts.");
} else if (!/algorithms\s*:\s*\[[^\]]*["']gzip["']/.test(compressionCallMatch[1])) {
	fail(
		"The compression() plugin call in vite.config.ts does not request the \"gzip\" " +
			"algorithm, so no .gz assets will be emitted for nginx's gzip_static to serve.",
	);
}

if (ok) {
	console.log(
		"nginx gzip_static is enabled and vite.config.ts emits matching pre-compressed .gz assets.",
	);
} else {
	process.exit(1);
}
