import { describe, it, expect } from "vitest";
import indexHtml from "../../index.html?raw";
import ogImageDataUri from "../../public/og-image.png?inline";

function metaContent(property: string): string | null {
	const match = new RegExp(
		`<meta\\s+property="${property}"\\s+content="([^"]*)"`,
	).exec(indexHtml);
	return match ? match[1] : null;
}

function bytesOf(dataUri: string): Uint8Array {
	const binary = atob(dataUri.slice(dataUri.indexOf(",") + 1));
	return Uint8Array.from(binary, (char) => char.charCodeAt(0));
}

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
		const actual = pngSize(bytesOf(ogImageDataUri));

		expect(actual.width).toBe(1200);
		expect(actual.height).toBe(630);
		expect(metaContent("og:image:type")).toBe("image/png");
		expect(metaContent("og:image")).toMatch(/\/og-image\.png$/);
	});
});
