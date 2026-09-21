import { signinLocaleArgs } from "./authLocale";

export const REPORT_INTENT_PARAM = "report";

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
