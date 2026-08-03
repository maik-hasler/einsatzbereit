// Brand color tokens live in one place - styles/global.css's @theme block -
// so retheming (or just darkening a shade for contrast) doesn't silently
// miss a spot. Plain CSS can read them directly via var(--color-brand-700);
// the few call sites that need an actual hex string instead (Leaflet's SVG
// marker markup, react-big-calendar's inline eventPropGetter, a native
// <input type="color"> value) read the same custom properties at runtime
// here rather than re-typing the literals a third time (#1129).
const brandColorCache = new Map<string, string>();

export function brandColor(shade: "600" | "700" | "800"): string {
	const cached = brandColorCache.get(shade);
	if (cached) return cached;
	const value = getComputedStyle(document.documentElement)
		.getPropertyValue(`--color-brand-${shade}`)
		.trim();
	brandColorCache.set(shade, value);
	return value;
}
