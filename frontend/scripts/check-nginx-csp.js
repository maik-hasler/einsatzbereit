#!/usr/bin/env node
// Guards against the regression class behind issue #1343: the
// Content-Security-Policy header was duplicated across four nginx location
// blocks, and img-src was missing the MinIO storage origin because a new
// env var got added to the header string but not to the envsubst variable
// list that actually renders it, so it never got substituted at container
// start. Purely static checks - no Docker/nginx/envsubst required.
import { readFileSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(__dirname, "..");

const template = readFileSync(
	join(frontendDir, "nginx.conf.template"),
	"utf8",
);
const entrypoint = readFileSync(
	join(frontendDir, "docker-entrypoint.d/99-runtime-config.sh"),
	"utf8",
);

let ok = true;
function fail(message) {
	console.error(message);
	ok = false;
}

// 1. The CSP value must be defined exactly once, via the "map $host
// $csp_header { default "..."; }" idiom, not duplicated per location.
const mapMatch = template.match(
	/map\s+\$host\s+\$csp_header\s*\{\s*default\s+"([^"]+)";\s*\}/,
);
if (!mapMatch) {
	fail(
		'Could not find a single `map $host $csp_header { default "..."; }` block in nginx.conf.template. ' +
			"The CSP policy must be defined once and referenced by all location blocks, not duplicated per-location.",
	);
} else {
	const policy = mapMatch[1];

	const imgSrcMatch = policy.match(/img-src ([^;]+);/);
	if (!imgSrcMatch || !imgSrcMatch[1].includes("${CSP_STORAGE_ORIGIN}")) {
		fail(
			"img-src directive is missing ${CSP_STORAGE_ORIGIN} - uploaded org logos/opportunity banners/avatars " +
				"are served from the MinIO storage origin and will be blocked by the browser without it.",
		);
	}
}

// 2. Every add_header Content-Security-Policy line must reference the
// shared $csp_header variable - none may hardcode the policy inline, which
// is exactly how the four copies drifted out of sync before.
const cspHeaderLines = template
	.split("\n")
	.map((line) => line.trim())
	.filter((line) => /^add_header\s+Content-Security-Policy\s+/.test(line));

if (cspHeaderLines.length === 0) {
	fail("No `add_header Content-Security-Policy` lines found in nginx.conf.template.");
}

for (const line of cspHeaderLines) {
	if (line !== "add_header Content-Security-Policy $csp_header always;") {
		fail(
			`Found a Content-Security-Policy header not referencing the shared $csp_header variable: "${line}". ` +
				"Every location must emit the header via $csp_header, not a hardcoded policy string.",
		);
	}
}

// 3. Every ${CSP_*} variable referenced in the template must actually be
// passed to the envsubst call that renders it - otherwise it's left as a
// literal, unexpanded "${CSP_...}" placeholder in production.
const templateVars = [...new Set(template.match(/\$\{CSP_[A-Z_]+\}/g) ?? [])];

const envsubstMatch = entrypoint.match(
	/envsubst\s+'([^']*)'\s*\\\s*\n\s*<\s*\/etc\/nginx\/nginx\.conf\.template/,
);
if (!envsubstMatch) {
	fail(
		"Could not find the envsubst invocation rendering nginx.conf.template in 99-runtime-config.sh.",
	);
} else {
	const envsubstVars = new Set(envsubstMatch[1].split(/\s+/).filter(Boolean));
	for (const templateVar of templateVars) {
		if (!envsubstVars.has(templateVar)) {
			fail(
				`nginx.conf.template references ${templateVar} but 99-runtime-config.sh's envsubst call ` +
					"does not include it in its variable list, so it will be left unexpanded in production.",
			);
		}
	}
}

// 4. STORAGE_PUBLIC_URL (the source env var for CSP_STORAGE_ORIGIN) needs a
// documented default, matching the existing VITE_API_URL/
// VITE_KEYCLOAK_AUTHORITY_URL fallbacks, so the container doesn't emit an
// empty img-src origin when the var isn't set.
if (!/:\s*"\$\{STORAGE_PUBLIC_URL:=[^}]+\}"/.test(entrypoint)) {
	fail(
		"99-runtime-config.sh is missing a default fallback for STORAGE_PUBLIC_URL (expected a `: \"${STORAGE_PUBLIC_URL:=...}\"` line).",
	);
}

if (!/CSP_STORAGE_ORIGIN="\$\(url_origin "\$STORAGE_PUBLIC_URL"\)"/.test(entrypoint)) {
	fail(
		"99-runtime-config.sh does not derive CSP_STORAGE_ORIGIN from STORAGE_PUBLIC_URL via url_origin().",
	);
}

if (!/export\s+.*\bCSP_STORAGE_ORIGIN\b/.test(entrypoint)) {
	fail("99-runtime-config.sh computes CSP_STORAGE_ORIGIN but never exports it.");
}

if (ok) {
	console.log(
		"nginx CSP header is consolidated, includes the storage origin, and all referenced vars are substituted.",
	);
} else {
	process.exit(1);
}
