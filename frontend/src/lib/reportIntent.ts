import { useEffect, useState } from "react";
import { useSearchParams } from "react-router";
import { signinLocaleArgs } from "./authLocale";

const REPORT_INTENT_PARAM = "report";

/**
 * Sign-in arguments that carry a pending "report this" click across the Keycloak round trip.
 *
 * An anonymous visitor who clicked Report was sent to Keycloak and returned to the page they
 * started from - but the click itself was dropped, so they landed back on a page that looked
 * exactly as before, with no modal, no toast and no hint that anything had been remembered
 * (#2326). The intent rides on the returnTo URL, the only channel that survives the full-page
 * navigation `onSigninCallback` performs.
 *
 * The target id travels rather than a bare flag so a list page knows which row to reopen, and
 * so a stale link cannot open the modal against a different entity than the one clicked.
 */
export function reportIntentSigninArgs(
	pathname: string,
	search: string,
	targetId: string,
) {
	const params = new URLSearchParams(search);
	params.set(REPORT_INTENT_PARAM, targetId);
	return signinLocaleArgs(`${pathname}?${params.toString()}`);
}

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
