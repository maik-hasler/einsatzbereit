import i18next from "../i18n";

/**
 * Extracts a human-readable message from a rejected API call.
 *
 * The NSwag client throws the raw ProblemDetails object (`{ title, status,
 * detail, errorCode }`) for error responses. `detail`/`title` are always
 * English (server-side `Error.Description` text) - see #1250 - so they are
 * never shown to the user, only logged for debugging. `errorCode` (set by
 * ResultFailureExceptionHandler for every Result-pattern failure) is looked
 * up as an `apiError.<errorCode>` translation key; if that key doesn't
 * exist (unmapped code, or a rejection with no errorCode at all, e.g. a
 * network failure), the caller-supplied, already-localized `fallback` is
 * used instead.
 */
export function getApiErrorMessage(err: unknown, fallback: string): string {
	if (err && typeof err === "object") {
		const o = err as { detail?: unknown; errorCode?: unknown };
		if (typeof o.detail === "string" && o.detail.trim()) {
			console.error("[API] error detail (not shown to user):", o.detail);
		}
		if (typeof o.errorCode === "string" && o.errorCode.trim()) {
			const key = `apiError.${o.errorCode}`;
			if (i18next.exists(key)) return i18next.t(key);
		}
	}
	return fallback;
}

/**
 * Detects a 404 from a rejected API call.
 *
 * Both shapes the NSwag client can throw - the raw ProblemDetails object
 * (when the response has a JSON body) or an ApiException instance (when it
 * doesn't) - carry a numeric `.status` field, so this works for either.
 */
export function isApiNotFoundError(err: unknown): boolean {
	return (
		!!err &&
		typeof err === "object" &&
		(err as { status?: unknown }).status === 404
	);
}

/**
 * Detects a specific Result-pattern `errorCode` on a rejected API call (see
 * `getApiErrorMessage` for the shape). Used to treat one particular failure
 * as a benign no-op rather than surfacing it - e.g. a retried publish call
 * landing on an opportunity a previous attempt already published.
 */
export function isApiErrorCode(err: unknown, code: string): boolean {
	return (
		!!err &&
		typeof err === "object" &&
		(err as { errorCode?: unknown }).errorCode === code
	);
}
