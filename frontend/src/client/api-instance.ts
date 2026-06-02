import { dispatchToast } from "../lib/toastBus";
import { runtimeConfig } from "../lib/runtimeConfig";
import { EinsatzbereitApi } from "./api-client";
import i18next from "../i18n";

async function handleErrorResponse(response: Response): Promise<void> {
	if (response.ok) return;

	if (response.status === 401) {
		return;
	}

	if (response.status === 403) {
		dispatchToast("error", i18next.t("error.forbidden"));
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
				},
			});
			await handleErrorResponse(response);
			return response;
		},
	});
}
