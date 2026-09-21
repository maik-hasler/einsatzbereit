#!/usr/bin/env node

// AGENTS.md is the first thing a contributor - and every coding agent working
// in this repo - reads about the frontend. A stale entry there does not just
// mislead: it produces code written against a file that no longer exists, or
// against a shape that moved. The i18n and nginx checks next to this one guard
// the same class of drift for translations and headers; this one guards the
// prose.
//
// What it enforces is referential integrity, deliberately not completeness:
//
//   1. Every source path AGENTS.md names resolves to a real file or folder.
//   2. Every top-level folder under src/ appears in the architecture tree.
//
// It does NOT demand that every component be documented. The design-system
// table is a curated list of the primitives a contributor must reach for
// first; forcing all 80-odd components into it would turn a decision aid into
// an unread inventory, and the next contributor would stop reading it.

import { existsSync, readFileSync, readdirSync, statSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(__dirname, "..");
const srcDir = join(frontendDir, "src");

const agentsMd = readFileSync(join(frontendDir, "AGENTS.md"), "utf8");

let ok = true;
function fail(message) {
	console.error(message);
	ok = false;
}

// ---------------------------------------------------------------------------
// 1. Every referenced source path exists.
// ---------------------------------------------------------------------------

// Paths are written in backticks throughout the file, either rooted
// ("src/components/Button.tsx", "lib/formClasses.ts") or as a bare file name
// inside the architecture tree ("Footer.tsx"). Only the rooted form is
// unambiguous enough to resolve, so that is what this checks; bare names are
// covered by the tree walk in section 2.
const REFERENCE_PATTERN =
	/`((?:src\/|components\/|pages\/|hooks\/|lib\/|layouts\/|contexts\/|client\/|test\/|styles\/)[A-Za-z0-9_./-]+)`/g;

// Referenced from the frontend but owned by another part of the monorepo.
const EXTERNAL_PREFIXES = ["backend/", "docs/", "keycloak/", ".github/"];

const missing = new Set();
for (const [, reference] of agentsMd.matchAll(REFERENCE_PATTERN)) {
	if (EXTERNAL_PREFIXES.some((prefix) => reference.startsWith(prefix))) continue;

	// A trailing slash marks a folder; everything else is a file. A reference
	// with neither an extension nor a slash is a symbol name, not a path.
	const isFolder = reference.endsWith("/");
	const looksLikePath = isFolder || /\.[a-z]+$/.test(reference);
	if (!looksLikePath) continue;

	const relative = reference.startsWith("src/") ? reference : `src/${reference}`;
	const absolute = join(frontendDir, relative);

	if (!existsSync(absolute)) {
		missing.add(reference);
		continue;
	}
	if (isFolder && !statSync(absolute).isDirectory()) {
		fail(
			`AGENTS.md documents \`${reference}\` as a folder, but ${relative} is a file.`,
		);
	}
	if (!isFolder && statSync(absolute).isDirectory()) {
		fail(
			`AGENTS.md documents \`${reference}\` as a file, but ${relative} is a folder - ` +
				"the trailing slash is what tells a reader (and an agent) to look inside for the " +
				"orchestrator rather than open a single file.",
		);
	}
}

for (const reference of [...missing].sort()) {
	fail(
		`AGENTS.md references \`${reference}\`, which does not exist. Update the entry, or delete ` +
			"it if the file is gone - a documented path that resolves to nothing is what sends the " +
			"next contributor (or agent) writing against a file that was renamed or removed.",
	);
}

// ---------------------------------------------------------------------------
// 2. Every name inside the architecture tree exists.
// ---------------------------------------------------------------------------

const treeMatch = agentsMd.match(/```\nsrc\/\n([\s\S]*?)```/);
if (!treeMatch) {
	fail(
		"Could not find the ```-fenced `src/` architecture tree in AGENTS.md - it is the map every " +
			"contributor reads first, and this check has nothing to verify without it.",
	);
} else {
	const treeBody = treeMatch[1];

	// Tree entries look like "├── components/" or "│   ├── Footer.tsx  Footer
	// with ...", and a long description may wrap onto continuation lines that
	// carry no "├──". Fold those back into the entry they belong to: the prose
	// matters as much as the name, because the Header, contexts/ and
	// VolunteerOpportunitiesList entries list their own contents there, and
	// those lists are exactly what goes stale when a file is renamed.
	const entries = [];
	for (const line of treeBody.split("\n")) {
		const match = /^([\s│]*)(?:├──|└──)\s+(\S+)(.*)$/.exec(line);
		if (match) {
			entries.push({
				depth: Math.round(match[1].length / 4),
				name: match[2],
				description: match[3],
			});
		} else if (entries.length && line.trim()) {
			entries[entries.length - 1].description += ` ${line.trim()}`;
		}
	}

	const FILE_TOKEN_PATTERN =
		/\b((?:[A-Za-z0-9_-]+\/)*[A-Za-z][A-Za-z0-9_-]*\.(?:tsx|ts|css|json))\b/g;

	const parents = [];
	const documentedTopLevel = new Set();

	for (const { depth, name, description } of entries) {
		parents.length = depth;

		// "styles/global.css" and "app/OrgDashboardPage/" are written as one
		// entry rather than nested, so join and let the path speak for itself.
		const relative = join("src", ...parents, name);
		if (depth === 0) documentedTopLevel.add(name.replace(/\/.*$/, ""));

		const isFolder = name.endsWith("/");
		if (isFolder) parents[depth] = name;

		if (!existsSync(join(frontendDir, relative))) {
			fail(
				`AGENTS.md's architecture tree lists ${relative}, which does not exist.`,
			);
			continue;
		}

		// A folder entry's prose describes what is inside it; a file entry's
		// prose points at siblings and at other folders.
		const base = isFolder ? relative : dirname(relative);
		for (const [, token] of description.matchAll(FILE_TOKEN_PATTERN)) {
			const target = token.includes("/")
				? join("src", token)
				: join(base, token);
			if (!existsSync(join(frontendDir, target))) {
				fail(
					`AGENTS.md's architecture tree entry for ${relative} names ${token}, which does ` +
						`not resolve (looked for ${target}). An entry that lists its own contents has to ` +
						"be corrected when one of them is renamed or removed.",
				);
			}
		}
	}

	const actualTopLevel = readdirSync(srcDir)
		.filter((entry) => statSync(join(srcDir, entry)).isDirectory())
		// Generated or self-evident folders that would only pad the map.
		.filter((entry) => !["assets", "locales", "test"].includes(entry));

	for (const folder of actualTopLevel) {
		if (!documentedTopLevel.has(folder)) {
			fail(
				`src/${folder}/ is missing from AGENTS.md's architecture tree. Every folder a ` +
					"contributor can put a file in belongs on the map, or the map quietly stops being one.",
			);
		}
	}
}

if (ok) {
	console.log(
		"AGENTS.md's documented paths all resolve and its architecture tree covers every src/ folder.",
	);
} else {
	process.exit(1);
}
