#!/usr/bin/env node

/**
 * Graph-level rules ESLint cannot express.
 *
 * The per-layer import direction is enforced in eslint.config.js, where a
 * violation shows up in the editor as you type. What is left over needs the
 * whole graph at once: a cycle is a property of a path, not of a single
 * import, and an orphan is defined by the absence of any edge into it.
 *
 * Measured when this was added: 0 cycles across 379 modules. The rule is a
 * ratchet on that, not a cleanup.
 *
 * This was `dependency-cruiser` until it turned out it cannot run here at all:
 * it follows the node.js release cycle (`^22||^24||>=26`) and this repository
 * pins `engines.node` to whatever is current, which every workflow reads via
 * `node-version-file`. On an odd-numbered line - 25.9.0 at the time of writing
 * - the tool refuses to start, and Renovate will land on such a line again.
 * The graph it walked was only ever this project's own relative imports, and
 * TypeScript, which is already here and has no such constraint, parses those
 * exactly - including which edges are type-only, which is the one distinction
 * the cycle rule turns on.
 */

import { readFileSync, readdirSync, statSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname, resolve, relative } from "path";
import ts from "typescript";

const frontendDir = join(dirname(fileURLToPath(import.meta.url)), "..");
const srcDir = join(frontendDir, "src");

const EXTENSIONS = [".ts", ".tsx"];

/**
 * Real files with no importer by design. Everything else with no edge into it
 * is either dead or was never wired up.
 */
const ORPHAN_ALLOWLIST = [
	// Vite/Vitest entry points and ambient declarations.
	/^src\/main\.tsx$/,
	/^src\/silentRenew\.ts$/,
	/^src\/test\/setup\.ts$/,
	// Generated: the entry points and barrels the generator emits are not all
	// reachable from application code, and none of it is ours to prune.
	/^src\/client\/generated\//,
	// Vitest discovers these; nothing imports them, by definition.
	/\.test\.(ts|tsx)$/,
	// Resolved through vitest.config.ts's alias for "virtual:pwa-register/react",
	// which is not an import edge.
	/^src\/test\/pwaRegisterStub\.ts$/,
	/\.d\.ts$/,
];

/**
 * The generated client's own back-edges. The generator's `runtimeConfigPath`
 * wiring has client.gen.ts import our config factory while api-instance.ts
 * imports its type back, and types.gen/utils.gen reference each other. Both
 * return edges are type-only and therefore erased before anything runs, but
 * the cycle rule below only skips an edge it can see is type-only - a cycle
 * starting inside generated code is not one we could act on either way.
 */
const CYCLE_START_EXCLUDED = /^src\/client\/generated\//;

function listSourceFiles(dir) {
	let files = [];
	for (const entry of readdirSync(dir)) {
		const full = join(dir, entry);
		if (statSync(full).isDirectory()) {
			files = files.concat(listSourceFiles(full));
		} else if (EXTENSIONS.some((extension) => entry.endsWith(extension))) {
			files.push(full);
		}
	}
	return files;
}

/**
 * `./foo` -> `src/dir/foo.tsx`, or `src/dir/foo/index.ts`.
 *
 * Vite's query suffixes (`?raw`, `?react`, `?inline`) select a transform, not
 * a different file, so they come off before the path is resolved.
 */
function resolveRelative(specifier, fromFile) {
	const withoutQuery = specifier.split("?")[0];
	const base = resolve(dirname(fromFile), withoutQuery);
	const candidates = [
		base,
		...EXTENSIONS.map((extension) => base + extension),
		...EXTENSIONS.map((extension) => join(base, "index" + extension)),
	];
	for (const candidate of candidates) {
		try {
			if (statSync(candidate).isFile()) return candidate;
		} catch {
			// Not a file - try the next shape.
		}
	}
	return null;
}

/**
 * Every relative import in a file, with the one bit the cycle rule needs: is
 * the whole edge type-only? `import type { X }` and `import { type X }` both
 * disappear at compile time; a single value specifier makes the edge real.
 *
 * Dynamic `import()` counts too, and has to: every route in App.tsx reaches
 * its page that way, so a walk of top-level statements alone reports the whole
 * lazy-loaded half of the app as orphaned.
 */
function readEdges(file) {
	const source = ts.createSourceFile(
		file,
		readFileSync(file, "utf8"),
		ts.ScriptTarget.Latest,
		/* setParentNodes */ false,
		file.endsWith(".tsx") ? ts.ScriptKind.TSX : ts.ScriptKind.TS,
	);

	const edges = [];

	function add(specifierNode, typeOnly) {
		if (!specifierNode || !ts.isStringLiteral(specifierNode)) return;
		if (!specifierNode.text.startsWith(".")) return;
		edges.push({ specifier: specifierNode.text, typeOnly });
	}

	function visit(node) {
		if (ts.isImportDeclaration(node)) {
			add(node.moduleSpecifier, isTypeOnlyImport(node));
		} else if (ts.isExportDeclaration(node) && node.moduleSpecifier) {
			add(node.moduleSpecifier, isTypeOnlyExport(node));
		} else if (
			ts.isCallExpression(node) &&
			node.expression.kind === ts.SyntaxKind.ImportKeyword
		) {
			// A dynamic import is always a value edge - there is nothing to erase.
			add(node.arguments[0], false);
		}
		ts.forEachChild(node, visit);
	}

	ts.forEachChild(source, visit);
	return edges;
}

/** `import "./x"` is a value edge; `import { type A, type B }` is not. */
function isTypeOnlyImport(node) {
	const clause = node.importClause;
	if (!clause) return false;
	if (clause.isTypeOnly) return true;
	if (clause.name) return false; // default import
	const bindings = clause.namedBindings;
	if (!bindings || !ts.isNamedImports(bindings)) return false; // `* as ns`
	return (
		bindings.elements.length > 0 &&
		bindings.elements.every((element) => element.isTypeOnly)
	);
}

/** Same question for `export ... from`; `export * from` always carries values. */
function isTypeOnlyExport(node) {
	if (node.isTypeOnly) return true;
	const clause = node.exportClause;
	if (!clause || !ts.isNamedExports(clause)) return false;
	return (
		clause.elements.length > 0 &&
		clause.elements.every((element) => element.isTypeOnly)
	);
}

const files = listSourceFiles(srcDir);
const rel = (file) => relative(frontendDir, file).split("\\").join("/");

/** file -> [{ to, typeOnly }] */
const graph = new Map();
const importedByAnyone = new Set();
const unresolved = [];

for (const file of files) {
	const edges = [];
	for (const edge of readEdges(file)) {
		const target = resolveRelative(edge.specifier, file);
		if (!target) {
			unresolved.push({ file: rel(file), specifier: edge.specifier });
			continue;
		}
		edges.push({ to: target, typeOnly: edge.typeOnly });
		importedByAnyone.add(target);
	}
	graph.set(file, edges);
}

let ok = true;
function fail(message) {
	console.error(message);
	ok = false;
}

for (const { file, specifier } of unresolved) {
	fail(
		`${file}: the relative import "${specifier}" does not resolve to a file. ` +
			"Left unchecked it survives type-checking (a .d.ts may still declare it) " +
			"and fails at bundle time.",
	);
}

// Cycles, over value edges only. A cycle that exists solely in the type graph
// is erased before anything runs and cannot deadlock module initialisation.
const WHITE = 0;
const GREY = 1;
const BLACK = 2;
const colour = new Map(files.map((file) => [file, WHITE]));
const reported = new Set();

function walk(file, stack) {
	colour.set(file, GREY);
	stack.push(file);

	for (const edge of graph.get(file) ?? []) {
		if (edge.typeOnly) continue;
		const state = colour.get(edge.to);
		if (state === GREY) {
			const cycle = stack.slice(stack.indexOf(edge.to)).map(rel);
			const key = [...cycle].sort().join(" -> ");
			if (!reported.has(key)) {
				reported.add(key);
				fail(
					`Import cycle: ${[...cycle, cycle[0]].join(" -> ")}\n` +
						"  A cycle means neither module can be read, tested or extracted " +
						"without the other, and it makes module initialisation order " +
						"load-bearing. Move the shared piece into a third module both can import.",
				);
			}
		} else if (state === WHITE) {
			walk(edge.to, stack);
		}
	}

	stack.pop();
	colour.set(file, BLACK);
}

for (const file of files) {
	if (colour.get(file) === WHITE && !CYCLE_START_EXCLUDED.test(rel(file))) {
		walk(file, []);
	}
}

for (const file of files) {
	const path = rel(file);
	if (importedByAnyone.has(file)) continue;
	if (ORPHAN_ALLOWLIST.some((pattern) => pattern.test(path))) continue;
	fail(
		`${path} is an orphan: nothing imports it and it is not an entry point, ` +
			"so it is either dead or someone forgot to wire it up. Delete it, or " +
			"point something at it.",
	);
}

if (ok) {
	console.log(
		`No import cycles and no orphans across ${files.length} modules in src/.`,
	);
} else {
	process.exit(1);
}
