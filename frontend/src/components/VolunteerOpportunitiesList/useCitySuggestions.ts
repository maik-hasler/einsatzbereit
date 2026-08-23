import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";
import {
	filterByLabelMatch,
	sortByLabelPrefixMatch,
} from "../../lib/citySuggestionSort";

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
					filterByLabelMatch(
						places.map((place) => ({
							label: place.label,
							lat: place.latitude,
							lng: place.longitude,
						})),
						query,
					),
					query,
				);
				if (results.length > 0) {
					cityCache.set(cacheKeyFor(query), results);
				}
				setSuggestions(results);
				setShow(results.length > 0);
			} catch {
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
