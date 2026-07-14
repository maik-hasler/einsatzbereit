import { useEffect, useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunityDetails } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";

interface CheckInModalProps {
	engagementId: string;
	opportunityId: string;
	onCheckedIn: () => void;
	onClose: () => void;
}

export default function CheckInModal({
	engagementId,
	opportunityId,
	onCheckedIn,
	onClose,
}: CheckInModalProps) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [details, setDetails] = useState<VolunteerOpportunityDetails | null>(
		null,
	);
	const [loadError, setLoadError] = useState(false);
	const [pin, setPin] = useState("");
	const [submitting, setSubmitting] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [success, setSuccess] = useState(false);

	useEffect(() => {
		api
			.getVolunteerOpportunityDetails(opportunityId)
			.then(setDetails)
			.catch(() => setLoadError(true));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [opportunityId]);

	useEffect(() => {
		const handler = (e: KeyboardEvent) => {
			if (e.key === "Escape") onClose();
		};
		document.addEventListener("keydown", handler);
		return () => document.removeEventListener("keydown", handler);
	}, [onClose]);

	async function handlePinSubmit(e: React.FormEvent) {
		e.preventDefault();
		setSubmitting(true);
		setError(null);
		try {
			await api.checkInWithPin(engagementId, { pin });
			setSuccess(true);
			onCheckedIn();
		} catch {
			setError(t("checkIn.invalidPin"));
		} finally {
			setSubmitting(false);
		}
	}

	const checkInMethod = details?.checkInMethod;

	return (
		<div className="fixed inset-0 z-[2000] flex items-center justify-center p-3 sm:p-4">
			<button
				type="button"
				className="absolute inset-0 bg-black/50"
				onClick={onClose}
				tabIndex={-1}
				aria-hidden="true"
			/>
			<div
				role="dialog"
				aria-modal="true"
				aria-labelledby="checkin-title"
				className="relative z-10 w-full max-w-sm rounded-xl bg-white p-6 shadow-xl"
			>
				<h2
					id="checkin-title"
					className="mb-4 text-lg font-semibold text-gray-900"
				>
					{t("checkIn.title")}
				</h2>

				{!details && !loadError && (
					<p className="text-sm text-gray-500">{t("opportunities.loading")}</p>
				)}

				{loadError && (
					<p className="text-sm text-red-600" role="alert">
						{t("checkIn.loadError")}
					</p>
				)}

				{details && !success && checkInMethod === "QRCode" && (
					<div className="flex flex-col items-center gap-4">
						<p className="text-center text-sm text-gray-600">
							{t("checkIn.qrInstruction")}
						</p>
						<QRCodeSVG value={engagementId} size={200} />
						<p className="break-all font-mono text-xs text-gray-400">
							{engagementId}
						</p>
					</div>
				)}

				{details && !success && checkInMethod === "PINCode" && (
					<div>
						<p className="mb-4 text-sm text-gray-600">
							{t("checkIn.pinInstruction")}
						</p>
						<form
							onSubmit={(e) => void handlePinSubmit(e)}
							className="space-y-3"
						>
							<div>
								<label
									htmlFor="pin-input"
									className="block text-sm font-medium text-gray-700"
								>
									{t("checkIn.pinLabel")}
								</label>
								<input
									id="pin-input"
									type="text"
									inputMode="numeric"
									pattern="[0-9]*"
									maxLength={6}
									value={pin}
									onChange={(e) => setPin(e.target.value.replace(/\D/g, ""))}
									placeholder={t("checkIn.pinPlaceholder")}
									className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
								/>
							</div>
							{error && <p className="text-sm text-red-600">{error}</p>}
							<button
								type="submit"
								disabled={submitting || pin.length < 4}
								className="w-full rounded-md bg-brand-700 px-4 py-2 text-sm font-medium text-white hover:bg-brand-800 disabled:opacity-50"
							>
								{submitting ? t("checkIn.submitting") : t("checkIn.submitPin")}
							</button>
						</form>
					</div>
				)}

				{details && !success && checkInMethod === "Manual" && (
					<p className="text-sm text-gray-600">
						{t("checkIn.manualInstruction")}
					</p>
				)}

				{details && !success && checkInMethod === "None" && (
					<p className="text-sm text-gray-600">
						{t("checkIn.noneInstruction")}
					</p>
				)}

				{success && (
					<p className="text-sm font-medium text-green-700">
						{t("checkIn.success")}
					</p>
				)}

				<button
					type="button"
					onClick={onClose}
					className="mt-5 w-full rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
				>
					{t("checkIn.close")}
				</button>
			</div>
		</div>
	);
}
