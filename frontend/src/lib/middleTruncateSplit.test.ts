import { describe, expect, it } from "vitest";
import { splitForMiddleTruncation } from "./middleTruncateSplit";

describe("splitForMiddleTruncation", () => {
	it("always concatenates back to the original text", () => {
		for (const text of [
			"",
			"A",
			"Lindenauer Nachbarschaftshilfe e.V.",
			"Lindenauer Tierschutzverein e.V.",
		]) {
			const [head, tail] = splitForMiddleTruncation(text);
			expect(head + tail).toBe(text);
		}
	});

	it("splits evenly, rounding the head up on an odd length", () => {
		expect(splitForMiddleTruncation("ABCDE")).toEqual(["ABC", "DE"]);
		expect(splitForMiddleTruncation("ABCD")).toEqual(["AB", "CD"]);
	});

	it("keeps the diverging word out of the shared head for two similar org names", () => {
		const [headA] = splitForMiddleTruncation(
			"Lindenauer Nachbarschaftshilfe e.V.",
		);
		const [headB] = splitForMiddleTruncation(
			"Lindenauer Tierschutzverein e.V.",
		);
		expect(headA).not.toBe(headB);
	});
});
