import { describe, it, expect } from "vitest";
// Vite's own asset imports, not `node:fs` - this project has no @types/node,
// and these are typed by `vite/client` (see vite-env.d.ts). `?raw` gives the
// HTML as a string; `?inline` forces the PNG to a base64 data URI regardless
// of size, which `atob` turns back into bytes.
import indexHtml from "../../index.html?raw";
import ogImageDataUri from "../../public/og-image.png?inline";

/**
 * `SocialPreviewImageTests`, moved down in #2148 wave 13. Remaining
 * inventory: #2159.
 *
 * The E2E version booted a browser, an Aspire stack and a frontend to read two
 * hardcoded strings out of a fully static `index.html` - and despite its name
 * it never opened the image, so a declaration that disagreed with the actual
 * file would have passed. Here both are build inputs, so the test can do what
 * it says: parse the declared dimensions and compare them against the PNG's
 * own header.
 */
function metaContent(property: string): string | null {
	const match = new RegExp(
		`<meta\\s+property="${property}"\\s+content="([^"]*)"`,
	).exec(indexHtml);
	return match ? match[1] : null;
}

/** The bytes behind a `data:...;base64,...` URI. */
function bytesOf(dataUri: string): Uint8Array {
	const binary = atob(dataUri.slice(dataUri.indexOf(",") + 1));
	return Uint8Array.from(binary, (char) => char.charCodeAt(0));
}

/**
 * Reads width and height out of a PNG's IHDR chunk - bytes 16-23, big-endian,
 * immediately after the 8-byte signature and the chunk's length and type. No
 * decoder needed, and no dependency to add for a header read.
 */
function pngSize(bytes: Uint8Array): { width: number; height: number } {
	expect(String.fromCharCode(...bytes.subarray(1, 4))).toBe("PNG");
	const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
	return { width: view.getUint32(16), height: view.getUint32(20) };
}

describe("the social preview image", () => {
	it("declares dimensions matching the actual file", () => {
		const declaredWidth = metaContent("og:image:width");
		const declaredHeight = metaContent("og:image:height");
		expect(declaredWidth).not.toBeNull();
		expect(declaredHeight).not.toBeNull();

		const actual = pngSize(bytesOf(ogImageDataUri));

		expect(actual.width).toBe(Number(declaredWidth));
		expect(actual.height).toBe(Number(declaredHeight));
	});

	it("uses the 1.91:1 ratio the major platforms crop to", () => {
		// 1200x630 is what Facebook, LinkedIn and X all render uncropped;
		// anything else gets centre-cropped, usually through the logo.
		const actual = pngSize(bytesOf(ogImageDataUri));

		expect(actual.width).toBe(1200);
		expect(actual.height).toBe(630);
		expect(metaContent("og:image:type")).toBe("image/png");
		// And the tag points at the file this test just measured.
		expect(metaContent("og:image")).toMatch(/\/og-image\.png$/);
	});
});
