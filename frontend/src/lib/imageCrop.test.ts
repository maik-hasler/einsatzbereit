import { describe, it, expect } from "vitest";
import {
	clampOffset,
	computeSourceRect,
	coverScale,
	recenterOffsetForScale,
} from "./imageCrop";

describe("coverScale", () => {
	it("picks the width ratio when the image is relatively taller than the frame", () => {
		// 1000x2000 image into a 200x200 frame - height ratio (0.1) would leave
		// gaps on the sides, so the wider width ratio (0.2) must win.
		expect(coverScale(1000, 2000, 200, 200)).toBe(0.2);
	});

	it("picks the height ratio when the image is relatively wider than the frame", () => {
		expect(coverScale(2000, 1000, 200, 200)).toBe(0.2);
	});

	it("returns 1 for an image that already exactly matches the frame", () => {
		expect(coverScale(320, 320, 320, 320)).toBe(1);
	});
});

describe("clampOffset", () => {
	it("leaves an offset unchanged when it is already within bounds", () => {
		// 400x400 scaled image in a 320x320 frame: valid x/y range is [-80, 0].
		expect(clampOffset({ x: -40, y: -20 }, 400, 400, 320, 320)).toEqual({
			x: -40,
			y: -20,
		});
	});

	it("pulls a positive offset back to 0 so no gap opens on the left/top", () => {
		expect(clampOffset({ x: 50, y: 50 }, 400, 400, 320, 320)).toEqual({
			x: 0,
			y: 0,
		});
	});

	it("pulls an offset that scrolled too far back to the minimum bound", () => {
		expect(clampOffset({ x: -500, y: -500 }, 400, 400, 320, 320)).toEqual({
			x: -80,
			y: -80,
		});
	});

	it("pins both axes to 0 when the scaled image exactly fills the frame", () => {
		expect(clampOffset({ x: -5, y: 5 }, 320, 320, 320, 320)).toEqual({
			x: 0,
			y: 0,
		});
	});
});

describe("recenterOffsetForScale", () => {
	it("keeps the frame's center point fixed on the image when zooming in", () => {
		// 320x320 frame, image centered at scale 1 (offset -40,-40 for a 400x400
		// scaled image) - the point at natural-space center of the visible
		// region should still be at the frame's center after zooming to scale 2.
		const offset = { x: -40, y: -40 };
		const recentered = recenterOffsetForScale(offset, 1, 2, 320, 320);

		// centerImgX/Y at scale 1 was (160 - -40) / 1 = 200; at scale 2 the
		// offset needed to keep that point at the frame center is 160 - 200*2.
		expect(recentered).toEqual({ x: 160 - 200 * 2, y: 160 - 200 * 2 });
	});

	it("is a no-op when the scale doesn't change", () => {
		const offset = { x: -30, y: -70 };
		expect(recenterOffsetForScale(offset, 1.5, 1.5, 320, 320)).toEqual(offset);
	});
});

describe("computeSourceRect", () => {
	it("maps a centered offset at cover scale to the full natural image extent on the wider axis", () => {
		// 1000x2000 natural image, cover scale into a 200x200 frame is 0.2
		// (width-constrained), centered vertically: scaledH = 2000*0.2 = 400,
		// offset.y = (200-400)/2 = -100.
		const scale = coverScale(1000, 2000, 200, 200);
		const offset = { x: 0, y: -100 };

		const rect = computeSourceRect(offset, scale, 200, 200);

		expect(rect.sx).toBeCloseTo(0);
		expect(rect.sw).toBeCloseTo(1000);
		expect(rect.sy).toBeCloseTo(500);
		expect(rect.sh).toBeCloseTo(1000);
	});

	it("shrinks the visible source rect as scale increases (zooming in)", () => {
		const frameSize = 320;
		const base = computeSourceRect({ x: 0, y: 0 }, 1, frameSize, frameSize);
		const zoomed = computeSourceRect({ x: 0, y: 0 }, 2, frameSize, frameSize);

		expect(zoomed.sw).toBeCloseTo(base.sw / 2);
		expect(zoomed.sh).toBeCloseTo(base.sh / 2);
	});
});
