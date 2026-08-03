import { useEffect, useState, type Dispatch, type SetStateAction } from "react";
import i18n from "../i18n";
import { getApiErrorMessage } from "../lib/apiError";

// Module-level registry of in-flight requests, keyed by a caller-supplied
// string (typically `${resource}:${organizationId}:${refreshKey}`, or just
// `${resource}` for a resource with no such scoping). Several call sites
// independently need the same data on the same mount - Calendar and Upcoming
// Opportunities widgets both call getOrganizationCalendarEvents, Upcoming
// Opportunities and Quick Check-in both call getOrganizationOpportunities,
// Header and HomePage both call getOrganizations - and without this, each
// caller fires its own copy of the same request. Only the in-flight
// *request* is shared, not the resolved data: each caller still gets its own
// local useState copy (see the returned setter) so one widget can
// optimistically mutate its view (e.g. CalendarWidget after saving an event
// color) without affecting another that happened to share the fetch.
const inFlight = new Map<string, Promise<unknown>>();

export function useSharedOrgFetch<T>(
	key: string,
	fetcher: () => Promise<T>,
): [T | null, Dispatch<SetStateAction<T | null>>, string | null] {
	const [data, setData] = useState<T | null>(null);
	const [error, setError] = useState<string | null>(null);

	useEffect(() => {
		let alive = true;
		setData(null);
		setError(null);

		let promise = inFlight.get(key) as Promise<T> | undefined;
		if (!promise) {
			promise = fetcher();
			inFlight.set(key, promise);
			// Dropped once settled - the next mount/refresh under this key issues
			// a fresh request rather than serving indefinitely stale data.
			void promise.finally(() => {
				if (inFlight.get(key) === promise) inFlight.delete(key);
			});
		}

		promise
			.then((result) => {
				if (alive) setData(result);
			})
			.catch((e: unknown) => {
				if (alive) setError(getApiErrorMessage(e, i18n.t("error.serverError")));
			});

		return () => {
			alive = false;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [key]);

	return [data, setData, error];
}
