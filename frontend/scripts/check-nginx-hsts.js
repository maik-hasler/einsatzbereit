#!/usr/bin/env node

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
