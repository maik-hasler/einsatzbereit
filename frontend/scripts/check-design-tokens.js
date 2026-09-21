#!/usr/bin/env node

// See scripts/generate-design-tokens.js for why the generated mirror exists.
// This fails when it has drifted from the @theme block it is generated from.

import { readFileSync } from "fs";
import { join } from "path";
import {
	CSS_FILE,
	TS_FILE,
	frontendDir,
	readThemeTokens,
	renderModule,
} from "./generate-design-tokens.js";

const expected = await renderModule(readThemeTokens());
const actual = readFileSync(join(frontendDir, TS_FILE), "utf8");

if (expected !== actual) {
	console.error(
		`${TS_FILE} no longer matches the @theme block in ${CSS_FILE}. ` +
			"Run `pnpm tokens:generate` and commit the result - the CSS block is the source, " +
			"the TypeScript module is the mirror.",
	);
	process.exit(1);
}

console.log(`${TS_FILE} matches the @theme block in ${CSS_FILE}.`);
