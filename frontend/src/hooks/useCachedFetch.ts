import { useCallback } from "react";
import type { Dispatch, SetStateAction } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import i18n from "../i18n";
import { getApiErrorMessage } from "../lib/apiError";

/**
 * A read, keyed by a string, held in the shared query cache.
 *
 * This replaces `useSharedOrgFetch`, which kept a module-level map of in-flight
 * promises and deleted each entry the moment it settled - so two components
 * asking in the same tick made one request, and the next mount made another.
 * Navigating away and back re-fetched and flickered through a skeleton the user
 * had already been shown.
 *
 * The tuple it returns is the one those call sites were written against, and
 * deliberately so: seven widgets read `data === null` as "not here yet", and
 * changing that at the same time as changing what backs it would have made a
 * behaviour change out of a caching change. What is different is underneath -
 * the value survives unmounting, a second asker gets it without a request, and
 * `setData` writes to the cache rather than to one component's state, so an
 * optimistic update is still there when the component comes back.
 *
 * New code should call `useQuery` directly with a key from `lib/queryKeys.ts`.
 * This exists for the call sites that predate the cache.
 */
export function useCachedFetch<T>(
	key: string,
	fetcher: () => Promise<T>,
): [T | null, Dispatch<SetStateAction<T | null>>, string | null] {
	const queryClient = useQueryClient();
	const { data, error } = useQuery({ queryKey: [key], queryFn: fetcher });

	const setData = useCallback<Dispatch<SetStateAction<T | null>>>(
		(update) => {
			queryClient.setQueryData<T>([key], (previous) => {
				const next =
					typeof update === "function"
						? (update as (previous: T | null) => T | null)(previous ?? null)
						: update;
				// The cache stores `undefined` for "nothing here", and
				// `setQueryData` treats an `undefined` update as "leave it
				// alone" - so a `null` update is a no-op rather than a clear.
				// That is the one place this differs from the `useState` the
				// tuple used to wrap, and no caller relies on it: both
				// optimistic updaters return either a mapped value or `prev`
				// untouched.
				return next ?? undefined;
			});
		},
		[queryClient, key],
	);

	return [
		data ?? null,
		setData,
		error ? getApiErrorMessage(error, i18n.t("error.serverError")) : null,
	];
}
