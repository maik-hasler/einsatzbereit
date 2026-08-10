import { describe, expect, it } from "vitest";
import { getInitials } from "./initials";

describe("getInitials", () => {
	it("takes the first letter of the first and last word", () => {
		expect(getInitials("Vera Volunteer")).toBe("VV");
		expect(getInitials("Olaf Organisator")).toBe("OO");
	});

	it("skips a legal-form suffix so an org reads by its name", () => {
		expect(getInitials("Lindenauer Tierschutzverein e.V.")).toBe("LT");
		expect(getInitials("Lindenauer Nachbarschaftshilfe e.V.")).toBe("LN");
		expect(getInitials("Muster GmbH")).toBe("MU");
	});

	it("falls back to the first two letters for a single word", () => {
		expect(getInitials("Tierschutzverein")).toBe("TI");
	});

	it("uses the middle word when first and last are the same letter", () => {
		expect(getInitials("Anna Marie Albrecht")).toBe("AA");
	});

	it("handles empty and punctuation-only input", () => {
		expect(getInitials("")).toBe("?");
		expect(getInitials("   ")).toBe("?");
		expect(getInitials("- -")).toBe("?");
	});
});
