import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import jsQR from "jsqr";
import { useApiClient } from "../hooks/useApiClient";
import { getApiErrorMessage } from "../lib/apiError";
import { inputSurfaceClass, labelClass } from "../lib/formClasses";
import Modal from "./Modal";
import Spinner from "./Spinner";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";

declare global {
	class BarcodeDetector {
		constructor(options?: { formats: string[] });
		detect(
			source: HTMLVideoElement | HTMLCanvasElement | ImageBitmap,
		): Promise<Array<{ rawValue: string; format: string }>>;
		static getSupportedFormats(): Promise<string[]>;
	}
}

const UUID_RE =
	/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const FALLBACK_CODE_LENGTH = 8;

// Native BarcodeDetector (Chromium only) is used when present since it's
// typically hardware-accelerated; everywhere else - WebKit/Safari (all iOS
// browsers, since they're WebKit shells) and Gecko/Firefox never implement
// it - this falls back to decoding video frames with jsQR (#2219).
async function detectRawValue(
	video: HTMLVideoElement,
	canvas: HTMLCanvasElement,
): Promise<string | null> {
	if (typeof BarcodeDetector !== "undefined") {
		const detector = new BarcodeDetector({ formats: ["qr_code"] });
		const barcodes = await detector.detect(video);
		return barcodes[0]?.rawValue ?? null;
	}

	const ctx = canvas.getContext("2d");
	if (!ctx) return null;
	canvas.width = video.videoWidth;
	canvas.height = video.videoHeight;
	ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
	const { data, width, height } = ctx.getImageData(
		0,
		0,
		canvas.width,
		canvas.height,
	);
	return (
		jsQR(data, width, height, { inversionAttempts: "dontInvert" })?.data ?? null
	);
}

interface QRScannerModalProps {
	opportunityId: string;
	onCheckedIn: (engagementId: string) => void;
	onClose: () => void;
}

export default function QRScannerModal({
	opportunityId,
	onCheckedIn,
	onClose,
}: QRScannerModalProps) {
	const api = useApiClient();
	const { t } = useTranslation();
	const videoRef = useRef<HTMLVideoElement>(null);
	const canvasRef = useRef<HTMLCanvasElement | null>(null);
	if (canvasRef.current === null) {
		canvasRef.current = document.createElement("canvas");
	}
	const streamRef = useRef<MediaStream | null>(null);
	const [supported, setSupported] = useState<boolean | null>(null);
	const [cameraError, setCameraError] = useState<string | null>(null);
	const [scanError, setScanError] = useState<string | null>(null);
	const [success, setSuccess] = useState(false);
	const [fallbackCode, setFallbackCode] = useState("");
	const [fallbackCodeSubmitting, setFallbackCodeSubmitting] = useState(false);
	const [fallbackCodeError, setFallbackCodeError] = useState<string | null>(
		null,
	);

	useEffect(() => {
		if (!success) return;
		requestAnimationFrame(() => {
			document
				.querySelector<HTMLButtonElement>(
					'[data-testid="qr-scanner-close-button"]',
				)
				?.focus();
		});
	}, [success]);

	useEffect(() => {
		setSupported(!!navigator.mediaDevices?.getUserMedia);
	}, []);

	useEffect(() => {
		if (supported !== true) return;
		let alive = true;
		navigator.mediaDevices
			.getUserMedia({ video: { facingMode: "environment" } })
			.then((stream) => {
				if (!alive) {
					stream.getTracks().forEach((tr) => tr.stop());
					return;
				}
				streamRef.current = stream;
				if (videoRef.current) videoRef.current.srcObject = stream;
			})
			.catch(() => {
				if (alive) setCameraError(t("checkIn.qrCameraError"));
			});
		return () => {
			alive = false;
			streamRef.current?.getTracks().forEach((tr) => tr.stop());
			streamRef.current = null;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [supported]);

	function completeCheckIn(engagementId: string) {
		streamRef.current?.getTracks().forEach((tr) => tr.stop());
		streamRef.current = null;
		setSuccess(true);
		onCheckedIn(engagementId);
	}

	useEffect(() => {
		if (supported !== true) return;
		let alive = true;
		let timer: ReturnType<typeof setTimeout> | null = null;

		const loop = async () => {
			if (!alive) return;
			const video = videoRef.current;
			const canvas = canvasRef.current;
			if (video && canvas && video.readyState >= video.HAVE_ENOUGH_DATA) {
				try {
					const raw = (await detectRawValue(video, canvas))?.trim();
					if (raw && UUID_RE.test(raw)) {
						alive = false;
						try {
							await api.checkInEngagement(raw);
							completeCheckIn(raw);
						} catch (err) {
							setScanError(
								getApiErrorMessage(err, t("checkIn.qrCheckInError")),
							);

							alive = true;
						}
					} else {
						setScanError(null);
					}
				} catch {
					// detection failure - retry on next tick
				}
			}
			if (alive) timer = setTimeout(() => void loop(), 500);
		};

		timer = setTimeout(() => void loop(), 1000);
		return () => {
			alive = false;
			if (timer !== null) clearTimeout(timer);
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [supported]);

	async function handleFallbackCodeSubmit(e: React.FormEvent) {
		e.preventDefault();
		setFallbackCodeSubmitting(true);
		setFallbackCodeError(null);
		try {
			const result = await api.checkInEngagementByCode(opportunityId, {
				code: fallbackCode,
			});
			completeCheckIn(result.id);
		} catch (err) {
			setFallbackCodeError(
				getApiErrorMessage(err, t("checkIn.qrCheckInError")),
			);
		} finally {
			setFallbackCodeSubmitting(false);
		}
	}

	return (
		<Modal onClose={onClose} labelledBy="qr-scanner-title" maxWidth="max-w-sm">
			<h2
				id="qr-scanner-title"
				className="mb-4 text-lg font-semibold text-gray-900"
			>
				{t("checkIn.qrScanTitle")}
			</h2>

			{supported === null && (
				<div className="flex items-center justify-center py-6">
					<Spinner label={t("opportunities.loading")} size="sm" />
				</div>
			)}

			{supported === false && (
				<ErrorBanner message={t("checkIn.qrNotSupported")} />
			)}

			{supported === true && !success && (
				<>
					{cameraError ? (
						<ErrorBanner message={cameraError} />
					) : (
						<div className="relative overflow-hidden rounded-lg bg-black">
							<video
								ref={videoRef}
								autoPlay
								muted
								playsInline
								className="h-64 w-full object-cover"
								aria-label={t("checkIn.qrVideoLabel")}
							/>
							<div
								aria-hidden="true"
								className="pointer-events-none absolute inset-0 flex items-center justify-center"
							>
								<div className="h-40 w-40 rounded-xl border-2 border-white opacity-60" />
							</div>
						</div>
					)}
					{scanError && <ErrorBanner message={scanError} className="mt-2" />}
					{!cameraError && !scanError && (
						<p className="mt-3 text-sm text-gray-500">
							{t("checkIn.qrScanHint")}
						</p>
					)}
				</>
			)}

			{!success && (
				<div className="mt-5 border-t border-gray-200 pt-4">
					<label htmlFor="qr-fallback-code-input" className={labelClass}>
						{t("checkIn.qrFallbackCodeInputLabel")}
					</label>
					<form
						onSubmit={(e) => void handleFallbackCodeSubmit(e)}
						className="mt-1 flex gap-2"
					>
						<input
							id="qr-fallback-code-input"
							type="text"
							inputMode="text"
							autoComplete="off"
							data-skip-initial-focus
							maxLength={FALLBACK_CODE_LENGTH}
							value={fallbackCode}
							onChange={(e) =>
								setFallbackCode(
									e.target.value.replace(/[^0-9a-fA-F]/g, "").toLowerCase(),
								)
							}
							placeholder={t("checkIn.qrFallbackCodePlaceholder")}
							className={`${inputSurfaceClass} flex-1 font-mono`}
						/>
						<Button
							type="submit"
							disabled={
								fallbackCodeSubmitting ||
								fallbackCode.length !== FALLBACK_CODE_LENGTH
							}
						>
							{fallbackCodeSubmitting
								? t("checkIn.submitting")
								: t("checkIn.qrFallbackCodeSubmit")}
						</Button>
					</form>
					{fallbackCodeError && (
						<ErrorBanner message={fallbackCodeError} className="mt-2" />
					)}
				</div>
			)}

			<p
				role="status"
				className={success ? "text-sm font-medium text-green-700" : "sr-only"}
			>
				{success ? t("checkIn.qrSuccess") : ""}
			</p>

			<Button
				type="button"
				variant="secondary"
				onClick={onClose}
				fullWidth
				className="mt-5"
				data-testid="qr-scanner-close-button"
			>
				{t("checkIn.close")}
			</Button>
		</Modal>
	);
}
