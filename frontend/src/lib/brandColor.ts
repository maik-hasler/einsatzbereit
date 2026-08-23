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
