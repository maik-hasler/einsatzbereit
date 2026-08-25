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

	it("keeps the select chevron's data URI free of raw whitespace Tailwind can't parse (#2051)", () => {
		const urlToken = selectClass.match(/bg-\[url\('([^']+)'\)\]/);
		expect(urlToken).not.toBeNull();
		const dataUri = urlToken?.[1] ?? "";
		expect(dataUri).not.toBe("");
		expect(/\s/.test(dataUri)).toBe(false);

		const svgMarkup = decodeURIComponent(
			dataUri.slice(dataUri.indexOf(",") + 1),
		);
		const doc = new DOMParser().parseFromString(svgMarkup, "image/svg+xml");
		expect(doc.querySelector("parsererror")).toBeNull();
		expect(doc.querySelector("svg")).not.toBeNull();
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
