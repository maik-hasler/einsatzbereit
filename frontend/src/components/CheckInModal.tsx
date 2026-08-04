import { useEffect, useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunityDetails } from "../client/api-client";
import { getApiErrorMessage } from "../lib/apiError";
import { inputClass, labelClass } from "../lib/formClasses";
import { useApiClient } from "../hooks/useApiClient";
import Modal from "./Modal";
import Spinner from "./Spinner";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";

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

	// The PIN form (including whatever field was focused when it was
	// submitted) unmounts once `success` flips true - move focus deliberately
	// to the remaining "Done" button instead of letting it drop to <body>.
	// Queried by data-testid rather than a ref: Button.tsx doesn't forward
	// refs, and this is the only place in the app that would need it to.
	useEffect(() => {
		if (!success) return;
		requestAnimationFrame(() => {
			document
				.querySelector<HTMLButtonElement>(
					'[data-testid="checkin-close-button"]',
				)
				?.focus();
		});
	}, [success]);

	useEffect(() => {
		api
			.getVolunteerOpportunityDetails(opportunityId)
			.then(setDetails)
			.catch(() => setLoadError(true));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [opportunityId]);

	async function handlePinSubmit(e: React.FormEvent) {
		e.preventDefault();
		setSubmitting(true);
		setError(null);
		try {
			await api.checkInWithPin(engagementId, { pin });
			setSuccess(true);
			onCheckedIn();
		} catch (e) {
			setError(getApiErrorMessage(e, t("checkIn.invalidPin")));
		} finally {
			setSubmitting(false);
		}
	}

	const checkInMethod = details?.checkInMethod;

	return (
		<Modal onClose={onClose} labelledBy="checkin-title" maxWidth="max-w-sm">
			<h2
				id="checkin-title"
				className="mb-4 text-lg font-semibold text-gray-900"
			>
				{t("checkIn.title")}
			</h2>

			{!details && !loadError && (
				<div className="flex items-center justify-center py-6">
					<Spinner label={t("opportunities.loading")} size="sm" />
				</div>
			)}

			{loadError && <ErrorBanner message={t("checkIn.loadError")} />}

			{details && !success && checkInMethod === "QRCode" && (
				<div className="flex flex-col items-center gap-4">
					<p className="text-center text-sm text-gray-600">
						{t("checkIn.qrInstruction")}
					</p>
					<QRCodeSVG value={engagementId} size={200} />
					<p className="font-mono text-xs break-all text-gray-500">
						{engagementId}
					</p>
				</div>
			)}

			{details && !success && checkInMethod === "PINCode" && (
				<div>
					<p className="mb-4 text-sm text-gray-600">
						{t("checkIn.pinInstruction")}
					</p>
					<form onSubmit={(e) => void handlePinSubmit(e)} className="space-y-3">
						<div>
							<label htmlFor="pin-input" className={labelClass}>
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
								className={inputClass}
							/>
						</div>
						{error && <ErrorBanner message={error} />}
						<Button
							type="submit"
							disabled={submitting || pin.length < 4}
							fullWidth
						>
							{submitting ? t("checkIn.submitting") : t("checkIn.submitPin")}
						</Button>
					</form>
				</div>
			)}

			{details && !success && checkInMethod === "Manual" && (
				<p className="text-sm text-gray-600">
					{t("checkIn.manualInstruction")}
				</p>
			)}

			{details &&
				!success &&
				checkInMethod === "None" &&
				details.participationType === "ScheduledSlots" && (
					<p className="text-sm text-gray-600">
						{t("checkIn.noneInstruction")}
					</p>
				)}

			{details &&
				!success &&
				checkInMethod === "None" &&
				details.participationType !== "ScheduledSlots" && (
					<p className="text-sm text-gray-600">
						{t("checkIn.noneIndividualInstruction")}
					</p>
				)}

			{/* Always mounted (not conditional on `success`) so the live region is
			registered before it ever gets content - a screen reader can miss a
			role="status" node that's inserted into the DOM already populated,
			same reasoning as the notification bell's live region (see
			NotificationDropdown.tsx). */}
			<p
				role="status"
				className={success ? "text-sm font-medium text-green-700" : "sr-only"}
			>
				{success ? t("checkIn.success") : ""}
			</p>

			<Button
				type="button"
				variant="secondary"
				onClick={onClose}
				fullWidth
				className="mt-5"
				data-testid="checkin-close-button"
			>
				{t("checkIn.close")}
			</Button>
		</Modal>
	);
}
