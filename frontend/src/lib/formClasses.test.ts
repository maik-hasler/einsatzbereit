import { describe, it, expect } from "vitest";
import {
	inputSurfaceClass,
	inputClass,
	textareaClass,
	labelClass,
	selectClass,
	fieldBorderClass,
	getInputClass,
	getTextareaClass,
	getLabelClass,
} from "./formClasses";

describe("formClasses", () => {
	it("gives inputSurfaceClass the shared visual recipe (radius/border/focus)", () => {
		expect(inputSurfaceClass).toContain("rounded-xl");
		expect(inputSurfaceClass).toContain("border-gray-200");
		expect(inputSurfaceClass).toContain("focus:border-brand-400");
		expect(inputSurfaceClass).toContain("shadow-sm");
	});

	it("keeps inputSurfaceClass free of block-level layout classes", () => {
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

	it("never paints the select chevron as a data: background image (#2225)", () => {
		// The deployed CSP's img-src has no `data:` - a `bg-[url('data:...')]` chevron
		// is silently blocked while appearance-none has already removed the native
		// arrow, leaving the control looking disabled. components/Select.tsx draws
		// the chevron as an inline <svg> instead; this class must stay free of any
		// background-image so that fix can't silently regress.
		expect(selectClass).not.toContain("data:");
		expect(selectClass).not.toContain("bg-[url(");
		expect(selectClass).toContain("appearance-none");
	});

	it("gives every invalid field the same red border/focus treatment (#2239)", () => {
		expect(fieldBorderClass(true)).toContain("border-red-300");
		expect(fieldBorderClass(true)).toContain("focus:border-red-400");
		expect(fieldBorderClass(true)).not.toContain("border-gray-200");
	});

	it("keeps the pristine border/focus treatment when there is no error", () => {
		expect(fieldBorderClass(false)).toContain("border-gray-200");
		expect(fieldBorderClass(false)).toContain("focus:border-brand-400");
		expect(fieldBorderClass(false)).not.toContain("border-red-300");
	});

	it("carries the invalid border into getInputClass/getTextareaClass", () => {
		expect(getInputClass(true)).toContain("border-red-300");
		expect(getTextareaClass(true)).toContain("border-red-300");
		expect(getTextareaClass(true)).toContain("resize-y");
	});

	it("defaults getInputClass/getTextareaClass to the pristine treatment", () => {
		expect(getInputClass()).toBe(inputClass);
		expect(getTextareaClass()).toBe(textareaClass);
	});

	it("turns the label red on error, same as the field it labels", () => {
		expect(getLabelClass(true)).toContain("text-red-600");
		expect(getLabelClass(false)).toBe(labelClass);
	});
});
