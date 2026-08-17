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
 * Reads the HTTP status off a rejected API call, or null when the rejection
 * carries none (a network failure, an aborted request).
 *
 * Both shapes the NSwag client can throw - the raw ProblemDetails object
 * (when the response has a JSON body) or an ApiException instance (when it
 * doesn't, which is what an unmapped status such as 400 produces, since the
 * generator only emits branches for the statuses the endpoint declares) -
 * carry a numeric `.status` field, so this works for either. It is the only
 * thing that tells one failure apart from another once the client has thrown:
 * a caller that keeps just the message string cannot distinguish "that
 * organization does not exist" from "the server fell over" (#1774).
 */
export function getApiErrorStatus(err: unknown): number | null {
	if (!err || typeof err !== "object") return null;
	const status = (err as { status?: unknown }).status;
	return typeof status === "number" ? status : null;
}

/** Detects a 404 from a rejected API call - see getApiErrorStatus. */
export function isApiNotFoundError(err: unknown): boolean {
	return getApiErrorStatus(err) === 404;
}

/**
 * Detects a rejected API call that never got an HTTP response at all - the
 * fetch itself failed (dropped connection, DNS failure, CORS) as opposed to
 * an error response the server actually sent, which always carries a numeric
 * status (see getApiErrorStatus). This is a stronger, cold-reload-safe
 * offline signal than `navigator.onLine`, which can misreport `true` right
 * after a hard reload or cold PWA launch while genuinely offline - a
 * well-documented cross-browser limitation (#1901) - since it reflects what
 * the request that just ran actually did, rather than a cached browser flag.
 */
export function isNetworkError(err: unknown): boolean {
	return getApiErrorStatus(err) === null;
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

/** Detects a 403 from a rejected API call - see getApiErrorStatus. */
export function isApiForbiddenError(err: unknown): boolean {
	return getApiErrorStatus(err) === 403;
}
