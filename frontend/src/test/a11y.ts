import { expect } from "vitest";
import { axe } from "vitest-axe";
import type { RunOptions } from "axe-core";

// Moderate-impact rules this project treats as failing. Everything axe rates
// serious or critical already fails; these two are the moderate ones that can
// still be judged on a component in isolation.
const ESCALATED_MODERATE_RULE_IDS = ["heading-order", "landmark-unique"];

// Rules that only mean something about a whole page, and so are waived here
// and answered by the page-level scan in
// backend/tests/VisualTests/AccessibilityTests.cs. Split by HOW that scan
// answers them, because the three cases have different consequences and the
// difference was previously written down nowhere - `pnpm check:a11y-rules`
// reads these lists and the C# one and fails when they stop agreeing.

// 1. Moderate there too, and named in that file's EscalatedModerateRuleIds.
const PAGE_SCOPED_RULES_ESCALATED_ON_PAGES = [
	"page-has-heading-one",
	"landmark-one-main",
	"landmark-banner-is-top-level",
	"landmark-complementary-is-top-level",
	"landmark-contentinfo-is-top-level",
	"landmark-main-is-top-level",
	"landmark-no-duplicate-banner",
	"landmark-no-duplicate-contentinfo",
	"landmark-no-duplicate-main",
];

// 2. Rated serious or critical by axe, so the page scan's first clause
// (`v.Impact is "serious" or "critical"`) already fails on them - no
// escalation entry needed, and adding one would be noise.
const PAGE_SCOPED_RULES_COVERED_BY_SEVERITY = [
	"color-contrast",
	"color-contrast-enhanced",
	"bypass",
	"html-has-lang",
	"html-lang-valid",
	"document-title",
];

// 3. The gap. Both are moderate, both are waived here, and neither is in the
// page scan's escalation list - so today they fail at neither altitude.
// Closing it means adding them to AccessibilityTests.cs, which turns a
// currently-green Playwright suite red on rules that have never been enforced;
// that is a change worth making on its own, with the resulting violations
// actually looked at, rather than as a side effect of writing this list down.
// Recorded here so the hole is visible in code instead of invisible in the
// space between two files.
const PAGE_SCOPED_RULES_NOT_YET_ESCALATED = [
	"region",
	"html-xml-lang-mismatch",
];

const PAGE_SCOPED_RULES = [
	...PAGE_SCOPED_RULES_ESCALATED_ON_PAGES,
	...PAGE_SCOPED_RULES_COVERED_BY_SEVERITY,
	...PAGE_SCOPED_RULES_NOT_YET_ESCALATED,
];

const disabledRules: RunOptions["rules"] = Object.fromEntries(
	PAGE_SCOPED_RULES.map((id) => [id, { enabled: false }]),
);

interface AxeViolationNode {
	html: string;
}

interface AxeViolation {
	id: string;
	impact?: string | null;
	description: string;
	nodes: AxeViolationNode[];
}

export async function expectNoA11yViolations(
	target: Element | Document = document.body,
	options: RunOptions = {},
): Promise<void> {
	const element = target instanceof Document ? target.body : target;

	if (!element.isConnected) {
		throw new Error(
			"expectNoA11yViolations was given a detached element - render it (or pass its container) so axe scans the live tree.",
		);
	}

	// A component that renders null - a guard clause that fired, a prop shape
	// that silently changed - produces an empty subtree, and an empty subtree
	// has no violations. Without this the test still passes, and the suite
	// quietly stops covering the component it names.
	if (element.childElementCount === 0) {
		throw new Error(
			"expectNoA11yViolations was given an empty subtree - nothing was rendered, so the scan would pass vacuously.",
		);
	}

	const results = (await axe(element as HTMLElement, {
		...options,
		rules: { ...disabledRules, ...options.rules },
	})) as { violations: AxeViolation[] };

	const violations = results.violations.filter(
		(v) =>
			v.impact === "serious" ||
			v.impact === "critical" ||
			(v.impact === "moderate" && ESCALATED_MODERATE_RULE_IDS.includes(v.id)),
	);

	const summary = violations
		.map(
			(v) =>
				`[${v.impact}] ${v.id}: ${v.description}\n` +
				v.nodes.map((n) => `  - ${n.html}`).join("\n"),
		)
		.join("\n");

	expect(
		violations.length,
		`Axe found ${violations.length} a11y violation(s):\n${summary}`,
	).toBe(0);
}
