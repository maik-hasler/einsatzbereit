#!/usr/bin/env node

import { readFileSync, existsSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(__dirname, "..");
const publicDir = join(frontendDir, "public");

const viteConfig = readFileSync(join(frontendDir, "vite.config.ts"), "utf8");
const appTsx = readFileSync(join(frontendDir, "src/App.tsx"), "utf8");

let ok = true;
function fail(message) {
	console.error(message);
	ok = false;
}

const MIN_DIMENSION = 320;
const MAX_DIMENSION = 3840;
const MAX_ASPECT_RATIO = 2.3;

function pngDimensions(file) {
	const buf = readFileSync(file);
	if (buf.length < 24 || buf.readUInt32BE(0) !== 0x89504e47) return null;
	if (buf.toString("ascii", 12, 16) !== "IHDR") return null;
	return { width: buf.readUInt32BE(16), height: buf.readUInt32BE(20) };
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

function field(objectText, name) {
	const match = objectText.match(new RegExp(`\\b${name}:\\s*"([^"]*)"`));
	return match ? match[1] : null;
}

// One manifest const per supported i18next language - keep in sync with
// frontend/src/i18n.ts's supportedLngs and the manifestFilename/
// emitLocaleManifest calls in vite.config.ts.
const LOCALES = [
	{ lang: "de", varName: "deManifest" },
	{ lang: "en", varName: "enManifest" },
];

for (const { lang, varName } of LOCALES) {
	const varIndex = viteConfig.indexOf(`const ${varName} =`);
	const manifest =
		varIndex === -1 ? null : sliceBalanced(viteConfig, varIndex, "{", "}");

	if (!manifest) {
		fail(`Could not find "const ${varName} = { ... }" in vite.config.ts.`);
		continue;
	}

	if (!/\bid:\s*"\/"/.test(manifest)) {
		fail(
			`${varName} in vite.config.ts has no \`id: "/"\` - without an explicit id the installed ` +
				"app's identity is derived from start_url, so a later start_url change would install a " +
				"second copy alongside the existing one instead of updating it (#1799).",
		);
	}

	if (!new RegExp(`\\blang:\\s*"${lang}"`).test(manifest)) {
		fail(
			`${varName} in vite.config.ts has no \`lang: "${lang}"\` - each locale manifest must ` +
				"declare its own language (#1923), matching the manifest.<locale>.webmanifest file it " +
				"is served as.",
		);
	}

	const screenshotsIndex = manifest.indexOf("screenshots:");
	const screenshotsBlock =
		screenshotsIndex === -1
			? null
			: sliceBalanced(manifest, screenshotsIndex, "[", "]");

	if (!screenshotsBlock) {
		fail(
			`${varName} in vite.config.ts has no \`screenshots\` - without them Chrome on Android ` +
				"shows the minimal install prompt (name + icon) instead of a real listing (#1799).",
		);
	} else {
		const screenshots = topLevelObjects(screenshotsBlock);
		if (screenshots.length === 0) {
			fail(`${varName}'s \`screenshots\` array in vite.config.ts is empty.`);
		}

		const aspectByFormFactor = new Map();

		for (const entry of screenshots) {
			const src = field(entry, "src");
			const sizes = field(entry, "sizes");
			const type = field(entry, "type");
			const formFactor = field(entry, "form_factor");
			const label = field(entry, "label");

			if (!src || !sizes || !type || !formFactor) {
				fail(
					`A ${varName} screenshot entry in vite.config.ts is missing src/sizes/type/form_factor: ${entry.replace(/\s+/g, " ")}`,
				);
				continue;
			}

			if (!src.startsWith("/screenshots/")) {
				fail(
					`${varName} screenshot "${src}" is not under /screenshots/ - keep screenshots there so ` +
						"workbox.globPatterns (which sweeps icons/*.png) doesn't precache install-prompt " +
						"artwork into every visitor's service worker cache.",
				);
			}

			if (!label) {
				fail(
					`${varName} screenshot "${src}" has no \`label\` - it is the accessible description shown ` +
						"with the screenshot in the install prompt.",
				);
			}

			const file = join(publicDir, src.replace(/^\//, ""));
			if (!existsSync(file)) {
				fail(
					`${varName} screenshot "${src}" does not exist at public${src} - the install prompt would ` +
						"drop the whole listing over one missing file.",
				);
				continue;
			}

			if (type !== "image/png" || !src.endsWith(".png")) {
				fail(
					`${varName} screenshot "${src}" is declared as "${type}" - this check reads PNG headers to ` +
						"verify dimensions, so screenshots must be PNGs (or this script needs extending).",
				);
				continue;
			}

			const actual = pngDimensions(file);
			if (!actual) {
				fail(`${varName} screenshot "${src}" is not a readable PNG.`);
				continue;
			}

			const declared = `${actual.width}x${actual.height}`;
			if (sizes !== declared) {
				fail(
					`${varName} screenshot "${src}" declares sizes "${sizes}" but the file on disk is ` +
						`${declared} - Chrome drops a screenshot whose declared size doesn't match.`,
				);
			}

			const shorter = Math.min(actual.width, actual.height);
			const longer = Math.max(actual.width, actual.height);
			if (shorter < MIN_DIMENSION || longer > MAX_DIMENSION) {
				fail(
					`${varName} screenshot "${src}" is ${declared} - every dimension must be between ` +
						`${MIN_DIMENSION} and ${MAX_DIMENSION} px for the richer install UI.`,
				);
			}
			if (longer / shorter > MAX_ASPECT_RATIO) {
				fail(
					`${varName} screenshot "${src}" is ${declared}, an aspect ratio of ` +
						`${(longer / shorter).toFixed(2)}:1 - the longer side may be at most ` +
						`${MAX_ASPECT_RATIO}x the shorter one.`,
				);
			}

			const aspect = actual.width / actual.height;
			const seen = aspectByFormFactor.get(formFactor);
			if (seen === undefined) {
				aspectByFormFactor.set(formFactor, { aspect, src });
			} else if (Math.abs(seen.aspect - aspect) > 0.01) {
				fail(
					`${varName} screenshot "${src}" (${declared}) has a different aspect ratio than ` +
						`"${seen.src}", which is also form_factor "${formFactor}" - all screenshots of one ` +
						"form factor must share one aspect ratio.",
				);
			}
		}

		if (!aspectByFormFactor.has("narrow")) {
			fail(
				`No ${varName} screenshot has form_factor "narrow" - Chrome on Android requires at least ` +
					"one to show the richer install UI at all.",
			);
		}
		if (!aspectByFormFactor.has("wide")) {
			fail(
				`No ${varName} screenshot has form_factor "wide" - desktop install prompts only show wide ` +
					"screenshots.",
			);
		}
	}

	const shortcutsIndex = manifest.indexOf("shortcuts:");
	const shortcutsBlock =
		shortcutsIndex === -1
			? null
			: sliceBalanced(manifest, shortcutsIndex, "[", "]");

	if (!shortcutsBlock) {
		fail(
			`${varName} in vite.config.ts has no \`shortcuts\` - the installed app's long-press menu ` +
				"then offers nothing beyond opening the app (#1799).",
		);
		continue;
	}

	const shortcuts = topLevelObjects(shortcutsBlock);
	if (shortcuts.length === 0) {
		fail(`${varName}'s \`shortcuts\` array in vite.config.ts is empty.`);
	}

	for (const entry of shortcuts) {
		const name = field(entry, "name");
		const url = field(entry, "url");

		if (!name || !url) {
			fail(
				`A ${varName} shortcut entry in vite.config.ts is missing name/url: ${entry.replace(/\s+/g, " ")}`,
			);
			continue;
		}

		if (!appTsx.includes(`path="${url}"`)) {
			fail(
				`${varName} shortcut "${name}" points at "${url}", which is not a route declared in ` +
					"src/App.tsx - an installed app's shortcut would open NotFoundPage. Update the " +
					"shortcut URL alongside the route rename.",
			);
		}

		const iconsIndex = entry.indexOf("icons:");
		const iconsBlock =
			iconsIndex === -1 ? null : sliceBalanced(entry, iconsIndex, "[", "]");
		if (!iconsBlock) {
			fail(
				`${varName} shortcut "${name}" has no icons - Android's long-press menu falls back to a ` +
					"generic entry without one.",
			);
			continue;
		}

		for (const icon of topLevelObjects(iconsBlock)) {
			const src = field(icon, "src");
			const sizes = field(icon, "sizes");
			if (!src || !sizes) {
				fail(`An icon of ${varName} shortcut "${name}" is missing src/sizes.`);
				continue;
			}
			const file = join(publicDir, src.replace(/^\//, ""));
			if (!existsSync(file)) {
				fail(
					`Icon "${src}" of ${varName} shortcut "${name}" does not exist at public${src}.`,
				);
				continue;
			}
			const actual = pngDimensions(file);
			if (!actual) {
				fail(`Icon "${src}" of ${varName} shortcut "${name}" is not a readable PNG.`);
				continue;
			}
			const declared = `${actual.width}x${actual.height}`;
			if (sizes !== declared) {
				fail(
					`Icon "${src}" of ${varName} shortcut "${name}" declares sizes "${sizes}" but the file ` +
						`on disk is ${declared}.`,
				);
			}
		}
	}
}

if (!/manifestFilename:\s*"manifest\.de\.webmanifest"/.test(viteConfig)) {
	fail(
		'vite.config.ts does not set manifestFilename: "manifest.de.webmanifest" on the VitePWA ' +
			"plugin - without it, VitePWA falls back to its default manifest.webmanifest filename, " +
			"which the nginx location block and CI's live-served checks (frontend-checks.yml) no " +
			"longer expect (#1923).",
	);
}
if (!/manifest\.en\.webmanifest/.test(viteConfig.replace(/manifestFilename:\s*"manifest\.de\.webmanifest"/, ""))) {
	fail(
		'vite.config.ts never references "manifest.en.webmanifest" outside of manifestFilename - ' +
			"enManifest is declared but nothing writes it into the build output (#1923).",
	);
}

if (ok) {
	console.log(
		"Both locale web app manifests (de/en) declare id, lang, screenshots and shortcuts; every " +
			"referenced asset exists, matches its declared size and satisfies the install-prompt " +
			"constraints.",
	);
} else {
	process.exit(1);
}
