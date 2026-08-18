import { describe, it, expect } from "vitest";
import {
	inputSurfaceClass,
	inputClass,
	textareaClass,
	labelClass,
	selectClass,
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

	it("keeps the select chevron's data URI free of raw whitespace Tailwind can't parse (#2051)", () => {
		// Tailwind splits utility candidates on whitespace, so a raw space
		// inside this arbitrary value silently drops the whole bg-[url(...)]
		// class from the generated CSS - the chevron then never renders.
		const urlToken = selectClass.match(/bg-\[url\('([^']+)'\)\]/);
		expect(urlToken).not.toBeNull();
		const dataUri = urlToken?.[1] ?? "";
		expect(dataUri).not.toBe("");
		expect(/\s/.test(dataUri)).toBe(false);

		// Guard against swapping in underscores instead: Tailwind detects
		// url(...) arbitrary values and, unlike other arbitrary values,
		// leaves underscores as literal underscores rather than converting
		// them to spaces - which would corrupt the SVG markup below.
		const svgMarkup = decodeURIComponent(
			dataUri.slice(dataUri.indexOf(",") + 1),
		);
		const doc = new DOMParser().parseFromString(svgMarkup, "image/svg+xml");
		expect(doc.querySelector("parsererror")).toBeNull();
		expect(doc.querySelector("svg")).not.toBeNull();
	});
});
