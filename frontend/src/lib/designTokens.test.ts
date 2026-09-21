import { describe, it, expect } from "vitest";
import { designTokens, designToken } from "./designTokens";

// Parity with the @theme block this module is generated from is enforced by
// `pnpm check:design-tokens` (a CI step), not here: Vitest stubs CSS imports,
// so a `?raw` import of the stylesheet comes back empty and a comparison
// against it would pass against nothing. What is worth asserting here is the
// module's own contract.
describe("designTokens", () => {
	it("carries resolved values, not unresolved custom-property references", () => {
		for (const [name, value] of Object.entries(designTokens)) {
			expect(value, `${name} is empty`).not.toBe("");
			expect(value, `${name} still points at another variable`).not.toMatch(
				/var\(/,
			);
		}
	});

	it("names every token by its CSS custom-property name", () => {
		for (const name of Object.keys(designTokens)) {
			expect(name).toMatch(/^--[a-z0-9-]+$/);
		}
	});

	it("reads a value back without touching the document", () => {
		expect(designToken("--color-brand-700")).toMatch(/^#[0-9a-f]{6}$/i);
	});
});
