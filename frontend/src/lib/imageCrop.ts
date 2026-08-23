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

export function coverScale(
	naturalWidth: number,
	naturalHeight: number,
	frameWidth: number,
	frameHeight: number,
): number {
	return Math.max(frameWidth / naturalWidth, frameHeight / naturalHeight);
}

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
