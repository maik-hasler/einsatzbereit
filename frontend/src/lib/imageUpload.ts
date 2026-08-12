import type { TFunction } from "i18next";
import { resolveDateLocale } from "./format";

/** Every image picker in the app - organization logo (create modal + org
 * settings), user avatar and opportunity banner - accepts the same three
 * formats at the same size ceiling. Each used to carry its own copy of both
 * constants and its own wording for them, which is how "JPEG" vs "JPG" and
 * "max. 2 MB" vs "max 2 MB" drifted apart across four surfaces (#1781).
 * Centralized so the hint, the `accept` attribute and the rejection messages
 * are all derived from these two values instead of restating them by hand. */
// Kept in sync by hand with the backend's own ceiling and allow-list
// (Application/Common/Storage/ImageUploadValidator.cs) - identical today, so the
// hint below never promises something the server then rejects.
//
// Changing this list means changing copy too: the formats are spelled out in
// `imageUpload.hint` and `imageUpload.wrongType` (both locales) and in
// `apiError.*.InvalidContentType`. Only the *size* ceiling is interpolated
// from here; the format names are not, so a new format added here and nowhere
// else leaves four translations quietly lying - the same shape of bug as
// #1781, one axis over.
export const IMAGE_UPLOAD_TYPES: readonly string[] = [
	"image/jpeg",
	"image/png",
	"image/webp",
];
export const MAX_IMAGE_UPLOAD_BYTES = 2 * 1024 * 1024;

/** Ready-made value for a file input's `accept` attribute - keeps the picker's
 * native filter and `validateImageUpload` below reading the same list, so a
 * format can never be offered in the dialog and then rejected on selection. */
export const IMAGE_UPLOAD_ACCEPT = IMAGE_UPLOAD_TYPES.join(",");

const KILOBYTE = 1024;
const MEGABYTE = 1024 * 1024;

/** Longest file name echoed back in an error, in characters. */
const MAX_FILE_NAME_CHARS = 40;

// Cached per locale/precision the same way format.ts caches its
// Intl.DateTimeFormat instances - getImageUploadHint below runs on every
// render of a form the user is actively typing in, and constructing a
// formatter is the expensive half of formatting one number.
const numberFormatters = new Map<string, Intl.NumberFormat>();

function getNumberFormatter(
	locale: string,
	maximumFractionDigits: number,
): Intl.NumberFormat {
	const cacheKey = `${locale}|${maximumFractionDigits}`;
	let formatter = numberFormatters.get(cacheKey);
	if (!formatter) {
		formatter = new Intl.NumberFormat(locale, { maximumFractionDigits });
		numberFormatters.set(cacheKey, formatter);
	}
	return formatter;
}

/** `lng` is i18n.language ("de"/"en"), not an Intl locale - resolved through
 * the same map the date formatters use so "4,2 MB" vs "4.2 MB" follows the
 * same regional variant as every other formatted number the viewer sees. */
export function formatFileSize(bytes: number, lng: string): string {
	const locale = resolveDateLocale(lng);
	if (bytes >= MEGABYTE)
		return `${getNumberFormatter(locale, 1).format(bytes / MEGABYTE)} MB`;
	if (bytes >= KILOBYTE)
		return `${getNumberFormatter(locale, 0).format(bytes / KILOBYTE)} KB`;
	return `${getNumberFormatter(locale, 0).format(bytes)} B`;
}

/** Keeps a rejected file identifiable in a one-line error without letting a
 * pathological name push the rest of the sentence out of the widget. The
 * extension is the part that usually explains *why* the file was rejected, so
 * it survives the trim rather than being cut off with the tail. */
export function truncateFileName(fileName: string): string {
	if (fileName.length <= MAX_FILE_NAME_CHARS) return fileName;

	const dotIndex = fileName.lastIndexOf(".");
	const extension = dotIndex > 0 ? fileName.slice(dotIndex) : "";
	// A name that is mostly "extension" has nothing worth preserving at the
	// end - trim the head instead of building a string that is all suffix.
	if (extension.length > 10)
		return `${fileName.slice(0, MAX_FILE_NAME_CHARS - 1)}…`;

	const head = fileName.slice(0, MAX_FILE_NAME_CHARS - extension.length - 1);
	return `${head}…${extension}`;
}

/** The grey "JPEG, PNG or WebP, max. 2 MB." helper text under every picker.
 * Reads the ceiling from `MAX_IMAGE_UPLOAD_BYTES` rather than spelling it out
 * in the translation, so raising the limit can't leave the hint promising the
 * old one. */
export function getImageUploadHint(t: TFunction, lng: string): string {
	return t("imageUpload.hint", {
		max: formatFileSize(MAX_IMAGE_UPLOAD_BYTES, lng),
	});
}

/**
 * Validates a picked file against the shared format/size rules, returning the
 * message to show or `null` when the file is fine.
 *
 * The two failure modes are deliberately separate messages that name the
 * offending file: the org-logo and avatar pickers used to answer both with
 * `t(...Hint)` - the very sentence already rendered in grey right above the
 * error - so a rejected `.txt` showed the same line twice and told the user
 * neither which rule it broke nor which file broke it (#1781).
 */
export function validateImageUpload(
	file: File,
	t: TFunction,
	lng: string,
): string | null {
	const fileName = truncateFileName(file.name);

	if (!IMAGE_UPLOAD_TYPES.includes(file.type))
		return t("imageUpload.wrongType", { fileName });

	if (file.size > MAX_IMAGE_UPLOAD_BYTES)
		return t("imageUpload.tooLarge", {
			fileName,
			size: formatFileSize(file.size, lng),
			max: formatFileSize(MAX_IMAGE_UPLOAD_BYTES, lng),
		});

	return null;
}
