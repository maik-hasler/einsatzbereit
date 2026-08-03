import { useState } from "react";
import { useTranslation } from "react-i18next";
import ReportContentModal, { type ReportReason } from "./ReportContentModal";
import { dispatchToast } from "../lib/toastBus";
import { FlagIcon } from "./icons";

interface ReportFlagButtonProps {
	targetLabel: string;
	ariaLabel: string;
	onReport: (reason: ReportReason, details: string) => Promise<void>;
	className?: string;
}

/**
 * Small icon-only flag trigger for list/card contexts where the full-text
 * "Report" button used on detail pages (OrganizationProfilePage,
 * VolunteerOpportunityDetailPage) doesn't fit. Cards it's dropped into
 * usually have an absolutely-positioned overlay <Link> for click-to-navigate,
 * so the button stops propagation and sits above it via z-20.
 */
export default function ReportFlagButton({
	targetLabel,
	ariaLabel,
	onReport,
	className,
}: ReportFlagButtonProps) {
	const { t } = useTranslation();
	const [showReport, setShowReport] = useState(false);

	async function handleSubmit(reason: ReportReason, details: string) {
		await onReport(reason, details);
		dispatchToast("success", t("report.submitSuccess"));
	}

	return (
		<>
			<button
				type="button"
				onClick={(e) => {
					e.preventDefault();
					e.stopPropagation();
					setShowReport(true);
				}}
				aria-label={ariaLabel}
				className={
					className ??
					"relative z-20 inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-gray-400 transition-colors hover:bg-gray-100 hover:text-gray-600"
				}
			>
				<FlagIcon className="h-3.5 w-3.5" />
			</button>
			{showReport && (
				<ReportContentModal
					targetLabel={targetLabel}
					onSubmit={handleSubmit}
					onClose={() => setShowReport(false)}
				/>
			)}
		</>
	);
}
