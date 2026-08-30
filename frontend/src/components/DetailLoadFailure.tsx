import { useTranslation } from "react-i18next";
import RouteState from "./RouteState";
import type { LoadFailureKind } from "../lib/apiError";

interface Props {
	kind: LoadFailureKind;

	notFoundTitle: string;
	notFoundMessage: string;

	errorMessage: string;

	offlineMessage?: string;

	onRetry: () => void;

	action: { label: string; to: string };
	"data-testid"?: string;
}

// The full-page state a detail route lands on when its resource cannot be
// loaded (#2320). Every branch carries a heading, an explanation and a way
// back out - a 404 in particular gets the not-found state rather than a retry
// button that can only ever re-fetch the same missing id.
export default function DetailLoadFailure({
	kind,
	notFoundTitle,
	notFoundMessage,
	errorMessage,
	offlineMessage,
	onRetry,
	action,
	"data-testid": testId,
}: Props) {
	const { t } = useTranslation();

	if (kind === "notFound")
		return (
			<RouteState
				variant="notFound"
				title={notFoundTitle}
				message={notFoundMessage}
				action={action}
				data-testid={testId}
			/>
		);

	if (kind === "offline")
		return (
			<RouteState
				variant="offline"
				title={t("routeState.offline.title")}
				message={offlineMessage ?? t("routeState.offline.message")}
				onRetry={onRetry}
				action={action}
				data-testid={testId}
			/>
		);

	return (
		<RouteState
			variant="error"
			title={t("error.boundaryTitle")}
			message={errorMessage}
			onRetry={onRetry}
			action={action}
			data-testid={testId}
		/>
	);
}
