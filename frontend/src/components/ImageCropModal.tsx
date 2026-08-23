import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
	blobToFile,
	clampOffset,
	computeSourceRect,
	coverScale,
	cropImageToBlob,
	loadImage,
	recenterOffsetForScale,
} from "../lib/imageCrop";
import type { Offset } from "../lib/imageCrop";
import Modal from "./Modal";
import Button from "./Button";

const FRAME_MAX_WIDTH = 320;
const MAX_ZOOM = 4;
const KEYBOARD_PAN_STEP = 12;

interface ImageCropModalProps {
	file: File;

	aspectRatio: number;

	shape: "circle" | "rect";
	outputWidth: number;
	outputHeight: number;
	title: string;
	onCancel: () => void;
	onCropped: (file: File) => void;
}

export default function ImageCropModal({
	file,
	aspectRatio,
	shape,
	outputWidth,
	outputHeight,
	title,
	onCancel,
	onCropped,
}: ImageCropModalProps) {
	const { t } = useTranslation();
	const frameRef = useRef<HTMLButtonElement>(null);

	const [frameWidth, setFrameWidth] = useState(FRAME_MAX_WIDTH);
	const frameHeight = frameWidth / aspectRatio;

	const [image, setImage] = useState<HTMLImageElement | null>(null);
	const [loadError, setLoadError] = useState(false);
	const [applying, setApplying] = useState(false);
	const [zoom, setZoom] = useState(1);
	const [offset, setOffset] = useState<Offset>({ x: 0, y: 0 });
	const [panAnnouncement, setPanAnnouncement] = useState("");
	const dragState = useRef<{
		start: { x: number; y: number };
		offset: Offset;
	} | null>(null);

	useLayoutEffect(() => {
		const el = frameRef.current;
		if (!el) return;
		const measure = () => {
			const width = el.getBoundingClientRect().width;
			if (width > 0) setFrameWidth(Math.min(FRAME_MAX_WIDTH, width));
		};
		measure();
		const observer = new ResizeObserver(measure);
		observer.observe(el);
		return () => observer.disconnect();
	}, [image]);

	useEffect(() => {
		if (!image) return;
		setOffset((prev) => {
			const scale =
				coverScale(
					image.naturalWidth,
					image.naturalHeight,
					frameWidth,
					frameHeight,
				) * zoom;
			return clampOffset(
				prev,
				image.naturalWidth * scale,
				image.naturalHeight * scale,
				frameWidth,
				frameHeight,
			);
		});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [frameWidth, frameHeight]);

	useEffect(() => {
		let cancelled = false;
		let ownedUrl: string | null = null;
		void loadImage(file)
			.then(({ image: img, objectUrl }) => {
				if (cancelled) {
					URL.revokeObjectURL(objectUrl);
					return;
				}
				ownedUrl = objectUrl;
				const baseScale = coverScale(
					img.naturalWidth,
					img.naturalHeight,
					frameWidth,
					frameHeight,
				);
				const scaledWidth = img.naturalWidth * baseScale;
				const scaledHeight = img.naturalHeight * baseScale;
				setImage(img);
				setZoom(1);
				setOffset({
					x: (frameWidth - scaledWidth) / 2,
					y: (frameHeight - scaledHeight) / 2,
				});
			})
			.catch(() => {
				if (!cancelled) setLoadError(true);
			});
		return () => {
			cancelled = true;
			if (ownedUrl) URL.revokeObjectURL(ownedUrl);
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [file]);

	if (!image) {
		return (
			<Modal
				onClose={onCancel}
				labelledBy="image-crop-title"
				maxWidth="max-w-sm"
			>
				<h2 id="image-crop-title" className="text-lg font-semibold">
					{title}
				</h2>
				<p className="mt-4 text-sm text-gray-600">
					{loadError ? t("imageCrop.loadError") : t("imageCrop.loading")}
				</p>
				<div className="mt-4 flex justify-end">
					<Button variant="secondary" onClick={onCancel}>
						{t("imageCrop.cancel")}
					</Button>
				</div>
			</Modal>
		);
	}

	const baseScale = coverScale(
		image.naturalWidth,
		image.naturalHeight,
		frameWidth,
		frameHeight,
	);
	const scale = baseScale * zoom;
	const scaledWidth = image.naturalWidth * scale;
	const scaledHeight = image.naturalHeight * scale;

	function moveBy(dx: number, dy: number) {
		setOffset((prev) => {
			const next = clampOffset(
				{ x: prev.x + dx, y: prev.y + dy },
				scaledWidth,
				scaledHeight,
				frameWidth,
				frameHeight,
			);
			setPanAnnouncement(
				next.x === prev.x && next.y === prev.y
					? t("imageCrop.panBoundary")
					: "",
			);
			return next;
		});
	}

	function handlePointerDown(e: React.PointerEvent<HTMLButtonElement>) {
		e.currentTarget.setPointerCapture(e.pointerId);
		dragState.current = { start: { x: e.clientX, y: e.clientY }, offset };
	}

	function handlePointerMove(e: React.PointerEvent<HTMLButtonElement>) {
		if (!dragState.current) return;
		const dx = e.clientX - dragState.current.start.x;
		const dy = e.clientY - dragState.current.start.y;
		setOffset(
			clampOffset(
				{
					x: dragState.current.offset.x + dx,
					y: dragState.current.offset.y + dy,
				},
				scaledWidth,
				scaledHeight,
				frameWidth,
				frameHeight,
			),
		);
	}

	function handlePointerUp() {
		dragState.current = null;
	}

	function handleFrameKeyDown(e: React.KeyboardEvent<HTMLButtonElement>) {
		switch (e.key) {
			case "ArrowLeft":
				e.preventDefault();
				moveBy(KEYBOARD_PAN_STEP, 0);
				break;
			case "ArrowRight":
				e.preventDefault();
				moveBy(-KEYBOARD_PAN_STEP, 0);
				break;
			case "ArrowUp":
				e.preventDefault();
				moveBy(0, KEYBOARD_PAN_STEP);
				break;
			case "ArrowDown":
				e.preventDefault();
				moveBy(0, -KEYBOARD_PAN_STEP);
				break;
		}
	}

	function handleZoomChange(next: number) {
		if (!image) return;
		const newScale = baseScale * next;
		const recentered = recenterOffsetForScale(
			offset,
			scale,
			newScale,
			frameWidth,
			frameHeight,
		);
		setZoom(next);
		setOffset(
			clampOffset(
				recentered,
				image.naturalWidth * newScale,
				image.naturalHeight * newScale,
				frameWidth,
				frameHeight,
			),
		);
	}

	async function handleApply() {
		if (!image) return;
		setApplying(true);
		try {
			const source = computeSourceRect(offset, scale, frameWidth, frameHeight);
			const blob = await cropImageToBlob(
				image,
				source,
				outputWidth,
				outputHeight,
			);
			const ext = blob.type === "image/webp" ? "webp" : "png";
			onCropped(
				blobToFile(blob, `${file.name.replace(/\.[^.]+$/, "")}.${ext}`),
			);
		} finally {
			setApplying(false);
		}
	}

	return (
		<Modal onClose={onCancel} labelledBy="image-crop-title" maxWidth="max-w-sm">
			<h2 id="image-crop-title" className="text-lg font-semibold">
				{title}
			</h2>
			<p className="mt-1 text-xs text-gray-500">{t("imageCrop.dragHint")}</p>

			<button
				ref={frameRef}
				type="button"
				aria-label={t("imageCrop.frameLabel")}
				className="relative mt-4 block w-full max-w-80 touch-none overflow-hidden rounded-card border-0 bg-gray-100 p-0"
				style={{ aspectRatio }}
				onPointerDown={handlePointerDown}
				onPointerMove={handlePointerMove}
				onPointerUp={handlePointerUp}
				onPointerCancel={handlePointerUp}
				onKeyDown={handleFrameKeyDown}
			>
				<img
					src={image.src}
					alt=""
					draggable={false}
					className="absolute max-w-none select-none"
					style={{
						left: offset.x,
						top: offset.y,
						width: scaledWidth,
						height: scaledHeight,
					}}
				/>
				{shape === "circle" && (
					<div
						aria-hidden="true"
						className="pointer-events-none absolute rounded-full"
						style={{
							width: Math.min(frameWidth, frameHeight),
							height: Math.min(frameWidth, frameHeight),
							left: (frameWidth - Math.min(frameWidth, frameHeight)) / 2,
							top: (frameHeight - Math.min(frameWidth, frameHeight)) / 2,
							boxShadow: "0 0 0 9999px rgba(0,0,0,0.5)",
						}}
					/>
				)}
			</button>
			<div aria-live="polite" className="sr-only">
				{panAnnouncement}
			</div>

			<div className="mt-4">
				<label
					htmlFor="image-crop-zoom"
					className="block text-xs font-medium text-gray-600"
				>
					{t("imageCrop.zoomLabel")}
				</label>
				<input
					id="image-crop-zoom"
					type="range"
					min={1}
					max={MAX_ZOOM}
					step={0.01}
					value={zoom}
					onChange={(e) => handleZoomChange(Number(e.target.value))}
					className="mt-1 w-full accent-brand-700"
				/>
			</div>

			<div className="mt-4 flex justify-end gap-2">
				<Button variant="secondary" onClick={onCancel} disabled={applying}>
					{t("imageCrop.cancel")}
				</Button>
				<Button onClick={() => void handleApply()} disabled={applying}>
					{applying ? t("imageCrop.applying") : t("imageCrop.apply")}
				</Button>
			</div>
		</Modal>
	);
}
