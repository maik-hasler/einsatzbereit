import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../hooks/useApiClient";
import { getApiErrorMessage } from "../lib/apiError";
import { textareaClass } from "../lib/formClasses";
import Modal from "./Modal";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";

const REASONS = ["Spam", "IllegalContent", "Fraud", "Other"] as const;
type Reason = (typeof REASONS)[number];

interface Props {
	contentType: "VolunteerOpportunity" | "Organization";
	contentId: string;
	onClose: () => void;
	onSuccess: () => void;
}

export default function ReportContentModal({
	contentType,
	contentId,
	onClose,
	onSuccess,
}: Props) {
	const api = useApiClient();
	const { t } = useTranslation();
	const firstReasonRef = useRef<HTMLFieldSetElement>(null);

	const [reason, setReason] = useState<Reason>("Spam");
	const [detail, setDetail] = useState("");
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState<string | null>(null);

	const detailRequired = reason === "Other";
	const detailMissing = detailRequired && detail.trim().length === 0;

	async function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
		if (detailMissing) return;

		setLoading(true);
		setError(null);
		try {
			await api.createReport({
				contentId,
				contentType,
				reason,
				detail: detail.trim() || undefined,
			});
			onSuccess();
			onClose();
		} catch (err: unknown) {
			setError(getApiErrorMessage(err, t("reportContent.error")));
		} finally {
			setLoading(false);
		}
	}

	return (
		<Modal
			onClose={onClose}
			labelledBy="report-content-dialog-title"
			maxWidth="max-w-md"
			className="rounded-xl bg-white shadow-xl"
			initialFocusRef={firstReasonRef}
		>
			<h2
				id="report-content-dialog-title"
				className="border-b border-gray-100 px-6 py-4 text-lg font-semibold"
			>
				{t("reportContent.title")}
			</h2>

			<form onSubmit={(e) => void handleSubmit(e)}>
				<div className="space-y-4 px-6 py-4">
					<fieldset ref={firstReasonRef}>
						<legend className="mb-2 block text-sm font-medium">
							{t("reportContent.reasonLabel")}
						</legend>
						<div className="space-y-2">
							{REASONS.map((r) => (
								<label
									key={r}
									className="flex items-center gap-2 text-sm text-gray-700"
								>
									<input
										type="radio"
										name="report-reason"
										value={r}
										checked={reason === r}
										onChange={() => setReason(r)}
										className="h-4 w-4 border-gray-300 text-brand-700 focus:ring-brand-400"
									/>
									{t(`reportContent.reason.${r}`)}
								</label>
							))}
						</div>
					</fieldset>

					<div>
						<label
							htmlFor="report-content-detail"
							className="mb-1 block text-sm font-medium"
						>
							{detailRequired
								? t("reportContent.detailLabelRequired")
								: t("reportContent.detailLabelOptional")}
						</label>
						<textarea
							id="report-content-detail"
							rows={3}
							maxLength={1000}
							value={detail}
							onChange={(e) => setDetail(e.target.value)}
							aria-required={detailRequired}
							aria-invalid={detailMissing ? true : undefined}
							aria-describedby={
								detailMissing ? "report-content-detail-error" : undefined
							}
							className={textareaClass}
						/>
						{detailMissing && (
							<p
								id="report-content-detail-error"
								className="mt-1 text-xs text-red-600"
								role="alert"
							>
								{t("reportContent.detailRequiredError")}
							</p>
						)}
					</div>

					{error && <ErrorBanner message={error} />}
				</div>

				<div className="flex justify-end gap-2 border-t border-gray-100 px-6 py-4">
					<Button type="button" variant="secondary" onClick={onClose}>
						{t("common.cancel")}
					</Button>
					<Button type="submit" disabled={loading || detailMissing}>
						{loading
							? t("reportContent.submitting")
							: t("reportContent.submit")}
					</Button>
				</div>
			</form>
		</Modal>
	);
}
