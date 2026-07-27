#!/usr/bin/env node
// Guards against the regression class behind issue #1370: the
// Strict-Transport-Security header was missing includeSubDomains, so only the
// exact host was protected against protocol downgrade and every subdomain
// stayed open to it. The value is duplicated across four nginx location
// blocks (no $host-keyed map like $csp_header, since it never varies by
// host), so a future edit could silently fix it in one block and miss the
// other three. Purely static checks - no Docker/nginx required.
import { readFileSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(__dirname, "..");

const template = readFileSync(
	join(frontendDir, "nginx.conf.template"),
	"utf8",
);

let ok = true;
function fail(message) {
	console.error(message);
	ok = false;
}

const hstsLines = template
	.split("\n")
	.map((line) => line.trim())
	.filter((line) => /^add_header\s+Strict-Transport-Security\s+/.test(line));

if (hstsLines.length === 0) {
	fail("No `add_header Strict-Transport-Security` lines found in nginx.conf.template.");
}

const expected = 'add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;';
for (const line of hstsLines) {
	if (line !== expected) {
		fail(
			`Found a Strict-Transport-Security header not matching the expected value: "${line}". ` +
				`Every location must emit exactly: ${expected}`,
		);
	}
}

if (ok) {
	console.log(
		`nginx Strict-Transport-Security header includes includeSubDomains consistently across all ${hstsLines.length} location blocks.`,
	);
} else {
	process.exit(1);
}
