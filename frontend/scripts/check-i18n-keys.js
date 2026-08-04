#!/usr/bin/env node
import { readFileSync, readdirSync, statSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname, extname } from "path";

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

function flattenValues(obj, prefix = "") {
	const out = {};
	for (const [k, v] of Object.entries(obj)) {
		const full = prefix ? `${prefix}.${k}` : k;
		if (v !== null && typeof v === "object" && !Array.isArray(v)) {
			Object.assign(out, flattenValues(v, full));
		} else {
			out[full] = v;
		}
	}
	return out;
}

function placeholderSet(value) {
	if (typeof value !== "string") return new Set();
	const matches = value.match(/\{\{\s*[\w-]+\s*\}\}|<[a-zA-Z][\w-]*>/g) ?? [];
	return new Set(matches.map((m) => m.replace(/\s+/g, "")));
}

function setsEqual(a, b) {
	if (a.size !== b.size) return false;
	for (const item of a) if (!b.has(item)) return false;
	return true;
}

// Values that are legitimately identical between en.json and de.json - brand
// names, loanwords, interpolation-only strings, and the language names
// themselves (which name a language, not describe something *in* it, so they
// never translate). Anything NOT on this list that happens to match must be
// reviewed: either it's another legitimate loanword (add it here) or it's a
// forgotten translation (#1258).
const ALLOWED_IDENTICAL_KEYS = new Set(
	[
		"brand.name",
		"nav.administration",
		"landing.heroStat3Label",
		"opportunities.remote",
		"opportunities.filterLabelRemote",
		"opportunities.dateRangeDisplay",
		"opportunities.radiusKmValue",
		"opportunities.category.Sport",
		"createOpportunity.step3Title",
		"createOpportunity.step4Title",
		"createOpportunity.fieldTags",
		"createOpportunity.charCount",
		"timeSlots.removing",
		"timeSlots.seriesBadge",
		"organization.nameLabel",
		"orgSettings.fieldLogo",
		"orgSettings.fieldName",
		"orgSettings.fieldWebsite",
		"feedback.organizerTab",
		"checkIn.pinLabel",
		"checkIn.submitting",
		"checkIn.markingCheckedIn",
		"checkIn.undoingCheckIn",
		"myEngagements.addToCalendarOutlook",
		"engagementManagement.processing",
		"engagementManagement.filterLabelStatus",
		"orgEngagements.processing",
		"orgEngagements.filterLabelStatus",
		"privacyPolicy.section4Title",
		"language.de",
		"language.en",
		"report.detailsLabel",
		"report.reasons.Spam",
		"invitations.accepting",
		"invitations.declining",
		"imageCrop.zoomLabel",
		"profile.preferredLanguageDe",
		"profile.preferredLanguageEn",
		"administration.title",
		"administration.users.adminBadge",
		"administration.reports.reason.Spam",
		"orgOverview.tabDashboard",
		"orgOverview.calendarAgenda",
		"orgOverview.eventChipUnlimited",
	].map((k) => `translation.${k}`),
);

// Email-template keys that are legitimately identical (or legitimately both
// empty) between languages.
const EMAIL_TEMPLATE_ALLOWED_IDENTICAL_KEYS = new Set([
	// A reason suffix appended into another template's body - it has no
	// subject line of its own, so both languages leave it empty.
	"EngagementCancelledReasonSuffix.subject",
]);

// Keys that interpolate {{count}} but never need a plural form: the
// surrounding word is grammatically invariant regardless of quantity in both
// languages (a unit abbreviation, a participle, or a fixed-position
// indicator), or - like nCategoriesSelected - the call site never actually
// renders them with count === 1 in the first place.
const PLURAL_EXEMPT_KEYS = new Set(
	[
		"opportunities.nCategoriesSelected",
		"opportunities.radiusKmValue",
		"timeSlots.bookedCount",
		"timeSlots.seriesBadge",
		"timeSlots.editPartialSkip",
		"signUp.spotsLeft",
		"orgOverview.calendarShowMore",
	].map((k) => `translation.${k}`),
);

const PLURAL_SUFFIXES = ["_zero", "_one", "_two", "_few", "_many", "_other"];

function pluralBaseKey(key) {
	for (const suffix of PLURAL_SUFFIXES) {
		if (key.endsWith(suffix)) return key.slice(0, -suffix.length);
	}
	return null;
}

function checkKeyParity(enKeys, deKeys) {
	const missingInDe = [...enKeys].filter((k) => !deKeys.has(k));
	const missingInEn = [...deKeys].filter((k) => !enKeys.has(k));
	const errors = [];
	if (missingInDe.length > 0) {
		errors.push(
			"Keys present in en.json but missing in de.json:\n" +
				missingInDe.map((k) => `  - ${k}`).join("\n"),
		);
	}
	if (missingInEn.length > 0) {
		errors.push(
			"Keys present in de.json but missing in en.json:\n" +
				missingInEn.map((k) => `  - ${k}`).join("\n"),
		);
	}
	return errors;
}

function checkIdenticalValues(enValues, deValues, allowlist, label) {
	const violations = [];
	for (const key of Object.keys(enValues)) {
		if (typeof enValues[key] !== "string") continue;
		if (enValues[key] !== deValues[key]) continue;
		if (allowlist.has(key)) continue;
		violations.push(key);
	}
	if (violations.length === 0) return [];
	return [
		`${label}: values are byte-identical between en and de (looks like an untranslated copy-paste) and not on the allowlist:\n` +
			violations.map((k) => `  - ${k} = ${JSON.stringify(enValues[k])}`).join("\n") +
			"\nIf this is a legitimate loanword/brand name, add it to ALLOWED_IDENTICAL_KEYS in this script.",
	];
}

function checkPlaceholderDrift(enValues, deValues, allKeys, label) {
	const violations = [];
	for (const key of allKeys) {
		if (typeof enValues[key] !== "string" || typeof deValues[key] !== "string") continue;
		const enSet = placeholderSet(enValues[key]);
		const deSet = placeholderSet(deValues[key]);
		if (!setsEqual(enSet, deSet)) {
			violations.push(
				`  - ${key}: en has {${[...enSet].join(", ")}}, de has {${[...deSet].join(", ")}}`,
			);
		}
	}
	if (violations.length === 0) return [];
	return [`${label}: placeholder/{{...}} tokens differ between en and de:\n` + violations.join("\n")];
}

function checkPluralCompleteness(enValues, deValues, label) {
	const violations = [];
	for (const [key, value] of Object.entries(enValues)) {
		if (typeof value !== "string") continue;
		if (pluralBaseKey(key) !== null) continue; // this key IS a plural variant, not a base key to check
		if (!value.includes("{{count}}")) continue;
		if (PLURAL_EXEMPT_KEYS.has(key)) continue;
		const hasOneOrOther =
			`${key}_one` in enValues ||
			`${key}_other` in enValues ||
			`${key}_one` in deValues ||
			`${key}_other` in deValues;
		if (!hasOneOrOther) violations.push(key);
	}
	if (violations.length === 0) return [];
	return [
		`${label}: keys interpolate {{count}} but have no _one/_other plural variant in either language ` +
			"(a bare key alone renders the same text for count=1 as for count=5):\n" +
			violations.map((k) => `  - ${k} = ${JSON.stringify(enValues[k])}`).join("\n"),
	];
}

function checkPluralSuffixParity(enKeys, deKeys, label) {
	// Within each file, a key's plural family (_one/_other/...) must be
	// internally consistent between en and de - e.g. en has X_one/X_other but
	// de only has X_other is a real asymmetry (German still needs both forms).
	const enFamilies = new Map();
	const deFamilies = new Map();
	for (const k of enKeys) {
		const base = pluralBaseKey(k);
		if (base === null) continue;
		if (!enFamilies.has(base)) enFamilies.set(base, new Set());
		enFamilies.get(base).add(k.slice(base.length));
	}
	for (const k of deKeys) {
		const base = pluralBaseKey(k);
		if (base === null) continue;
		if (!deFamilies.has(base)) deFamilies.set(base, new Set());
		deFamilies.get(base).add(k.slice(base.length));
	}
	const allBases = new Set([...enFamilies.keys(), ...deFamilies.keys()]);
	const violations = [];
	for (const base of allBases) {
		const enSuffixes = enFamilies.get(base) ?? new Set();
		const deSuffixes = deFamilies.get(base) ?? new Set();
		if (!setsEqual(enSuffixes, deSuffixes)) {
			violations.push(
				`  - ${base}: en has {${[...enSuffixes].join(", ")}}, de has {${[...deSuffixes].join(", ")}}`,
			);
		}
	}
	if (violations.length === 0) return [];
	return [`${label}: plural-form suffixes differ between en and de for the same key:\n` + violations.join("\n")];
}

// ── Usage check (#1002) ─────────────────────────────────────────────────────
// Parity alone lets dead keys accumulate silently, as long as both locale
// files stay in lockstep. This scans source for t("key") / t(`key`) calls and
// flags any en.json key with no reference - literal or via a dynamic prefix
// (t(`foo.${x}`), t(`foo` + x), t("foo." + x)). A dynamic reference protects
// its whole prefix subtree rather than trying to guess the exact suffix, so
// this only ever produces false negatives (missing a truly dead key), never
// false positives that would break an unrelated PR. Locale-only - email
// templates aren't referenced via t() from frontend source.
function checkUnusedKeys(enKeys) {
	const srcDir = join(__dirname, "../src");

	function walkSourceFiles(dir, out = []) {
		for (const entry of readdirSync(dir)) {
			if (entry === "node_modules") continue;
			const full = join(dir, entry);
			const st = statSync(full);
			if (st.isDirectory()) {
				walkSourceFiles(full, out);
			} else if ([".ts", ".tsx"].includes(extname(entry)) && entry !== "api-client.ts") {
				out.push(full);
			}
		}
		return out;
	}

	// A key is "translation.foo.bar" once flattened - t() calls never spell out
	// the implicit i18next default-namespace root, so strip it before comparing.
	const enKeysNoRoot = new Set([...enKeys].map((k) => k.replace(/^translation\./, "")));

	const sourceText = walkSourceFiles(srcDir)
		.map((f) => readFileSync(f, "utf8"))
		.join("\n");

	const dynamicRoots = new Set();

	// `foo.bar.${x}` / `foo${x}` - static text between the opening backtick and
	// the first ${, truncated to the last "." so a bare suffix like `scope${s}`
	// doesn't wrongly protect the whole file (no dot -> no root added; that shape
	// is caught by the concatenation branch below instead). A dotted one like
	// `foo.bar.${x}` protects "foo.bar". Deliberately NOT anchored to a preceding
	// `t(` - a key is often built into a variable first (e.g.
	// `const key = \`apiError.${code}\`; i18next.t(key)` in lib/apiError.ts) and
	// only passed to t()/i18next.t() afterward.
	for (const m of sourceText.matchAll(/`([A-Za-z0-9_.]*)\$\{/g)) {
		const prefix = m[1];
		const lastDot = prefix.lastIndexOf(".");
		if (lastDot > 0) dynamicRoots.add(prefix.slice(0, lastDot));
	}

	// "foo.bar" + x / "foo.bar." + x / `foo.bar` + x - static text immediately
	// before a `+`. Same reasoning as above: not anchored to a preceding t(.
	for (const m of sourceText.matchAll(/["'`]([A-Za-z0-9_.]+)["'`]\s*\+/g)) {
		const prefix = m[1].replace(/\.$/, "");
		dynamicRoots.add(prefix);
	}

	function isUsed(key) {
		const base = pluralBaseKey(key) ?? key;
		if (
			sourceText.includes(`"${base}"`) ||
			sourceText.includes(`'${base}'`) ||
			sourceText.includes(`\`${base}\``)
		) {
			return true;
		}
		for (const root of dynamicRoots) {
			if (base === root || base.startsWith(`${root}.`)) return true;
		}
		return false;
	}

	const unused = [...enKeysNoRoot].filter((k) => !isUsed(k)).sort();
	if (unused.length === 0) return [];
	return [
		`locales: ${unused.length} translation key(s) have no reference anywhere in frontend/src:\n` +
			unused.map((k) => `  - ${k}`).join("\n"),
	];
}

function runParityChecks(enPath, dePath, label) {
	const en = JSON.parse(readFileSync(enPath, "utf8"));
	const de = JSON.parse(readFileSync(dePath, "utf8"));
	const enKeys = flattenKeys(en);
	const deKeys = flattenKeys(de);
	const enValues = flattenValues(en);
	const deValues = flattenValues(de);

	const identicalAllowlist =
		label === "locales" ? ALLOWED_IDENTICAL_KEYS : EMAIL_TEMPLATE_ALLOWED_IDENTICAL_KEYS;

	return [
		...checkKeyParity(enKeys, deKeys),
		...checkIdenticalValues(enValues, deValues, identicalAllowlist, label),
		...checkPlaceholderDrift(enValues, deValues, enKeys, label),
		...checkPluralCompleteness(enValues, deValues, label),
		...checkPluralSuffixParity(enKeys, deKeys, label),
		...(label === "locales" ? checkUnusedKeys(enKeys) : []),
	];
}

const localesDir = join(__dirname, "../src/locales");
const emailTemplatesDir = join(__dirname, "../../backend/src/Infrastructure/Email/Templates");

const errors = [
	...runParityChecks(join(localesDir, "en.json"), join(localesDir, "de.json"), "locales"),
	...runParityChecks(
		join(emailTemplatesDir, "en.json"),
		join(emailTemplatesDir, "de.json"),
		"email templates",
	),
];

if (errors.length > 0) {
	for (const error of errors) console.error(error + "\n");
	process.exit(1);
}

const totalKeys = flattenKeys(JSON.parse(readFileSync(join(localesDir, "en.json"), "utf8"))).size;
console.log(`All ${totalKeys} locale keys match between de.json and en.json, plus email templates - no drift detected.`);
