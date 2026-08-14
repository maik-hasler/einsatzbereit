import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";
import { sortByLabelPrefixMatch } from "../../lib/citySuggestionSort";

export interface CitySuggestion {
	label: string;
	lat: number;
	lng: number;
}

// Module-level so it survives across hook re-mounts for the lifetime of the
// page (cleared on reload) - repeated searches for the same city (e.g. the
// user re-focuses the field, or backspaces and retypes) never need to hit the
// backend again since city name-to-coordinates mappings are effectively
// static. Never stores an empty result: an empty response is indistinguishable
// here from a transient backend/upstream hiccup, and caching it would turn
// that into a permanent false "no such city" for the rest of the page's life.
const cityCache = new Map<string, CitySuggestion[]>();

function cacheKeyFor(query: string) {
	return query.trim().toLowerCase();
}

// Debounced city-name autocomplete for the location filter's "search by city"
// input. Proxied through the backend's /v1/maps/cities endpoint (which in turn
// queries the public Nominatim/OpenStreetMap geocoder) so the visitor's IP
// address and search text are never sent to Nominatim directly - see
// docs/ADRs/5_map_and_geocoding_request_proxying.adoc.
export function useCitySuggestions(query: string) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [suggestions, setSuggestions] = useState<CitySuggestion[]>([]);
	const [show, setShow] = useState(false);
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const abortRef = useRef<AbortController | null>(null);

	useEffect(() => {
		if (query.length < 2) {
			setSuggestions([]);
			setShow(false);
			setLoading(false);
			setError(null);
			return;
		}

		const cached = cityCache.get(cacheKeyFor(query));
		if (cached) {
			setSuggestions(cached);
			setShow(cached.length > 0);
			setLoading(false);
			setError(null);
			return;
		}

		abortRef.current?.abort();
		const controller = new AbortController();
		abortRef.current = controller;
		setError(null);
		const timer = setTimeout(async () => {
			setLoading(true);
			try {
				const places = await api.searchCities(query, controller.signal);
				const results: CitySuggestion[] = sortByLabelPrefixMatch(
					places.map((place) => ({
						label: place.label,
						lat: place.latitude,
						lng: place.longitude,
					})),
					query,
				);
				if (results.length > 0) {
					cityCache.set(cacheKeyFor(query), results);
				}
				setSuggestions(results);
				setShow(results.length > 0);
			} catch {
				// A stale request's own abort (superseded by a newer keystroke, or
				// the component unmounting) isn't a real failure - only a genuine
				// rate-limit/network failure from Nominatim should surface (#1240).
				if (controller.signal.aborted) return;
				setSuggestions([]);
				setShow(false);
				setError(t("opportunities.cityError"));
			} finally {
				if (!controller.signal.aborted) setLoading(false);
			}
		}, 350);
		return () => {
			clearTimeout(timer);
			controller.abort();
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [query]);

	function reset() {
		setSuggestions([]);
		setShow(false);
		setLoading(false);
		setError(null);
	}

	return { suggestions, show, setShow, reset, loading, error };
}
