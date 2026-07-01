import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { TimeSlotDetail } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { formatDateTime } from "../lib/format";

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
	const [selectedTimeSlotId, setSelectedTimeSlotId] = useState<string>("");
	const [message, setMessage] = useState("");
	const [submitting, setSubmitting] = useState(false);
	const [error, setError] = useState<string | null>(null);

	const isWaitlist = participationType === "Waitlist";

	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			if (e.key === "Escape") onClose();
		}
		document.addEventListener("keydown", handleKeyDown);
		return () => document.removeEventListener("keydown", handleKeyDown);
	}, [onClose]);

	async function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
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
			setError(err instanceof Error ? err.message : t("signUp.unknownError"));
		} finally {
			setSubmitting(false);
		}
	}

	return (
		<div className="fixed inset-0 z-[2000] flex items-center justify-center">
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
				aria-labelledby="sign-up-dialog-title"
				className="relative z-10 w-full max-w-md rounded-xl bg-white p-6 shadow-xl"
			>
				<h2 id="sign-up-dialog-title" className="mb-4 text-lg font-semibold">
					{isWaitlist ? t("signUp.titleWaitlist") : t("signUp.titleInterest")}
				</h2>

				<form onSubmit={handleSubmit} className="space-y-4">
					{isWaitlist && (
						<div>
							<label className="mb-1 block text-sm font-medium text-gray-700">
								{t("signUp.selectTimeSlot")}
							</label>
							{timeSlots.length === 0 ? (
								<p className="text-sm text-gray-500">
									{t("signUp.noTimeSlots")}
								</p>
							) : (
								<select
									value={selectedTimeSlotId}
									onChange={(e) => setSelectedTimeSlotId(e.target.value)}
									required
									className="w-full rounded border px-3 py-2 text-sm"
								>
									<option value="">{t("signUp.selectPlaceholder")}</option>
									{timeSlots.map((ts) => {
										const spotsLeft = ts.maxParticipants - ts.bookedCount;
										const slotFull = spotsLeft <= 0;
										return (
											<option key={ts.id} value={ts.id} disabled={slotFull}>
												{formatDateTime(
													ts.startDateTime as unknown as string,
													i18n.language,
												)}{" "}
												-{" "}
												{formatDateTime(
													ts.endDateTime as unknown as string,
													i18n.language,
												)}{" "}
												{slotFull
													? t("signUp.slotFull")
													: t("signUp.spotsLeft", {
															count: spotsLeft,
														})}
											</option>
										);
									})}
								</select>
							)}
						</div>
					)}

					{!isWaitlist && (
						<div>
							<label className="mb-1 block text-sm font-medium text-gray-700">
								{t("signUp.message")}
							</label>
							<textarea
								value={message}
								onChange={(e) => setMessage(e.target.value)}
								required
								rows={4}
								placeholder={t("signUp.messagePlaceholder")}
								className="w-full rounded border px-3 py-2 text-sm"
							/>
						</div>
					)}

					{error && <p className="text-sm text-red-600">{error}</p>}

					<div className="flex justify-end gap-2">
						<button
							type="button"
							onClick={onClose}
							className="rounded px-4 py-2 text-sm text-gray-600 hover:bg-gray-100"
						>
							{t("signUp.cancel")}
						</button>
						<button
							type="submit"
							disabled={submitting || (isWaitlist && timeSlots.length === 0)}
							className="rounded bg-brand-700 px-4 py-2 text-sm text-white hover:bg-brand-800 disabled:opacity-50"
						>
							{submitting ? t("signUp.submitting") : t("signUp.submit")}
						</button>
					</div>
				</form>
			</div>
		</div>
	);
}
