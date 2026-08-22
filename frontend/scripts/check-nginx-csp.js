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
	if (!imgSrcMatch || !imgSrcMatch[1].split(" ").includes("blob:")) {
		fail(
			"img-src directive is missing blob: - avatar/org-logo/opportunity-banner previews are rendered from " +
				"URL.createObjectURL() before upload and will be blocked by the browser without it.",
		);
	}

	// Regression for #2042: frame-src without 'self' blocks the hidden iframe
	// automaticSilentRenew/signinSilent() load for silent_redirect_uri
	// (src/main.tsx, same-origin - see src/silentRenew.ts), breaking silent SSO
	// and automatic token renewal in the released image while looking fine locally
	// (the dev server sends no CSP header at all).
	const frameSrcMatch = policy.match(/frame-src ([^;]+);/);
	if (!frameSrcMatch || !frameSrcMatch[1].split(" ").includes("'self'")) {
		fail(
			"frame-src directive is missing 'self' - the hidden iframe automaticSilentRenew/signinSilent() " +
				"loads for silent_redirect_uri is same-origin and will be blocked by the browser without it (#2042).",
		);
	}
	if (!frameSrcMatch || !frameSrcMatch[1].includes("${CSP_KEYCLOAK_ORIGIN}")) {
		fail(
			"frame-src directive is missing ${CSP_KEYCLOAK_ORIGIN} - Keycloak's own iframes (e.g. its check-session " +
				"iframe) will be blocked by the browser without it.",
		);
	}
}

// 2. Every add_header Content-Security-Policy line must reference a shared
// $csp_header* variable - none may hardcode the policy inline, which is
// exactly how the four copies drifted out of sync before. $csp_header_silent_renew
// (#2042) is a second, deliberate exception to "exactly one policy" - see
// check 5 below - so both are allowed here, just not a third ad hoc one.
const allowedCspHeaderLines = new Set([
	"add_header Content-Security-Policy $csp_header always;",
	"add_header Content-Security-Policy $csp_header_silent_renew always;",
]);
const cspHeaderLines = template
	.split("\n")
	.map((line) => line.trim())
	.filter((line) => /^add_header\s+Content-Security-Policy\s+/.test(line));

if (cspHeaderLines.length === 0) {
	fail("No `add_header Content-Security-Policy` lines found in nginx.conf.template.");
}

for (const line of cspHeaderLines) {
	if (!allowedCspHeaderLines.has(line)) {
		fail(
			`Found a Content-Security-Policy header not referencing a shared $csp_header* variable: "${line}". ` +
				"Every location must emit the header via $csp_header or $csp_header_silent_renew, not a hardcoded policy string.",
		);
	}
}

// 3. Every ${CSP_*} variable referenced in the template must actually be
// passed to the envsubst call that renders it - otherwise it's left as a
// literal, unexpanded "${CSP_...}" placeholder in the running container.
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
					"does not include it in its variable list, so it will be left unexpanded at container start.",
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

// 5. silent-renew.html (#2042) needs frame-ancestors 'self' (and a matching
// X-Frame-Options: SAMEORIGIN) to actually render inside the hidden iframe
// automaticSilentRenew/signinSilent() loads it in - frame-src 'self' on the
// *parent* (checked in 1 above) only governs what the parent may embed;
// whether the embed is actually allowed to render is a separate check
// against frame-ancestors/X-Frame-Options on silent-renew.html's *own*
// response, so both are required and neither is sufficient alone.
const silentRenewMapMatch = template.match(
	/map\s+\$host\s+\$csp_header_silent_renew\s*\{\s*default\s+"([^"]+)";\s*\}/,
);
if (!silentRenewMapMatch) {
	fail(
		'Could not find a `map $host $csp_header_silent_renew { default "..."; }` block in nginx.conf.template - ' +
			"silent-renew.html (#2042) needs its own policy with frame-ancestors 'self', distinct from every " +
			"other page's frame-ancestors 'none'.",
	);
} else if (!silentRenewMapMatch[1].includes("frame-ancestors 'self'")) {
	fail(
		"$csp_header_silent_renew's policy does not include frame-ancestors 'self' - without it, the browser " +
			"still refuses to render silent-renew.html inside the hidden iframe regardless of frame-src (#2042).",
	);
}

const silentRenewLocationMatch = template.match(
	/location\s*=\s*\/silent-renew\.html\s*\{([\s\S]*?)\n\t\}/,
);
if (!silentRenewLocationMatch) {
	fail("Could not find a `location = /silent-renew.html { ... }` block in nginx.conf.template.");
} else {
	const locationBlock = silentRenewLocationMatch[1];
	if (!/add_header X-Frame-Options "SAMEORIGIN" always;/.test(locationBlock)) {
		fail(
			"The /silent-renew.html location does not set X-Frame-Options: SAMEORIGIN - the site-wide DENY " +
				"(used everywhere else) independently blocks the hidden iframe this page exists for, regardless " +
				"of frame-ancestors/frame-src (#2042).",
		);
	}
	if (!/add_header Content-Security-Policy \$csp_header_silent_renew always;/.test(locationBlock)) {
		fail(
			"The /silent-renew.html location does not emit Content-Security-Policy via $csp_header_silent_renew.",
		);
	}
}

if (ok) {
	console.log(
		"nginx CSP header is consolidated, includes the storage origin, and all referenced vars are substituted.",
	);
} else {
	process.exit(1);
}
