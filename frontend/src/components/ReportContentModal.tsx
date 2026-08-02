import { useState } from "react";
import { useTranslation } from "react-i18next";
import Modal from "./Modal";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";
import { getApiErrorMessage } from "../lib/apiError";

const REPORT_REASONS = [
	"Spam",
	"IllegalContent",
	"Fraud",
	"Harassment",
	"Other",
] as const;

export type ReportReason = (typeof REPORT_REASONS)[number];

interface ReportContentModalProps {
	targetLabel: string;
	onSubmit: (reason: ReportReason, details: string) => Promise<void>;
	onClose: () => void;
}

export default function ReportContentModal({
	targetLabel,
	onSubmit,
	onClose,
}: ReportContentModalProps) {
	const { t } = useTranslation();
	const [reason, setReason] = useState<ReportReason>("Spam");
	const [details, setDetails] = useState("");
	const [submitting, setSubmitting] = useState(false);
	const [error, setError] = useState<string | null>(null);

	async function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
		setSubmitting(true);
		setError(null);
		try {
			await onSubmit(reason, details.trim());
			onClose();
		} catch (err) {
			setError(getApiErrorMessage(err, t("report.submitError")));
		} finally {
			setSubmitting(false);
		}
	}

	return (
		<Modal
			onClose={onClose}
			labelledBy="report-content-title"
			maxWidth="max-w-md"
		>
			<h2
				id="report-content-title"
				className="mb-1 text-lg font-semibold text-gray-900"
			>
				{t("report.title")}
			</h2>
			<p className="mb-5 text-sm text-gray-500">{targetLabel}</p>

			<form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
				<div>
					<label
						htmlFor="report-reason"
						className="block text-sm font-medium text-gray-700"
					>
						{t("report.reasonLabel")}
					</label>
					<select
						id="report-reason"
						value={reason}
						onChange={(e) => setReason(e.target.value as ReportReason)}
						className="mt-1 block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
					>
						{REPORT_REASONS.map((r) => (
							<option key={r} value={r}>
								{t(`report.reasons.${r}`)}
							</option>
						))}
					</select>
				</div>

				<div>
					<label
						htmlFor="report-details"
						className="block text-sm font-medium text-gray-700"
					>
						{t("report.detailsLabel")}
					</label>
					<textarea
						id="report-details"
						rows={3}
						maxLength={1000}
						value={details}
						onChange={(e) => setDetails(e.target.value)}
						placeholder={t("report.detailsPlaceholder")}
						className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
					/>
					<p className="mt-1 text-right text-xs text-gray-500">
						{details.length}/1000
					</p>
				</div>

				{error && <ErrorBanner message={error} />}

				<div className="flex gap-3">
					<Button
						type="button"
						variant="secondary"
						onClick={onClose}
						className="flex-1"
					>
						{t("report.cancel")}
					</Button>
					<Button type="submit" disabled={submitting} className="flex-1">
						{submitting ? t("report.submitting") : t("report.submit")}
					</Button>
				</div>
			</form>
		</Modal>
	);
}
