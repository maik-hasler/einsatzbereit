import { dispatchToast } from "../lib/toastBus";
import { runtimeConfig } from "../lib/runtimeConfig";
import { EinsatzbereitApi } from "./api-client";

async function handleErrorResponse(response: Response): Promise<void> {
	if (response.ok) return;

	if (response.status === 401) {
		return;
	}

	if (response.status === 403) {
		dispatchToast("error", "You do not have permission to do this.");
		return;
	}

	if (response.status >= 500) {
		let detail = "An unexpected error occurred. Please try again later.";
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
				},
			});
			await handleErrorResponse(response);
			return response;
		},
	});
}
