#!/usr/bin/env node
// Guards the web app manifest declared in vite.config.ts (issue #1799). The
// manifest used to be the bare minimum - name, description, three icons -
// with no `id`, no `screenshots` and no `shortcuts`, so Chrome on Android
// fell back to its plain install prompt (name + icon only) instead of the
// "richer install UI" listing, and the installed app's identity was derived
// from `start_url`, meaning a future entry-point move would have installed a
// second copy next to the first rather than updating it.
//
// Screenshots are the fragile half: Chrome silently drops the richer install
// UI - no console warning, no visible failure - if any single screenshot
// violates its constraints, and the failure mode is invisible from the repo
// (a `sizes` typo, a re-cropped PNG whose real dimensions no longer match
// what the manifest claims, a screenshot deleted from public/ while its
// manifest entry stays). Everything below is therefore checked against the
// bytes on disk, not just against what the config says. Purely static - no
// build required.
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

// Chrome's documented constraints for screenshots to qualify for the richer
// install UI. Violating any of them costs the listing silently.
const MIN_DIMENSION = 320;
const MAX_DIMENSION = 3840;
const MAX_ASPECT_RATIO = 2.3;

// Reads a PNG's real pixel dimensions straight out of the IHDR chunk (bytes
// 16-23, big-endian), so the `sizes` string in the manifest is checked
// against the actual image rather than trusted.
function pngDimensions(file) {
	const buf = readFileSync(file);
	if (buf.length < 24 || buf.readUInt32BE(0) !== 0x89504e47) return null;
	if (buf.toString("ascii", 12, 16) !== "IHDR") return null;
	return { width: buf.readUInt32BE(16), height: buf.readUInt32BE(20) };
}

// Returns the substring starting at `open` and ending at its matching
// closing delimiter, both included. Brace/bracket counting rather than a
// regex: the manifest nests objects inside arrays inside objects, which a
// flat regex can't follow without over-matching into the next block.
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

// The `{ ... }` entries at the top level of an array block, i.e. skipping
// objects nested inside them (a shortcut's own `icons: [{ ... }]`).
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

const manifestIndex = viteConfig.indexOf("manifest:");
const manifest =
	manifestIndex === -1
		? null
		: sliceBalanced(viteConfig, manifestIndex, "{", "}");

if (!manifest) {
	fail("Could not find the VitePWA `manifest: { ... }` block in vite.config.ts.");
	process.exit(1);
}

// 1. An explicit id, decoupled from start_url. Without it the browser
// derives the app identity from start_url, so moving the entry point later
// reads as a different app to every already-installed client.
if (!/\bid:\s*"\/"/.test(manifest)) {
	fail(
		'The manifest in vite.config.ts has no `id: "/"` - without an explicit id the installed ' +
			"app's identity is derived from start_url, so a later start_url change would install a " +
			"second copy alongside the existing one instead of updating it (#1799).",
	);
}

// 2. lang stays declared. The German-only manifest is a deliberate decision
// (see the comment on it in vite.config.ts and CONTRIBUTING.md's Language
// Convention) - dropping the field entirely would leave the language of the
// name/description undeclared rather than merely non-localised.
if (!/\blang:\s*"[a-z]{2}(-[A-Za-z0-9]+)*"/.test(manifest)) {
	fail(
		"The manifest in vite.config.ts has no `lang` - the name and description are written in " +
			"one specific language and need to say which (see CONTRIBUTING.md's Language Convention " +
			"for why that language is German).",
	);
}

// 3. Screenshots: present, on disk, and within every constraint Chrome
// enforces for the richer install UI.
const screenshotsIndex = manifest.indexOf("screenshots:");
const screenshotsBlock =
	screenshotsIndex === -1
		? null
		: sliceBalanced(manifest, screenshotsIndex, "[", "]");

if (!screenshotsBlock) {
	fail(
		"The manifest in vite.config.ts has no `screenshots` - without them Chrome on Android " +
			"shows the minimal install prompt (name + icon) instead of a real listing (#1799).",
	);
} else {
	const screenshots = topLevelObjects(screenshotsBlock);
	if (screenshots.length === 0) {
		fail("The manifest's `screenshots` array in vite.config.ts is empty.");
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
				`A manifest screenshot entry in vite.config.ts is missing src/sizes/type/form_factor: ${entry.replace(/\s+/g, " ")}`,
			);
			continue;
		}

		// Screenshots are install-time artwork the browser fetches once, on
		// demand - keeping them out of the directories workbox.globPatterns
		// sweeps ("icons/*.png") is what stops half a megabyte of them being
		// precached by the service worker for every visitor, installing or not.
		if (!src.startsWith("/screenshots/")) {
			fail(
				`Manifest screenshot "${src}" is not under /screenshots/ - keep screenshots there so ` +
					"workbox.globPatterns (which sweeps icons/*.png) doesn't precache install-prompt " +
					"artwork into every visitor's service worker cache.",
			);
		}

		if (!label) {
			fail(
				`Manifest screenshot "${src}" has no \`label\` - it is the accessible description shown ` +
					"with the screenshot in the install prompt.",
			);
		}

		const file = join(publicDir, src.replace(/^\//, ""));
		if (!existsSync(file)) {
			fail(
				`Manifest screenshot "${src}" does not exist at public${src} - the install prompt would ` +
					"drop the whole listing over one missing file.",
			);
			continue;
		}

		if (type !== "image/png" || !src.endsWith(".png")) {
			fail(
				`Manifest screenshot "${src}" is declared as "${type}" - this check reads PNG headers to ` +
					"verify dimensions, so screenshots must be PNGs (or this script needs extending).",
			);
			continue;
		}

		const actual = pngDimensions(file);
		if (!actual) {
			fail(`Manifest screenshot "${src}" is not a readable PNG.`);
			continue;
		}

		const declared = `${actual.width}x${actual.height}`;
		if (sizes !== declared) {
			fail(
				`Manifest screenshot "${src}" declares sizes "${sizes}" but the file on disk is ` +
					`${declared} - Chrome drops a screenshot whose declared size doesn't match.`,
			);
		}

		const shorter = Math.min(actual.width, actual.height);
		const longer = Math.max(actual.width, actual.height);
		if (shorter < MIN_DIMENSION || longer > MAX_DIMENSION) {
			fail(
				`Manifest screenshot "${src}" is ${declared} - every dimension must be between ` +
					`${MIN_DIMENSION} and ${MAX_DIMENSION} px for the richer install UI.`,
			);
		}
		if (longer / shorter > MAX_ASPECT_RATIO) {
			fail(
				`Manifest screenshot "${src}" is ${declared}, an aspect ratio of ` +
					`${(longer / shorter).toFixed(2)}:1 - the longer side may be at most ` +
					`${MAX_ASPECT_RATIO}x the shorter one.`,
			);
		}

		// All screenshots of one form factor must share a single aspect ratio,
		// or Chrome shows none of them.
		const aspect = actual.width / actual.height;
		const seen = aspectByFormFactor.get(formFactor);
		if (seen === undefined) {
			aspectByFormFactor.set(formFactor, { aspect, src });
		} else if (Math.abs(seen.aspect - aspect) > 0.01) {
			fail(
				`Manifest screenshot "${src}" (${declared}) has a different aspect ratio than ` +
					`"${seen.src}", which is also form_factor "${formFactor}" - all screenshots of one ` +
					"form factor must share one aspect ratio.",
			);
		}
	}

	// Chrome only qualifies for the richer install UI on Android when a narrow
	// (mobile) screenshot exists, and only shows wide ones on desktop.
	if (!aspectByFormFactor.has("narrow")) {
		fail(
			'No manifest screenshot has form_factor "narrow" - Chrome on Android requires at least ' +
				"one to show the richer install UI at all.",
		);
	}
	if (!aspectByFormFactor.has("wide")) {
		fail(
			'No manifest screenshot has form_factor "wide" - desktop install prompts only show wide ' +
				"screenshots.",
		);
	}
}

// 4. Shortcuts: present, and every one of them points at a route that
// actually exists. A renamed route would otherwise leave a long-press
// shortcut on installed devices landing on NotFoundPage, with nothing in CI
// noticing.
const shortcutsIndex = manifest.indexOf("shortcuts:");
const shortcutsBlock =
	shortcutsIndex === -1
		? null
		: sliceBalanced(manifest, shortcutsIndex, "[", "]");

if (!shortcutsBlock) {
	fail(
		"The manifest in vite.config.ts has no `shortcuts` - the installed app's long-press menu " +
			"then offers nothing beyond opening the app (#1799).",
	);
} else {
	const shortcuts = topLevelObjects(shortcutsBlock);
	if (shortcuts.length === 0) {
		fail("The manifest's `shortcuts` array in vite.config.ts is empty.");
	}

	for (const entry of shortcuts) {
		const name = field(entry, "name");
		const url = field(entry, "url");

		if (!name || !url) {
			fail(
				`A manifest shortcut entry in vite.config.ts is missing name/url: ${entry.replace(/\s+/g, " ")}`,
			);
			continue;
		}

		if (!appTsx.includes(`path="${url}"`)) {
			fail(
				`Manifest shortcut "${name}" points at "${url}", which is not a route declared in ` +
					"src/App.tsx - an installed app's shortcut would open NotFoundPage. Update the " +
					"shortcut URL alongside the route rename.",
			);
		}

		const iconsIndex = entry.indexOf("icons:");
		const iconsBlock =
			iconsIndex === -1 ? null : sliceBalanced(entry, iconsIndex, "[", "]");
		if (!iconsBlock) {
			fail(
				`Manifest shortcut "${name}" has no icons - Android's long-press menu falls back to a ` +
					"generic entry without one.",
			);
			continue;
		}

		for (const icon of topLevelObjects(iconsBlock)) {
			const src = field(icon, "src");
			const sizes = field(icon, "sizes");
			if (!src || !sizes) {
				fail(`An icon of manifest shortcut "${name}" is missing src/sizes.`);
				continue;
			}
			const file = join(publicDir, src.replace(/^\//, ""));
			if (!existsSync(file)) {
				fail(
					`Icon "${src}" of manifest shortcut "${name}" does not exist at public${src}.`,
				);
				continue;
			}
			const actual = pngDimensions(file);
			if (!actual) {
				fail(`Icon "${src}" of manifest shortcut "${name}" is not a readable PNG.`);
				continue;
			}
			const declared = `${actual.width}x${actual.height}`;
			if (sizes !== declared) {
				fail(
					`Icon "${src}" of manifest shortcut "${name}" declares sizes "${sizes}" but the file ` +
						`on disk is ${declared}.`,
				);
			}
		}
	}
}

if (ok) {
	console.log(
		"Web app manifest declares id, screenshots and shortcuts; every referenced asset exists, " +
			"matches its declared size and satisfies the install-prompt constraints.",
	);
} else {
	process.exit(1);
}
