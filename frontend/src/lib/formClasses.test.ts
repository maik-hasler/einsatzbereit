import { describe, it, expect } from "vitest";
import {
	inputSurfaceClass,
	inputClass,
	textareaClass,
	labelClass,
} from "./formClasses";

describe("formClasses", () => {
	it("gives inputSurfaceClass the shared visual recipe (radius/border/focus)", () => {
		expect(inputSurfaceClass).toContain("rounded-xl");
		expect(inputSurfaceClass).toContain("border-gray-200");
		expect(inputSurfaceClass).toContain("focus:border-brand-400");
		expect(inputSurfaceClass).toContain("shadow-sm");
	});

	it("keeps inputSurfaceClass free of block-level layout classes", () => {
		// Dropdown's trigger button is `flex` - composing inputSurfaceClass
		// into its className must never add a `block` that contradicts it
		// (einsatzbereit#1104).
		expect(inputSurfaceClass.split(" ")).not.toContain("block");
		expect(inputSurfaceClass.split(" ")).not.toContain("mt-1");
	});

	it("builds inputClass from inputSurfaceClass plus block-level layout", () => {
		for (const cls of inputSurfaceClass.split(" ")) {
			expect(inputClass).toContain(cls);
		}
		expect(inputClass).toContain("mt-1");
		expect(inputClass).toContain("block");
		expect(inputClass).toContain("text-gray-900");
	});

	it("extends inputClass with vertical resize for textareaClass", () => {
		for (const cls of inputClass.split(" ")) {
			expect(textareaClass).toContain(cls);
		}
		expect(textareaClass).toContain("resize-y");
	});

	it("keeps labelClass a small muted label style", () => {
		expect(labelClass).toContain("text-xs");
		expect(labelClass).toContain("text-gray-600");
	});
});
