#!/usr/bin/env node

// The OpenAPI document declares every numeric property twice.
//
//     "pageCount": {
//       "pattern": "^-?(?:0|[1-9]\\d*)$",
//       "type": ["integer", "string"],
//       "format": "int32"
//     }
//
// That is ASP.NET describing `JsonNumberHandling.AllowReadingFromString`,
// which `JsonSerializerDefaults.Web` turns on: the server will *accept* "42"
// on the way in. It never *writes* one - `System.Text.Json` serialises an
// `int` as a JSON number, always - so the union describes a tolerance on the
// request side and says nothing true about a response.
//
// Generated verbatim it produces `number | string` on 112 properties, and a
// consumer has two bad options: narrow it with a cast at every read (a claim
// about the wire format that nothing checks), or thread the union through
// every calculation. The previous generator quietly narrowed it and nobody
// ever hit the string case in the years since.
//
// So narrow it here, once, where the reason can be written down - and narrow
// only the exact shape ASP.NET emits, so a genuine union in the schema is
// still generated as one.
//
// Run as part of `pnpm client:generate`. The output is a build artefact.

import { mkdirSync, readFileSync, writeFileSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(__dirname, "..");

export const SOURCE = join(
	frontendDir,
	"../backend/src/Api/wwwroot/openapi-v1.json",
);
export const OUTPUT = join(frontendDir, ".openapi/openapi-v1.json");

const NUMERIC_TYPES = new Set(["integer", "number"]);
const NUMERIC_FORMATS = new Set([
	"int32",
	"int64",
	"double",
	"float",
	"decimal",
]);

let narrowed = 0;

/**
 * True for exactly the shape ASP.NET emits: `["integer"|"number", "string"]`,
 * plus `"null"` when the property is nullable. Nullability is real and is
 * preserved; only the string alternative goes.
 */
function isNumberOrStringUnion(schema) {
	if (!Array.isArray(schema.type)) return false;
	const withoutNull = schema.type.filter((type) => type !== "null");
	if (withoutNull.length !== 2 || !withoutNull.includes("string")) return false;
	if (!withoutNull.some((type) => NUMERIC_TYPES.has(type))) return false;
	// The format is what proves this is a number that merely tolerates a
	// string, rather than a property that is genuinely one or the other.
	return NUMERIC_FORMATS.has(schema.format);
}

function walk(node) {
	if (Array.isArray(node)) {
		node.forEach(walk);
		return;
	}
	if (!node || typeof node !== "object") return;

	if (isNumberOrStringUnion(node)) {
		const numeric = node.type.find((type) => NUMERIC_TYPES.has(type));
		node.type = node.type.includes("null") ? [numeric, "null"] : numeric;
		// The pattern only described the string alternative that just went away.
		delete node.pattern;
		narrowed++;
	}

	for (const value of Object.values(node)) walk(value);
}

export function normalize() {
	narrowed = 0;
	const document = JSON.parse(readFileSync(SOURCE, "utf8"));
	walk(document);

	// A silent no-op is the failure mode that matters here: the generator would
	// happily emit `number | string` across the app again and the first anyone
	// would know is a hundred type errors in an unrelated PR. If ASP.NET ever
	// stops emitting these unions this script becomes unnecessary - but that is
	// a decision to take deliberately, not to discover.
	if (narrowed === 0) {
		throw new Error(
			"Found no number-or-string unions to narrow in the OpenAPI document. Either the backend " +
				"stopped emitting them (in which case delete this script and the step that runs it), or " +
				"the shape it emits changed and isNumberOrStringUnion() no longer recognises it.",
		);
	}

	mkdirSync(dirname(OUTPUT), { recursive: true });
	writeFileSync(OUTPUT, JSON.stringify(document, null, 2) + "\n");
	return narrowed;
}

if (import.meta.url === `file://${process.argv[1]}`) {
	console.log(
		`Normalized the OpenAPI document: narrowed ${normalize()} number-or-string properties.`,
	);
}
