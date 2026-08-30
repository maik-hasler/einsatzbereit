import { describe, expect, it } from "vitest";

import { quoteMarks } from "./quotes";

describe("quoteMarks", () => {
	it("uses German low-then-high quotes for de", () => {
		expect(quoteMarks("de")).toEqual({ open: "„", close: "“" });
	});

	it("uses English high-then-high quotes for en", () => {
		expect(quoteMarks("en")).toEqual({ open: "“", close: "”" });
	});

	it("falls back to German, the same way pickLocalizedText does", () => {
		expect(quoteMarks("fr")).toEqual(quoteMarks("de"));
	});
});
