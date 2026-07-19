import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { TimeSlotDetail } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { formatDateTime } from "../lib/format";
import { getApiErrorMessage } from "../lib/apiError";
import { textareaClass } from "../lib/formClasses";
import Dropdown from "./Dropdown";
import Modal from "./Modal";

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
			(ts) => ts.maxParticipants - ts.bookedCount > 0,
		);
		return availableSlots.length === 1 ? availableSlots[0].id : "";
	});
	const [message, setMessage] = useState("");
	const [submitting, setSubmitting] = useState(false);
	const [error, setError] = useState<string | null>(null);

	const isWaitlist = participationType === "Waitlist";

	async function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
		if (isWaitlist && timeSlots.length > 0 && !selectedTimeSlotId) {
			setError(t("signUp.selectTimeSlotRequired"));
			return;
		}
		setSubmitting(true);
		setError(null);

		try {
			await api.createEngagement(opportunityId, {
				type: isWaitlist ? "Waitlist" : "IndividualContact",
				timeSlotId:
					isWaitlist && selectedTimeSlotId ? selectedTimeSlotId : undefined,
				message: !isWaitlist ? message : undefined,
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
				{isWaitlist ? t("signUp.titleWaitlist") : t("signUp.titleInterest")}
			</h2>

			<form onSubmit={handleSubmit} className="space-y-4">
				{isWaitlist && (
					<div>
						<label
							htmlFor="sign-up-time-slot"
							className="mb-1 block text-sm font-medium text-gray-700"
						>
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
								className="mt-1 w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/30"
								options={timeSlots.map((ts) => {
									const spotsLeft = ts.maxParticipants - ts.bookedCount;
									const slotFull = spotsLeft <= 0;
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
											slotFull
												? t("signUp.slotFull")
												: t("signUp.spotsLeft", { count: spotsLeft })
										}`,
									};
								})}
							/>
						)}
					</div>
				)}

				{!isWaitlist && (
					<div>
						<label
							htmlFor="sign-up-message"
							className="mb-1 block text-sm font-medium text-gray-700"
						>
							{t("signUp.message")}
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

				{error && <p className="text-sm text-red-600">{error}</p>}

				<div className="flex justify-end gap-2">
					<button
						type="button"
						onClick={onClose}
						className="rounded-xl px-4 py-2 text-sm text-gray-600 transition-colors hover:bg-gray-100"
					>
						{t("signUp.cancel")}
					</button>
					<button
						type="submit"
						disabled={submitting || (isWaitlist && timeSlots.length === 0)}
						className="rounded-xl bg-brand-700 px-4 py-2 text-sm text-white transition-colors hover:bg-brand-800 disabled:opacity-50"
					>
						{submitting ? t("signUp.submitting") : t("signUp.submit")}
					</button>
				</div>
			</form>
		</Modal>
	);
}
