import { useId } from "react";
import { useTranslation } from "react-i18next";
import ErrorBanner from "./ErrorBanner";

interface Props {
	message: string;
	retrying: boolean;
	onRetry: () => void;
	"data-testid"?: string;
}

// Inline "load more failed" state - keeps whatever already rendered above it
// in place and offers a retry, rather than the page's full error banner
// (which would also hide those already-loaded rows). See einsatzbereit#1226.
// Also doubles as the initial-load error state for lists that want a retry
// affordance instead of a dead-end ErrorBanner (einsatzbereit#1728) - `message`/
// `retrying`/`onRetry` read the same either way.
export default function LoadMoreError({
	message,
	retrying,
	onRetry,
	"data-testid": testId,
}: Props) {
	const { t } = useTranslation();
	// useId (not a fixed string) - OrgOpportunitiesPage renders a drafts and a
	// published instance of this at once, which would otherwise collide.
	const errorId = useId();
	return (
		<div className="mt-6 flex flex-col items-center gap-3" data-testid={testId}>
			<ErrorBanner id={errorId} message={message} />
			{/* aria-describedby ties this to the error text above - its own
			accessible name ("Retry") says nothing about what it's retrying, and a
			screen-reader user tabbing to it after the banner's one-time aria-live
			announcement has already passed would otherwise hear just "Retry,
			button" with no context (same reasoning as OrgDashboardPage's own
			layout-load-error retry button). */}
			<button
				type="button"
				onClick={onRetry}
				disabled={retrying}
				aria-describedby={errorId}
				className="rounded-xl border border-red-200 bg-red-50 px-6 py-2 text-sm font-semibold text-red-700 transition-colors hover:bg-red-100 disabled:opacity-40"
			>
				{retrying ? t("common.retrying") : t("common.retry")}
			</button>
		</div>
	);
}
