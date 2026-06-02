/**
 * Extracts a human-readable message from a rejected API call.
 *
 * The NSwag client throws the raw ProblemDetails object (`{ title, status,
 * detail }`) for error responses, which has no `.message` property. Reading
 * `err.message` there yields `undefined`, which previously left error state
 * falsy and made pages render their empty state instead of the error (see #349).
 */
export function getApiErrorMessage(err: unknown, fallback: string): string {
	if (err && typeof err === "object") {
		const o = err as { detail?: unknown; title?: unknown; message?: unknown };
		if (typeof o.detail === "string" && o.detail.trim()) return o.detail;
		if (typeof o.title === "string" && o.title.trim()) return o.title;
		if (typeof o.message === "string" && o.message.trim()) return o.message;
	}
	return fallback;
}
