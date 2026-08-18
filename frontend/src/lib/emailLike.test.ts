import { describe, it, expect } from "vitest";
import { looksLikeEmail } from "./emailLike";

describe("looksLikeEmail", () => {
	it("matches a syntactically valid email", () => {
		expect(looksLikeEmail("brandnewperson@example.com")).toBe(true);
	});

	it("ignores surrounding whitespace", () => {
		expect(looksLikeEmail("  person@example.com  ")).toBe(true);
	});

	it("rejects a plain username", () => {
		expect(looksLikeEmail("brandnewperson")).toBe(false);
	});

	it("rejects a query containing a space", () => {
		expect(looksLikeEmail("brand newperson@example.com")).toBe(false);
	});

	it("rejects an @ with no domain suffix", () => {
		expect(looksLikeEmail("person@example")).toBe(false);
	});

	it("rejects an empty string", () => {
		expect(looksLikeEmail("")).toBe(false);
	});
});
