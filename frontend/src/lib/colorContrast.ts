const DARK_TEXT = "#111827";

export const MIN_TEXT_CONTRAST_RATIO = 4.5;

function toRgb(hex: string): { r: number; g: number; b: number } {
	const normalized = hex.length === 4 ? expandShorthand(hex) : hex;
	const int = parseInt(normalized.slice(1), 16);
	return { r: (int >> 16) & 255, g: (int >> 8) & 255, b: int & 255 };
}

function expandShorthand(hex: string): string {
	return `#${hex[1]}${hex[1]}${hex[2]}${hex[2]}${hex[3]}${hex[3]}`;
}

function toLinear(channel: number): number {
	const c = channel / 255;
	return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
}

function relativeLuminance(hex: string): number {
	const { r, g, b } = toRgb(hex);
	return 0.2126 * toLinear(r) + 0.7152 * toLinear(g) + 0.0722 * toLinear(b);
}

function luminanceContrastRatio(
	luminanceA: number,
	luminanceB: number,
): number {
	const lighter = Math.max(luminanceA, luminanceB);
	const darker = Math.min(luminanceA, luminanceB);
	return (lighter + 0.05) / (darker + 0.05);
}

export function readableTextColor(backgroundHex: string): string {
	const bgLuminance = relativeLuminance(backgroundHex);
	const whiteContrast = luminanceContrastRatio(1, bgLuminance);
	const darkContrast = luminanceContrastRatio(
		relativeLuminance(DARK_TEXT),
		bgLuminance,
	);
	return whiteContrast >= darkContrast ? "#ffffff" : DARK_TEXT;
}

export function contrastRatio(hexA: string, hexB: string): number {
	return luminanceContrastRatio(
		relativeLuminance(hexA),
		relativeLuminance(hexB),
	);
}

export function bestTextContrastRatio(backgroundHex: string): number {
	const bgLuminance = relativeLuminance(backgroundHex);
	const whiteContrast = luminanceContrastRatio(1, bgLuminance);
	const darkContrast = luminanceContrastRatio(
		relativeLuminance(DARK_TEXT),
		bgLuminance,
	);
	return Math.max(whiteContrast, darkContrast);
}

export function meetsTextContrastFloor(backgroundHex: string): boolean {
	return bestTextContrastRatio(backgroundHex) >= MIN_TEXT_CONTRAST_RATIO;
}
