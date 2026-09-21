import { designTokens } from "./designTokens";

/**
 * A brand colour, for the few places that need one as a value rather than a
 * class - a Leaflet marker's fill, a canvas stroke, an inline SVG attribute.
 *
 * This used to read the CSS custom property back out of the document with
 * `getComputedStyle(document.documentElement)`, cached in a Map because that
 * call forces a style recalculation. It was a browser round trip to learn a
 * constant, and it made a folder that is otherwise plain logic depend on there
 * being a DOM at all. `lib/designTokens.ts` is generated from the same
 * `@theme` block Tailwind reads, so the value is the same one - it just does
 * not need a document to find it.
 */
export function brandColor(shade: "600" | "700" | "800"): string {
	return designTokens[`--color-brand-${shade}`];
}
