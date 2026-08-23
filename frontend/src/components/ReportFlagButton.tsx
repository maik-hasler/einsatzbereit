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
					"relative z-20 inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-gray-600 transition-colors hover:bg-gray-100 hover:text-gray-800"
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
