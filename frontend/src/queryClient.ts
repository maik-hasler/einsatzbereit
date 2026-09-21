import { QueryClient } from "@tanstack/react-query";
import { getApiErrorStatus } from "./lib/apiError";

/**
 * The app's single server-state cache.
 *
 * Before this existed, every component fetched from its own `useEffect` into
 * its own `useState`: no cache, so every navigation back to a page re-fetched
 * it and flickered through a skeleton it had already shown; no deduplication,
 * so two components wanting the same list made two requests; and no
 * invalidation, so a mutation had to reach into the array it had just changed
 * by hand.
 *
 * The defaults below are the whole policy, and each is a decision:
 */
export function createQueryClient(): QueryClient {
	return new QueryClient({
		defaultOptions: {
			queries: {
				// Long enough that navigating away and back is instant - which is
				// the flicker this cache exists to remove - and short enough that
				// an organizer who just changed something sees it. Anything that
				// must be fresher says so at its own call site.
				staleTime: 30_000,

				// A tab left open for an afternoon should not show this morning's
				// sign-up counts. Paired with the staleTime above, coming back
				// within 30 seconds costs nothing.
				refetchOnWindowFocus: true,

				// The service worker answers /v1/volunteer-opportunities and the
				// detail route from cache while offline (vite.config.ts's
				// runtimeCaching). The default "online" mode would pause those
				// queries before they ever reached it, so a volunteer with no
				// signal would see a spinner over data that is sitting in the
				// cache. "offlineFirst" lets the request through and lets Workbox
				// answer it.
				networkMode: "offlineFirst",

				retry: (failureCount, error) => {
					const status = getApiErrorStatus(error);
					// A 4xx is an answer, not a hiccup: the id does not exist, the
					// caller is not allowed, the rate limit is spent. Retrying
					// spends the user's connection to be told the same thing again.
					if (status !== null && status >= 400 && status < 500) return false;
					return failureCount < 2;
				},
			},
			mutations: {
				// A retried mutation is a second sign-up, a second invitation, a
				// second cancellation. Whether one is safe to repeat is a per-call
				// decision, never a default.
				retry: false,
				networkMode: "offlineFirst",
			},
		},
	});
}
