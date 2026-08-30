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

export type LoadFailureKind = "notFound" | "offline" | "error";

// How a failed detail-route load should be presented (#2320). A 404 there is
// not a transient server fault - the id simply does not resolve - so it earns
// the app's not-found state instead of a retry that can never succeed.
export function classifyLoadFailure(
	err: unknown,
	online: boolean,
): LoadFailureKind {
	if (isApiNotFoundError(err)) return "notFound";
	if (!online || isNetworkError(err)) return "offline";
	return "error";
}

// The field names an ASP.NET `ValidationProblemDetails` blames, lowercased.
// The framework's own messages are untranslated English internals ("The field
// FirstName must be a string or array type with a maximum length of '100'."),
// so only the names are usable - the copy shown to the user comes from the
// locale files (#2320). NSwag hands a declared status the parsed body and any
// other status a raw `response` string, so both shapes are read here.
export function getInvalidFieldNames(err: unknown): string[] {
	if (!err || typeof err !== "object") return [];

	const problem = err as { errors?: unknown; response?: unknown };
	let errors = problem.errors;

	if (!errors && typeof problem.response === "string") {
		try {
			errors = (JSON.parse(problem.response) as { errors?: unknown }).errors;
		} catch {
			return [];
		}
	}

	if (!errors || typeof errors !== "object") return [];
	return Object.keys(errors).map((name) => name.toLowerCase());
}
