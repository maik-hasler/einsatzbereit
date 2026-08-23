import { expect } from "vitest";
import { axe } from "vitest-axe";
import type { RunOptions } from "axe-core";

const ESCALATED_MODERATE_RULE_IDS = ["heading-order", "landmark-unique"];

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
