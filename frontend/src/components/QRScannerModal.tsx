import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../hooks/useApiClient";
import { getApiErrorMessage } from "../lib/apiError";
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

interface QRScannerModalProps {
	onCheckedIn: (engagementId: string) => void;
	onClose: () => void;
}

export default function QRScannerModal({
	onCheckedIn,
	onClose,
}: QRScannerModalProps) {
	const api = useApiClient();
	const { t } = useTranslation();
	const videoRef = useRef<HTMLVideoElement>(null);
	const streamRef = useRef<MediaStream | null>(null);
	const [supported, setSupported] = useState<boolean | null>(null);
	const [cameraError, setCameraError] = useState<string | null>(null);
	const [scanError, setScanError] = useState<string | null>(null);
	const [success, setSuccess] = useState(false);

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
		const ok =
			typeof BarcodeDetector !== "undefined" &&
			!!navigator.mediaDevices?.getUserMedia;
		setSupported(ok);
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

	useEffect(() => {
		if (supported !== true) return;
		let alive = true;
		let timer: ReturnType<typeof setTimeout> | null = null;

		const loop = async () => {
			if (!alive) return;
			const video = videoRef.current;
			if (video && video.readyState >= video.HAVE_ENOUGH_DATA) {
				try {
					const detector = new BarcodeDetector({ formats: ["qr_code"] });
					const barcodes = await detector.detect(video);
					let matched = false;
					for (const barcode of barcodes) {
						const raw = barcode.rawValue.trim();
						if (!UUID_RE.test(raw)) continue;

						matched = true;
						alive = false;
						try {
							await api.checkInEngagement(raw);
							streamRef.current?.getTracks().forEach((tr) => tr.stop());
							streamRef.current = null;
							setSuccess(true);
							onCheckedIn(raw);
						} catch (err) {
							setScanError(
								getApiErrorMessage(err, t("checkIn.qrCheckInError")),
							);

							alive = true;
						}
						break;
					}
					if (!matched) setScanError(null);
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
