import { dispatchToast } from "../lib/toastBus";
import { notifySessionExpired } from "../lib/sessionExpiryBus";
import { clearAuthRecoveryAttempts } from "../lib/authRecovery";
import { runtimeConfig } from "../lib/runtimeConfig";
import { EinsatzbereitApi } from "./api-client";
import i18next from "../i18n";

// Fallback wait when the server doesn't send Retry-After - matches the
// default rate-limit window (RateLimitingOptions.ReadOptions.WindowSeconds).
const DEFAULT_RATE_LIMIT_WAIT_SECONDS = 60;

// A single burst (several parallel requests against an already-exhausted
// bucket) would otherwise fire one identical toast per failed request.
// Track how long the last announcement is still valid for and stay quiet
// until then instead - the message itself already tells the user when it's
// safe to retry, so re-announcing sooner would just be noise (#2208).
let rateLimitSuppressedUntil = 0;

function parseRetryAfterSeconds(headerValue: string | null): number | null {
	if (!headerValue) return null;
	const seconds = Number.parseInt(headerValue, 10);
	return Number.isFinite(seconds) && seconds >= 0 ? seconds : null;
}

function handleRateLimited(response: Response): void {
	const now = Date.now();
	if (now < rateLimitSuppressedUntil) return;

	const retryAfterSeconds = parseRetryAfterSeconds(
		response.headers.get("Retry-After"),
	);
	rateLimitSuppressedUntil =
		now + (retryAfterSeconds ?? DEFAULT_RATE_LIMIT_WAIT_SECONDS) * 1000;

	dispatchToast(
		"error",
		retryAfterSeconds !== null
			? i18next.t("error.rateLimited", { count: retryAfterSeconds })
			: i18next.t("error.rateLimitedGeneric"),
	);
}

export async function handleErrorResponse(
	response: Response,
	hadAccessToken: boolean,
): Promise<void> {
	if (response.ok) {
		// Proves the token this client is using is actually accepted by the
		// backend - the one signal strong enough to end a recovery episode
		// (see useSessionExpiryHandler.ts and lib/authRecovery.ts for why
		// completing the Keycloak redirect alone isn't enough, #2208).
		if (hadAccessToken) clearAuthRecoveryAttempts();
		return;
	}

	if (response.status === 401) {
		if (hadAccessToken) {
			notifySessionExpired();
		}
		return;
	}

	if (response.status === 403) {
		dispatchToast("error", i18next.t("error.forbidden"));
		return;
	}

	if (response.status === 429) {
		handleRateLimited(response);
		return;
	}

	if (response.status >= 500) {
		let detail = i18next.t("error.serverError");
		try {
			const clone = response.clone();
			const body = await clone.json();
			if (body?.detail) detail = body.detail;
		} catch {
			// ignore parse errors
		}
		dispatchToast("error", detail);
		console.error("[API] Server error", response.status, response.url);
	}
}

export function createApiClient(accessToken?: string): EinsatzbereitApi {
	return new EinsatzbereitApi(runtimeConfig.apiUrl, {
		fetch: async (url: RequestInfo, init?: RequestInit) => {
			const response = await globalThis.fetch(url, {
				...init,
				headers: {
					...init?.headers,
					...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
					"X-Timezone": Intl.DateTimeFormat().resolvedOptions().timeZone,
					"X-Language": i18next.language.split("-")[0],
				},
			});
			await handleErrorResponse(response, Boolean(accessToken));
			return response;
		},
	});
}
