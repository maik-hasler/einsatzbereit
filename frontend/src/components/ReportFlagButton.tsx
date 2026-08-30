import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import ReportContentModal, { type ReportReason } from "./ReportContentModal";
import { dispatchToast } from "../lib/toastBus";
import { FlagIcon } from "./icons";

interface ReportFlagButtonProps {
	targetLabel: string;
	targetLabelLang?: string;
	ariaLabel: string;
	onReport: (reason: ReportReason, details: string) => Promise<void>;

	/**
	 * Routed to instead of opening the modal - lets an anonymous visitor be sent through
	 * sign-in first, carrying the click with them (see lib/reportIntent).
	 */
	onRequireSignIn?: () => void;

	/**
	 * Opens the modal as soon as this turns true, for a click resumed after that sign-in round
	 * trip. Not read once on mount: the session is restored asynchronously, so the caller's
	 * "signed in and this is the target that was clicked" is routinely false on the first render.
	 */
	autoOpen?: boolean;
	className?: string;
}

export default function ReportFlagButton({
	targetLabel,
	targetLabelLang,
	ariaLabel,
	onReport,
	onRequireSignIn,
	autoOpen = false,
	className,
}: ReportFlagButtonProps) {
	const { t } = useTranslation();
	const [showReport, setShowReport] = useState(autoOpen);

	// Keyed on autoOpen alone, so closing the modal does not immediately reopen it.
	useEffect(() => {
		if (autoOpen) setShowReport(true);
	}, [autoOpen]);

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
					if (onRequireSignIn) {
						onRequireSignIn();
						return;
					}
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
					targetLabelLang={targetLabelLang}
					onSubmit={handleSubmit}
					onClose={() => setShowReport(false)}
				/>
			)}
		</>
	);
}
