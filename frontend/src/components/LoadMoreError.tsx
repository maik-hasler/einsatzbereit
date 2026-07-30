import { useTranslation } from "react-i18next";
import ErrorBanner from "./ErrorBanner";

interface Props {
	message: string;
	retrying: boolean;
	onRetry: () => void;
}

// Inline "load more failed" state - keeps whatever already rendered above it
// in place and offers a retry, rather than the page's full error banner
// (which would also hide those already-loaded rows). See einsatzbereit#1226.
export default function LoadMoreError({ message, retrying, onRetry }: Props) {
	const { t } = useTranslation();
	return (
		<div className="mt-6 flex flex-col items-center gap-3">
			<ErrorBanner message={message} />
			<button
				type="button"
				onClick={onRetry}
				disabled={retrying}
				className="rounded-xl border border-red-200 bg-red-50 px-6 py-2 text-sm font-semibold text-red-700 transition-colors hover:bg-red-100 disabled:opacity-40"
			>
				{retrying ? t("common.retrying") : t("common.tryAgain")}
			</button>
		</div>
	);
}
