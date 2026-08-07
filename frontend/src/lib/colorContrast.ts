// Organizers pick a raw calendar event color via an unconstrained OS color
// picker (CalendarWidget.tsx) with no contrast check - a plausible pick like
// #ffff00 renders unreadable white text at 1.07:1 (einsatzbereit#1286).
// Mirrors backend/src/Domain/VolunteerOpportunities/EventColorContrast.cs's
// formula (WCAG relative luminance) so client and server agree on what
// "readable" means, including the two independent floors documented there:
// MinimumContrastRatio (chip vs. page, 1.4.11) and MinimumTextContrastRatio
// (chip text, 1.4.3, einsatzbereit#1726).
const DARK_TEXT = "#111827";

/** WCAG SC 1.4.3 floor for the better of white/near-black chip text - see EventColorContrast.cs's MinimumTextContrastRatio. */
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

/** Picks whichever of white/near-black clears more contrast against `backgroundHex`. */
export function readableTextColor(backgroundHex: string): string {
	const bgLuminance = relativeLuminance(backgroundHex);
	const whiteContrast = luminanceContrastRatio(1, bgLuminance);
	const darkContrast = luminanceContrastRatio(
		relativeLuminance(DARK_TEXT),
		bgLuminance,
	);
	return whiteContrast >= darkContrast ? "#ffffff" : DARK_TEXT;
}

/** WCAG contrast ratio (1:1 to 21:1) between two colors - the same formula SC 1.4.3 uses. */
export function contrastRatio(hexA: string, hexB: string): number {
	return luminanceContrastRatio(
		relativeLuminance(hexA),
		relativeLuminance(hexB),
	);
}

/** The higher of white-on-color and near-black-on-color contrast - whichever text color `readableTextColor` would actually pick for `backgroundHex`. */
export function bestTextContrastRatio(backgroundHex: string): number {
	const bgLuminance = relativeLuminance(backgroundHex);
	const whiteContrast = luminanceContrastRatio(1, bgLuminance);
	const darkContrast = luminanceContrastRatio(
		relativeLuminance(DARK_TEXT),
		bgLuminance,
	);
	return Math.max(whiteContrast, darkContrast);
}

/** Whether either text color choice for `backgroundHex` clears the WCAG AA 4.5:1 text floor. */
export function meetsTextContrastFloor(backgroundHex: string): boolean {
	return bestTextContrastRatio(backgroundHex) >= MIN_TEXT_CONTRAST_RATIO;
}
