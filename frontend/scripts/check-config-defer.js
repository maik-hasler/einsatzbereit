#!/usr/bin/env node
// Guards against the regression class behind issue #1405: the runtime
// config script (/config.js) blocked HTML parsing (no defer), delaying
// first contentful paint on every page load. Purely static checks - no
// Docker/nginx/browser required.
import { readFileSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(__dirname, "..");

const html = readFileSync(join(frontendDir, "index.html"), "utf8");

let ok = true;
function fail(message) {
	console.error(message);
	ok = false;
}

const scriptTags = html.match(/<script\b[^>]*>/g) ?? [];

const configTag = scriptTags.find((tag) => tag.includes('src="/config.js"'));
const mainTag = scriptTags.find((tag) => tag.includes('src="/src/main.tsx"'));

if (!configTag) {
	fail('Could not find a <script src="/config.js"> tag in index.html.');
} else {
	if (!/\bdefer\b/.test(configTag)) {
		fail(
			"The /config.js script tag is missing `defer` - without it, the browser blocks HTML " +
				"parsing (and therefore first contentful paint) while it fetches this file (issue #1405).",
		);
	}

	if (/\basync\b/.test(configTag)) {
		fail(
			"The /config.js script tag must not be `async` - async scripts run as soon as they load, " +
				"out of document order, which can execute config.js after main.tsx and leave " +
				"window.__APP_CONFIG__ unset when runtimeConfig.ts reads it.",
		);
	}
}

if (!mainTag) {
	fail(
		'Could not find the app entry <script type="module" src="/src/main.tsx"> tag in index.html.',
	);
}

if (configTag && mainTag && html.indexOf(configTag) > html.indexOf(mainTag)) {
	fail(
		"/config.js must appear before the main.tsx module script in index.html - deferred classic " +
			"scripts and module scripts both execute in document order, so config.js needs to run " +
			"first to populate window.__APP_CONFIG__ before runtimeConfig.ts reads it.",
	);
}

if (ok) {
	console.log(
		"/config.js is deferred, non-async, and ordered before the app entry script.",
	);
} else {
	process.exit(1);
}
