export interface RetryOptions {
	/** Total attempts, including the first one. */
	attempts?: number;
	/** Base delay; backs off linearly (delayMs, 2x delayMs, ...). */
	delayMs?: number;
	shouldRetry?: (error: unknown) => boolean;
}

/**
 * Whether a rejected API call looks like something that could succeed on a
 * retry.
 *
 * Both shapes the NSwag client can throw carry a numeric `.status` (see
 * isApiNotFoundError in apiError.ts). A rejection with no numeric status is a
 * transport-level failure (network drop, CORS, aborted request) rather than a
 * response, so it is worth retrying. Of the real responses, only 429 and 5xx
 * are transient - retrying a 401/403/404 just repeats a request the server has
 * already answered definitively.
 */
export function isTransientApiError(error: unknown): boolean {
	const status =
		error && typeof error === "object"
			? (error as { status?: unknown }).status
			: undefined;

	if (typeof status !== "number") return true;
	return status === 429 || status >= 500;
}

/**
 * Runs an idempotent async operation, retrying transient failures.
 *
 * Exists because a single transient failure used to be permanent for anything
 * loaded through useSharedOrgFetch: it fetches once per key on mount and never
 * re-attempts, so one dropped request left the consumer's data null for the
 * life of the page. On HomePage that silently downgraded a signed-in organizer
 * to the "no organizations" branch - the hero stopped offering "Organization
 * overview" and instead invited them to create an organization they already
 * had, recoverable only by a manual reload.
 *
 * Only ever wrap reads in this: every current caller is a GET, and retrying a
 * non-idempotent request would risk duplicating a write.
 */
export async function withRetry<T>(
	operation: () => Promise<T>,
	{
		attempts = 3,
		delayMs = 250,
		shouldRetry = isTransientApiError,
	}: RetryOptions = {},
): Promise<T> {
	let lastError: unknown;

	for (let attempt = 1; attempt <= attempts; attempt++) {
		try {
			return await operation();
		} catch (error) {
			lastError = error;
			if (attempt === attempts || !shouldRetry(error)) break;
			await new Promise((resolve) => setTimeout(resolve, delayMs * attempt));
		}
	}

	throw lastError;
}
