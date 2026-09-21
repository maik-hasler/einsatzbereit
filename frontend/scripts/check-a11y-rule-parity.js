#!/usr/bin/env node

// The axe gate is applied at two altitudes, in two languages.
//
//   frontend/src/test/a11y.ts           - per component, jsdom, `pnpm test`
//   backend/tests/VisualTests/
//     AccessibilityTests.cs             - per page, real Chromium, dotnet.yml
//
// Both fail on serious/critical, and both escalate a hand-picked set of
// *moderate* rules to failing. The component side additionally DISABLES the
// rules that cannot be judged on a fragment - landmarks, page headings,
// colour contrast, document title - and hands them to the page side.
//
// That hand-off is the invariant this script guards: every rule the component
// scan waives as page-scoped has to be caught by the page scan, either because
// axe rates it serious/critical (already covered by the C# predicate) or
// because the C# escalation list names it. A rule that is moderate, waived
// here and unnamed there is enforced at neither altitude - a hole that is
// invisible from inside either file.
//
// The two lists are parsed rather than shared because a shared file would have
// to be plumbed into VisualTests.csproj, which today has no asset items at
// all. `check-i18n-keys.js` already sets the precedent for a frontend check
// reading the backend tree.

import { readFileSync } from "fs";
import { fileURLToPath } from "url";
import { join, dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(__dirname, "../..");

const TS_FILE = "frontend/src/test/a11y.ts";
const CS_FILE = "backend/tests/VisualTests/AccessibilityTests.cs";

const tsSource = readFileSync(join(repoRoot, TS_FILE), "utf8");
const csSource = readFileSync(join(repoRoot, CS_FILE), "utf8");

let ok = true;
function fail(message) {
	console.error(message);
	ok = false;
}

/** Pulls the quoted rule ids out of a named array literal, in either language. */
function readRuleIds(source, file, declaration) {
	const block = new RegExp(`${declaration}[^[]*\\[([^\\]]*)\\]`).exec(source);
	if (!block) {
		fail(
			`Could not find \`${declaration}\` in ${file}. If it was renamed, rename it here too - ` +
				"a parse that silently finds nothing would turn this check green for the wrong reason.",
		);
		return null;
	}
	const ids = [...block[1].matchAll(/"([a-z0-9-]+)"/g)].map((m) => m[1]);
	if (ids.length === 0) {
		fail(`\`${declaration}\` in ${file} parsed to an empty list.`);
		return null;
	}
	return ids;
}

const tsEscalated = readRuleIds(tsSource, TS_FILE, "ESCALATED_MODERATE_RULE_IDS");
const tsEscalatedOnPages = readRuleIds(
	tsSource,
	TS_FILE,
	"PAGE_SCOPED_RULES_ESCALATED_ON_PAGES",
);
const tsCoveredBySeverity = readRuleIds(
	tsSource,
	TS_FILE,
	"PAGE_SCOPED_RULES_COVERED_BY_SEVERITY",
);
const tsKnownUncovered = readRuleIds(
	tsSource,
	TS_FILE,
	"PAGE_SCOPED_RULES_NOT_YET_ESCALATED",
);
const csEscalated = readRuleIds(csSource, CS_FILE, "EscalatedModerateRuleIds");

if (
	tsEscalated &&
	tsEscalatedOnPages &&
	tsCoveredBySeverity &&
	tsKnownUncovered &&
	csEscalated
) {
	// PAGE_SCOPED_RULES itself is a spread of the three lists below, so it is
	// rebuilt here rather than parsed - a regex over `[...A, ...B]` would find
	// no quoted ids and read as an empty list.
	const tsPageScoped = [
		...tsEscalatedOnPages,
		...tsCoveredBySeverity,
		...tsKnownUncovered,
	];
	const cs = new Set(csEscalated);
	const pageScoped = new Set(tsPageScoped);
	const waivedWithReason = new Set([
		...tsCoveredBySeverity,
		...tsKnownUncovered,
	]);

	// 1. Anything the component scan escalates, the page scan escalates too -
	//    otherwise a rule is stricter on a fragment than on the whole page.
	for (const id of tsEscalated) {
		if (!cs.has(id)) {
			fail(
				`"${id}" is escalated to failing in ${TS_FILE} but not in ${CS_FILE}. A moderate rule that ` +
					"fails on a component and passes on the page it lives in is a contradiction; add it to " +
					"EscalatedModerateRuleIds.",
			);
		}
	}

	// 2. Anything the page scan escalates is either waived by the component
	//    scan as page-scoped, or escalated there too. A third case means the
	//    lists have drifted apart without anyone deciding to.
	for (const id of csEscalated) {
		if (!pageScoped.has(id) && !tsEscalated.includes(id)) {
			fail(
				`"${id}" is escalated in ${CS_FILE} but ${TS_FILE} neither escalates it nor waives it as ` +
					"page-scoped. Add it to ESCALATED_MODERATE_RULE_IDS (it can be judged on a fragment) or " +
					"to PAGE_SCOPED_RULES (it cannot).",
			);
		}
	}

	// 3. The hole this check exists for: a page-scoped waiver that nothing on
	//    the page side answers. Each waived rule has to be in exactly one of
	//    the three buckets - escalated there, covered there by severity, or
	//    recorded as a known gap - so "enforced nowhere" cannot happen by
	//    omission.
	for (const id of tsPageScoped) {
		if (cs.has(id) || waivedWithReason.has(id)) continue;
		fail(
			`"${id}" is waived as page-scoped in ${TS_FILE} but ${CS_FILE} does not escalate it. If axe ` +
				"rates it serious or critical the page scan already catches it - record it in " +
				"PAGE_SCOPED_RULES_NOT_YET_ESCALATED with that reason. Otherwise it is enforced at neither " +
				"altitude.",
		);
	}

	// 4. A recorded waiver cannot outlive its reason: once the page scan
	//    escalates a rule, the note saying it does not is wrong.
	for (const id of [...tsCoveredBySeverity, ...tsKnownUncovered]) {
		if (!pageScoped.has(id)) {
			fail(
				`"${id}" is recorded as a page-scoped waiver in ${TS_FILE} but is no longer disabled ` +
					"there. Drop it from the list that still names it.",
			);
		}
		if (cs.has(id)) {
			fail(
				`"${id}" is recorded in ${TS_FILE} as unanswered by the page scan, but ${CS_FILE} now ` +
					"escalates it. Move it to PAGE_SCOPED_RULES_ESCALATED_ON_PAGES.",
			);
		}
	}
}

if (ok) {
	console.log(
		"The component and page axe gates agree: every page-scoped waiver is answered by the page scan.",
	);
} else {
	process.exit(1);
}
