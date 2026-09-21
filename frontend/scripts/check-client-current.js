#!/usr/bin/env node

// `src/client/generated/` is committed, and generated from
// `backend/src/Api/wwwroot/openapi-v1.json`, which is itself committed and
// regenerated on every backend build. Two committed artefacts in a producer/
// consumer relationship drift the moment someone changes an endpoint and does
// not re-run the generator - and the failure that follows is a type error in a
// PR that has nothing to do with the endpoint.
//
// This regenerates into a scratch directory and compares. It does not write
// into src/, so a red CI run tells a contributor exactly one thing: run
// `pnpm client:generate` and commit the result.

import { execFileSync } from "child_process";
import { normalize } from "./normalize-openapi.js";
import { readdirSync, readFileSync, rmSync, statSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname, relative } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(__dirname, "..");
const committedDir = join(frontendDir, "src/client/generated");

// Beside the committed output, not in a temp directory: the generated
// client.gen.ts imports `../api-instance.ts` by a path relative to its own
// folder, so a scratch directory at a different depth produces a file that
// differs for a reason that has nothing to do with the API. Same depth, same
// output. Ignored by git (.gitignore) and removed in the `finally` below.
const scratchDir = join(frontendDir, "src/client/.generated-check");
rmSync(scratchDir, { recursive: true, force: true });

function listFiles(dir) {
	let files = [];
	for (const entry of readdirSync(dir)) {
		const full = join(dir, entry);
		files = statSync(full).isDirectory()
			? files.concat(listFiles(full))
			: files.concat(full);
	}
	return files.sort();
}

try {
	// Same two steps `pnpm client:generate` runs, in the same order.
	normalize();

	execFileSync(
		"node",
		[
			join(frontendDir, "node_modules/@hey-api/openapi-ts/bin/run.js"),
			"--output",
			scratchDir,
		],
		{ cwd: frontendDir, stdio: "pipe" },
	);

	const committed = listFiles(committedDir).map((f) =>
		relative(committedDir, f),
	);
	const fresh = listFiles(scratchDir).map((f) => relative(scratchDir, f));

	const missing = fresh.filter((f) => !committed.includes(f));
	const extra = committed.filter((f) => !fresh.includes(f));
	const changed = fresh
		.filter((f) => committed.includes(f))
		.filter(
			(f) =>
				readFileSync(join(committedDir, f), "utf8") !==
				readFileSync(join(scratchDir, f), "utf8"),
		);

	if (missing.length || extra.length || changed.length) {
		for (const f of missing)
			console.error(`Missing from src/client/generated/: ${f}`);
		for (const f of extra)
			console.error(`No longer generated, still committed: ${f}`);
		for (const f of changed)
			console.error(`Out of date in src/client/generated/: ${f}`);
		console.error(
			"\nThe committed API client no longer matches backend/src/Api/wwwroot/openapi-v1.json. " +
				"Run `pnpm client:generate` in frontend/ and commit the result. If the OpenAPI document " +
				"itself is what is stale, rebuild the backend first " +
				"(`dotnet build backend/src/Api/Api.csproj`), which regenerates it.",
		);
		process.exit(1);
	}

	console.log(
		"src/client/generated/ matches the committed OpenAPI document.",
	);
} finally {
	rmSync(scratchDir, { recursive: true, force: true });
}
