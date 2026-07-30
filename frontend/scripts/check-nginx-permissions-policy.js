#!/usr/bin/env node
// Guards against the regression class behind issue #1220: the
// Permissions-Policy header shipped `camera=()` (an empty allowlist), so the
// browser rejected camera access before ever prompting the user - silently
// breaking the QR check-in scanner in production while leaving it visually
// functional. The value is duplicated across four nginx location blocks (no
// $host-keyed map like $csp_header, since it never varies by host), so a
// future edit could silently regress one block and miss the other three.
// Purely static checks - no Docker/nginx required.
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

const permissionsPolicyLines = template
	.split("\n")
	.map((line) => line.trim())
	.filter((line) => /^add_header\s+Permissions-Policy\s+/.test(line));

if (permissionsPolicyLines.length === 0) {
	fail("No `add_header Permissions-Policy` lines found in nginx.conf.template.");
}

const expected =
	'add_header Permissions-Policy "camera=(self), microphone=(), geolocation=(self), payment=()" always;';
for (const line of permissionsPolicyLines) {
	if (line !== expected) {
		fail(
			`Found a Permissions-Policy header not matching the expected value: "${line}". ` +
				`Every location must emit exactly: ${expected}`,
		);
	}
}

if (ok) {
	console.log(
		`nginx Permissions-Policy header allows camera=(self) consistently across all ${permissionsPolicyLines.length} location blocks.`,
	);
} else {
	process.exit(1);
}
