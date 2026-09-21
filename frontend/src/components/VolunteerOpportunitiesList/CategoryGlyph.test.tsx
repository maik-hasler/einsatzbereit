import { describe, it, expect } from "vitest";
import { render } from "@testing-library/react";
import { CategoryGlyph } from "./CategoryGlyph";

/**
 * The glyph is decorative - it repeats a category the card already names in
 * words - so there is no accessible name to assert on. What matters is that
 * each category gets a distinct mark, that an unknown one still gets a mark
 * rather than a hole in the layout, and that the size the caller asked for is
 * the size it gets.
 */
function markup(category: string | null | undefined, className?: string) {
	const { container } = render(
		<CategoryGlyph category={category} className={className} />,
	);
	return container.innerHTML;
}

const CATEGORIES = [
	"Social",
	"Environment",
	"Sport",
	"Education",
	"DisasterRelief",
	"Health",
	"Animals",
	"Culture",
	"Technology",
] as const;

describe("CategoryGlyph", () => {
	it("gives every known category a glyph of its own", () => {
		const byCategory = new Map(CATEGORIES.map((c) => [c, markup(c)]));

		expect(new Set(byCategory.values()).size).toBe(CATEGORIES.length);
	});

	// An opportunity with no category, or one added to the backend enum before
	// this switch learns about it, still has to render something - a card with a
	// missing glyph reflows around the gap.
	it.each([undefined, null, "", "SomethingAddedLater"])(
		"falls back to a glyph for %p",
		(category) => {
			expect(markup(category)).toContain("<svg");
		},
	);

	it("uses the same fallback glyph for every unrecognised category", () => {
		expect(markup("SomethingAddedLater")).toBe(markup(null));
	});

	it("renders the size the caller asked for", () => {
		expect(markup("Social", "h-3 w-3")).toContain("h-3 w-3");
	});

	it("has a default size, for callers that do not care", () => {
		expect(markup("Social")).toContain("h-10 w-10");
	});

	// aria-hidden, not role="img": the card states the category in text beside
	// it, and announcing it twice is worse than not announcing it at all.
	it("stays out of the accessibility tree", () => {
		const { container } = render(<CategoryGlyph category="Social" />);

		expect(container.querySelector("svg")).toHaveAttribute(
			"aria-hidden",
			"true",
		);
	});
});
