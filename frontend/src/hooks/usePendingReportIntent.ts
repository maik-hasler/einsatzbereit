import { useEffect, useState } from "react";
import { useSearchParams } from "react-router";
import { REPORT_INTENT_PARAM } from "../lib/reportIntent";

/**
 * The target id of a report intent carried back from sign-in, or `null` when there is none.
 *
 * Read once on mount and held in state: the marker is stripped from the URL straight away, so a
 * reload, a back-navigation or a shared link cannot reopen the modal a second time.
 */
export function usePendingReportIntent(): string | null {
	const [searchParams, setSearchParams] = useSearchParams();
	const [pendingTargetId] = useState(
		() => searchParams.get(REPORT_INTENT_PARAM) || null,
	);

	useEffect(() => {
		if (pendingTargetId === null) return;
		setSearchParams(
			(prev) => {
				const next = new URLSearchParams(prev);
				next.delete(REPORT_INTENT_PARAM);
				return next;
			},
			{ replace: true },
		);
	}, [pendingTargetId, setSearchParams]);

	return pendingTargetId;
}
