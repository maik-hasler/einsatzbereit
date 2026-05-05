#!/usr/bin/env node
import { readFileSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));

function flattenKeys(obj, prefix = "") {
	const keys = new Set();
	for (const [k, v] of Object.entries(obj)) {
		const full = prefix ? `${prefix}.${k}` : k;
		if (v !== null && typeof v === "object" && !Array.isArray(v)) {
			for (const nested of flattenKeys(v, full)) keys.add(nested);
		} else {
			keys.add(full);
		}
	}
	return keys;
}

const localesDir = join(__dirname, "../src/locales");
const de = JSON.parse(readFileSync(join(localesDir, "de.json"), "utf8"));
const en = JSON.parse(readFileSync(join(localesDir, "en.json"), "utf8"));

const deKeys = flattenKeys(de);
const enKeys = flattenKeys(en);

const missingInDe = [...enKeys].filter((k) => !deKeys.has(k));
const missingInEn = [...deKeys].filter((k) => !enKeys.has(k));

let ok = true;

if (missingInDe.length > 0) {
	console.error("Keys present in en.json but missing in de.json:");
	for (const k of missingInDe) console.error(`  - ${k}`);
	ok = false;
}

if (missingInEn.length > 0) {
	console.error("Keys present in de.json but missing in en.json:");
	for (const k of missingInEn) console.error(`  - ${k}`);
	ok = false;
}

if (ok) {
	console.log(`All ${enKeys.size} keys match between de.json and en.json.`);
} else {
	process.exit(1);
}
