import { describe, it, expect } from "vitest";
import type { QueryClient } from "@tanstack/react-query";
import { createQueryClient } from "./queryClient";

function queryDefaults(client: QueryClient) {
	return client.getDefaultOptions().queries ?? {};
}

function shouldRetry(
	client: QueryClient,
	failureCount: number,
	error: unknown,
): boolean {
	const retry = queryDefaults(client).retry;
	if (typeof retry !== "function") {
		throw new Error("queries.retry is no longer a predicate");
	}
	return retry(failureCount, error as Error) as boolean;
}

describe("the query client's retry policy", () => {
	// A 4xx is an answer, not a hiccup: the id does not exist, the caller is not
	// allowed, the rate limit is spent. Retrying spends the user's connection to
	// be told the same thing again.
	it.each([400, 401, 403, 404, 409, 429, 499])(
		"does not retry a %i",
		(status) => {
			expect(shouldRetry(createQueryClient(), 0, { status })).toBe(false);
		},
	);

	it.each([500, 502, 503, 504])("retries a %i", (status) => {
		expect(shouldRetry(createQueryClient(), 0, { status })).toBe(true);
	});

	// No status at all is how lib/apiError.ts reports a request that never got a
	// response - a lost connection, which is the case retrying is for.
	it("retries an error carrying no status", () => {
		expect(shouldRetry(createQueryClient(), 0, new Error("network"))).toBe(
			true,
		);
	});

	it("gives up after two attempts", () => {
		const client = createQueryClient();

		expect(shouldRetry(client, 0, new Error("network"))).toBe(true);
		expect(shouldRetry(client, 1, new Error("network"))).toBe(true);
		expect(shouldRetry(client, 2, new Error("network"))).toBe(false);
	});
});

describe("the query client's defaults", () => {
	it("keeps data fresh for half a minute, so a back-navigation is instant", () => {
		expect(queryDefaults(createQueryClient()).staleTime).toBe(30_000);
	});

	// The service worker answers the opportunity list and detail routes from
	// cache while offline. The default "online" mode pauses a query before it
	// ever reaches it, so a volunteer with no signal would see a spinner over
	// data that is already there.
	it("lets a query reach the service worker while offline", () => {
		expect(queryDefaults(createQueryClient()).networkMode).toBe("offlineFirst");
	});

	it("refetches when a tab regains focus", () => {
		expect(queryDefaults(createQueryClient()).refetchOnWindowFocus).toBe(true);
	});

	// A retried mutation is a second sign-up, a second invitation, a second
	// cancellation. Whether one is safe to repeat is a per-call decision.
	it("never retries a mutation by default", () => {
		const mutations = createQueryClient().getDefaultOptions().mutations ?? {};
		expect(mutations.retry).toBe(false);
	});

	it("gives each call its own cache", () => {
		expect(createQueryClient()).not.toBe(createQueryClient());
	});
});
