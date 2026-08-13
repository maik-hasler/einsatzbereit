import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { TimeSlotDetail } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { computeSpotsLeft, formatDateTime, isSlotFull } from "../lib/format";
import { getApiErrorMessage } from "../lib/apiError";
import { labelClass, textareaClass } from "../lib/formClasses";
import Dropdown from "./Dropdown";
import Modal from "./Modal";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";
import { RequiredFieldsLegend, RequiredMark } from "./RequiredMark";

interface Props {
	opportunityId: string;
	participationType: string;
	timeSlots: TimeSlotDetail[];
	onClose: () => void;
	onSuccess: () => void;
}

export default function SignUpModal({
	opportunityId,
	participationType,
	timeSlots,
	onClose,
	onSuccess,
}: Props) {
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	const [selectedTimeSlotId, setSelectedTimeSlotId] = useState<string>(() => {
		const availableSlots = timeSlots.filter(
			(ts) => !isSlotFull(ts.maxParticipants, ts.bookedCount),
		);
		return availableSlots.length === 1 ? availableSlots[0].id : "";
	});
	const [message, setMessage] = useState("");
	const [submitting, setSubmitting] = useState(false);
	const [error, setError] = useState<string | null>(null);

	const isScheduledSlots = participationType === "ScheduledSlots";

	async function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
		if (isScheduledSlots && timeSlots.length > 0 && !selectedTimeSlotId) {
			setError(t("signUp.selectTimeSlotRequired"));
			return;
		}
		setSubmitting(true);
		setError(null);

		try {
			await api.createEngagement(opportunityId, {
				type: isScheduledSlots ? "ScheduledSlots" : "IndividualContact",
				timeSlotId:
					isScheduledSlots && selectedTimeSlotId
						? selectedTimeSlotId
						: undefined,
				message: !isScheduledSlots ? message : undefined,
			});
			onSuccess();
			onClose();
		} catch (err) {
			setError(getApiErrorMessage(err, t("signUp.unknownError")));
		} finally {
			setSubmitting(false);
		}
	}

	return (
		<Modal
			onClose={onClose}
			labelledBy="sign-up-dialog-title"
			maxWidth="max-w-md"
		>
			<h2 id="sign-up-dialog-title" className="mb-4 text-lg font-semibold">
				{isScheduledSlots
					? t("signUp.titleWaitlist")
					: t("signUp.titleInterest")}
			</h2>

			<form onSubmit={handleSubmit} className="space-y-4">
				{isScheduledSlots && (
					<div>
						{/* Visually hidden, not removed - the dialog title just above
						("Select a slot") already conveys this on screen, so showing it
						again here read as a duplicated label (#987); the dropdown still
						needs its own accessible name for screen reader users landing on
						it directly. */}
						<label htmlFor="sign-up-time-slot" className="sr-only">
							{t("signUp.selectTimeSlot")}
						</label>
						{timeSlots.length === 0 ? (
							<p className="text-sm text-gray-500">{t("signUp.noTimeSlots")}</p>
						) : (
							<Dropdown
								id="sign-up-time-slot"
								value={selectedTimeSlotId}
								onChange={setSelectedTimeSlotId}
								placeholder={t("signUp.selectPlaceholder")}
								className="mt-1 w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400"
								options={timeSlots.map((ts) => {
									const spotsLeft = computeSpotsLeft(
										ts.maxParticipants,
										ts.bookedCount,
									);
									const slotFull = isSlotFull(
										ts.maxParticipants,
										ts.bookedCount,
									);
									return {
										value: ts.id,
										disabled: slotFull,
										label: `${formatDateTime(
											ts.startDateTime as unknown as string,
											i18n.language,
										)} - ${formatDateTime(
											ts.endDateTime as unknown as string,
											i18n.language,
										)} ${
											spotsLeft === null
												? t("opportunities.unlimitedSpots")
												: slotFull
													? t("opportunities.full")
													: t("opportunities.spotsLeft", { count: spotsLeft })
										}`,
									};
								})}
							/>
						)}
					</div>
				)}

				{!isScheduledSlots && (
					<div>
						{/* Scoped to this branch: the slot-picker variant has no required
						field, so its legend would explain an absent asterisk. */}
						<RequiredFieldsLegend className="mb-2" />
						<label htmlFor="sign-up-message" className={`mb-1 ${labelClass}`}>
							{t("signUp.message")}
							<RequiredMark />
						</label>
						<textarea
							id="sign-up-message"
							value={message}
							onChange={(e) => setMessage(e.target.value)}
							required
							rows={4}
							placeholder={t("signUp.messagePlaceholder")}
							className={textareaClass}
						/>
					</div>
				)}

				{error && <ErrorBanner message={error} />}

				<div className="flex justify-end gap-2">
					<Button type="button" variant="secondary" onClick={onClose}>
						{t("signUp.cancel")}
					</Button>
					<Button
						type="submit"
						disabled={
							submitting || (isScheduledSlots && timeSlots.length === 0)
						}
					>
						{submitting
							? t("signUp.submitting")
							: isScheduledSlots
								? t("signUp.submitWaitlist")
								: t("signUp.submitInterest")}
					</Button>
				</div>
			</form>
		</Modal>
	);
}
