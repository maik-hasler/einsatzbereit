export interface Offset {
	x: number;
	y: number;
}

export interface SourceRect {
	sx: number;
	sy: number;
	sw: number;
	sh: number;
}

// The scale at which an image of (naturalWidth, naturalHeight) fully covers a
// frame of (frameWidth, frameHeight) with no empty gaps at any edge - the
// same "cover" behavior CSS object-fit: cover gives, but computed explicitly
// so it can be combined with an adjustable zoom on top.
export function coverScale(
	naturalWidth: number,
	naturalHeight: number,
	frameWidth: number,
	frameHeight: number,
): number {
	return Math.max(frameWidth / naturalWidth, frameHeight / naturalHeight);
}

// Clamps the image's top-left offset (relative to the frame's top-left) so
// the scaled image always fully covers the frame - dragging or zooming can
// never open a gap at an edge.
export function clampOffset(
	offset: Offset,
	scaledWidth: number,
	scaledHeight: number,
	frameWidth: number,
	frameHeight: number,
): Offset {
	const minX = frameWidth - scaledWidth;
	const minY = frameHeight - scaledHeight;
	return {
		x: Math.min(0, Math.max(minX, offset.x)),
		y: Math.min(0, Math.max(minY, offset.y)),
	};
}

// Recomputes the offset when the scale changes (zoom slider) so the point
// currently at the frame's center stays fixed, rather than the image
// jumping to a new position. Caller still needs to clamp the result.
export function recenterOffsetForScale(
	offset: Offset,
	oldScale: number,
	newScale: number,
	frameWidth: number,
	frameHeight: number,
): Offset {
	const centerImgX = (frameWidth / 2 - offset.x) / oldScale;
	const centerImgY = (frameHeight / 2 - offset.y) / oldScale;
	return {
		x: frameWidth / 2 - centerImgX * newScale,
		y: frameHeight / 2 - centerImgY * newScale,
	};
}

// The natural-pixel-space rectangle of the source image currently visible
// inside the frame, given the image is rendered at `scale` and positioned at
// `offset` (top-left, relative to the frame's top-left).
export function computeSourceRect(
	offset: Offset,
	scale: number,
	frameWidth: number,
	frameHeight: number,
): SourceRect {
	return {
		sx: -offset.x / scale,
		sy: -offset.y / scale,
		sw: frameWidth / scale,
		sh: frameHeight / scale,
	};
}

export interface LoadedImage {
	image: HTMLImageElement;
	// Caller owns this and must URL.revokeObjectURL it once the image is no
	// longer displayed - `image.src` is reused as the crop preview's <img>
	// src, so revoking too early (e.g. right after decode) can break that
	// second consumer of the same URL.
	objectUrl: string;
}

export function loadImage(file: File): Promise<LoadedImage> {
	return new Promise((resolve, reject) => {
		const objectUrl = URL.createObjectURL(file);
		const img = new Image();
		img.onload = () => resolve({ image: img, objectUrl });
		img.onerror = () => {
			URL.revokeObjectURL(objectUrl);
			reject(new Error("Failed to load image"));
		};
		img.src = objectUrl;
	});
}

// Draws the given source rect from `img` onto an offscreen canvas sized
// (outputWidth, outputHeight) and re-encodes it as WebP - this is what turns
// a multi-MB original into a small, appropriately-sized upload (einsatzbereit#1380).
export function cropImageToBlob(
	img: HTMLImageElement,
	source: SourceRect,
	outputWidth: number,
	outputHeight: number,
	quality = 0.85,
): Promise<Blob> {
	const canvas = document.createElement("canvas");
	canvas.width = outputWidth;
	canvas.height = outputHeight;
	const ctx = canvas.getContext("2d");
	if (!ctx) throw new Error("Canvas 2D context unavailable");

	ctx.drawImage(
		img,
		source.sx,
		source.sy,
		source.sw,
		source.sh,
		0,
		0,
		outputWidth,
		outputHeight,
	);

	return new Promise((resolve, reject) => {
		canvas.toBlob(
			(blob) => {
				if (blob) resolve(blob);
				else reject(new Error("Canvas toBlob failed"));
			},
			"image/webp",
			quality,
		);
	});
}

export function blobToFile(blob: Blob, fileName: string): File {
	return new File([blob], fileName, { type: blob.type });
}
