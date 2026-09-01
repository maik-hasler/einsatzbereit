import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";

export interface CitySuggestion {
	label: string;
	lat: number;
	lng: number;
}

const cityCache = new Map<string, CitySuggestion[]>();

function cacheKeyFor(query: string) {
	return query.trim().toLowerCase();
}

export function useCitySuggestions(query: string) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [suggestions, setSuggestions] = useState<CitySuggestion[]>([]);
	// Whether a lookup for the *current* query has actually resolved. Without it the
	// "no match" helper line fires off an empty suggestion array the moment the third
	// character lands - a full debounce interval before the request is even sent, so
	// the field claimed the city did not exist before it had looked (#2319).
	const [searched, setSearched] = useState(false);
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const abortRef = useRef<AbortController | null>(null);

	useEffect(() => {
		if (query.length < 2) {
			setSuggestions([]);
			setSearched(false);
			setLoading(false);
			setError(null);
			return;
		}

		const cached = cityCache.get(cacheKeyFor(query));
		if (cached) {
			setSuggestions(cached);
			setSearched(true);
			setLoading(false);
			setError(null);
			return;
		}

		abortRef.current?.abort();
		const controller = new AbortController();
		abortRef.current = controller;
		setSearched(false);
		setError(null);
		const timer = setTimeout(async () => {
			setLoading(true);
			try {
				const places = await api.searchCities(query, controller.signal);
				// Taken in the order the server ranked them. Re-filtering here to
				// labels that literally contain the query threw away every postal-code
				// hit - "26129" resolves to "26129 Oldenburg", which no client-side
				// substring test can recognise as a match.
				const results: CitySuggestion[] = places.map((place) => ({
					label: place.label,
					lat: place.latitude,
					lng: place.longitude,
				}));
				if (results.length > 0) {
					cityCache.set(cacheKeyFor(query), results);
				}
				setSuggestions(results);
				setSearched(true);
			} catch {
				if (controller.signal.aborted) return;
				setSuggestions([]);
				setSearched(true);
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

	return { suggestions, searched, loading, error };
}
