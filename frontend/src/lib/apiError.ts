import i18next from "../i18n";

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

export function getApiErrorStatus(err: unknown): number | null {
	if (!err || typeof err !== "object") return null;
	const status = (err as { status?: unknown }).status;
	return typeof status === "number" ? status : null;
}

export function isApiNotFoundError(err: unknown): boolean {
	return getApiErrorStatus(err) === 404;
}

export function isNetworkError(err: unknown): boolean {
	return getApiErrorStatus(err) === null;
}

export function isApiErrorCode(err: unknown, code: string): boolean {
	return (
		!!err &&
		typeof err === "object" &&
		(err as { errorCode?: unknown }).errorCode === code
	);
}

export function isApiForbiddenError(err: unknown): boolean {
	return getApiErrorStatus(err) === 403;
}

// A rejection only counts as user-actionable when it carries an errorCode
// this app has a specific, translated message for (see getApiErrorMessage) -
// a bare status or a generic JS error gives the user nothing to act on and
// should not be surfaced as a toast (#2241).
export function hasActionableErrorCode(err: unknown): boolean {
	if (!err || typeof err !== "object") return false;
	const errorCode = (err as { errorCode?: unknown }).errorCode;
	if (typeof errorCode !== "string" || !errorCode.trim()) return false;
	return i18next.exists(`apiError.${errorCode}`);
}
