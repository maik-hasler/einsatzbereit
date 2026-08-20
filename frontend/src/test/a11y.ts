import { expect } from "vitest";
import { axe } from "vitest-axe";
import type { RunOptions } from "axe-core";

/**
 * Component-level axe-core scanning, the counterpart to
 * `backend/tests/VisualTests/AccessibilityTests.cs`'s `AssertNoViolations`.
 *
 * The two gates deliberately apply the *same* impact filter (see
 * `IMPACT_FILTER` below), so moving a scan down from Playwright to here is a
 * change of altitude, not a change of standard. What differs is scope, and it
 * differs in both directions:
 *
 * - Rules about whole-document structure (one <main>, a page <h1>, a lang
 *   attribute, a bypass link) cannot be judged from a fragment, so they stay
 *   in the Playwright page smokes.
 * - `color-contrast` needs real layout and a canvas to sample rendered pixels.
 *   jsdom has neither, so axe can only ever report it "incomplete" here. The
 *   Playwright smokes remain the only place contrast is actually evaluated -
 *   do not read a green run of this suite as a contrast check.
 */

// Axe reports "page has no h1" and most landmark-structure defects at
// "moderate" impact, which the serious/critical filter would let through.
// Only the two that are meaningful inside a single component are escalated
// here - the rest are page-scoped and disabled below. Kept in step with
// AccessibilityTests.cs's EscalatedModerateRuleIds.
const ESCALATED_MODERATE_RULE_IDS = ["heading-order", "landmark-unique"];

// Turned off rather than filtered out: a rule that can only ever be
// inconclusive (color-contrast) or is being asked a question the fragment
// cannot answer (everything else) produces noise that reads like a real
// finding on the next person to open a failure.
const PAGE_SCOPED_RULES = [
	"color-contrast",
	"color-contrast-enhanced",
	"page-has-heading-one",
	"landmark-one-main",
	"landmark-banner-is-top-level",
	"landmark-complementary-is-top-level",
	"landmark-contentinfo-is-top-level",
	"landmark-main-is-top-level",
	"landmark-no-duplicate-banner",
	"landmark-no-duplicate-contentinfo",
	"landmark-no-duplicate-main",
	"region",
	"bypass",
	"html-has-lang",
	"html-lang-valid",
	"html-xml-lang-mismatch",
	"document-title",
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

/**
 * Scans `target` (default: the whole rendered body, so portalled dialogs and
 * their backdrops are included) and fails on any serious/critical violation.
 */
export async function expectNoA11yViolations(
	target: Element | Document = document.body,
	options: RunOptions = {},
): Promise<void> {
	const element = target instanceof Document ? target.body : target;

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
