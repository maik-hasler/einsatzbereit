#!/usr/bin/env node

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

if (!/^\s*gzip_static\s+on;\s*$/m.test(nginxTemplate)) {
	fail(
		"nginx.conf.template is missing `gzip_static on;` - without it nginx re-gzips " +
			"every matching response from scratch on each cold-cache request instead of " +
			"serving a pre-built .gz file.",
	);
}

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
