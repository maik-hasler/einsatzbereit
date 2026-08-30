import { describe, it, expect } from "vitest";
import { resolveDropdownPlacement } from "./dropdownPlacement";

const EDGE_MARGIN = 8;

function placement(overrides: {
	triggerTop: number;
	triggerBottom: number;
	panelHeight: number;
	viewportHeight?: number;
}) {
	return resolveDropdownPlacement({
		viewportHeight: 844,
		edgeMargin: EDGE_MARGIN,
		...overrides,
	});
}

describe("resolveDropdownPlacement", () => {
	it("opens below when the panel fits there", () => {
		expect(
			placement({ triggerTop: 100, triggerBottom: 130, panelHeight: 302 }),
		).toBe("below");
	});

	it("flips above when the panel would run past the bottom of the viewport", () => {
		// The reported case: a 390x844 phone, the date chip at y 562-592, and a 302px
		// picker whose bottom would land at 894 - 50px past the fold (#2319).
		expect(
			placement({ triggerTop: 562, triggerBottom: 592, panelHeight: 302 }),
		).toBe("above");
	});

	it("stays below when neither side fits but below is the roomier one", () => {
		expect(
			placement({
				triggerTop: 40,
				triggerBottom: 70,
				panelHeight: 2000,
			}),
		).toBe("below");
	});

	it("keeps the edge margin out of the space it measures", () => {
		// Exactly enough room below only once the margin is *not* deducted; deducting
		// it is what stops the panel butting up against the viewport edge.
		expect(
			placement({ triggerTop: 500, triggerBottom: 542, panelHeight: 302 }),
		).toBe("above");
		expect(
			placement({ triggerTop: 500, triggerBottom: 534, panelHeight: 302 }),
		).toBe("below");
	});
});
