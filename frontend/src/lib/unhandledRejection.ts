import i18n from "../i18n";
import { dispatchToast } from "./toastBus";
import { getApiErrorMessage, hasActionableErrorCode } from "./apiError";

// Most promise rejections that reach `window`'s unhandledrejection event are
// a missed .catch() somewhere in the app, not something the user did or can
// react to - toasting "unexpected error" (and blaming the server) for those
// teaches people to ignore every toast, including the ones that matter
// (#2241). Still logged unconditionally so it stays visible for debugging.
export function handleUnhandledRejection(reason: unknown): void {
	console.error("[unhandledrejection]", reason);

	if (!hasActionableErrorCode(reason)) return;

	dispatchToast(
		"error",
		getApiErrorMessage(reason, i18n.t("error.serverError")),
	);
}
