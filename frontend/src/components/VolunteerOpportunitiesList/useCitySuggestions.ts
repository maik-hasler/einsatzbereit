import { useEffect, useRef, useState } from "react";

interface NominatimPlace {
	lat: string;
	lon: string;
	address?: {
		city?: string;
		town?: string;
		village?: string;
		municipality?: string;
	};
}

export interface CitySuggestion {
	label: string;
	lat: number;
	lng: number;
}

// Debounced city-name autocomplete against the public Nominatim (OpenStreetMap)
// geocoder, used by the location filter's "search by city" input.
export function useCitySuggestions(query: string) {
	const [suggestions, setSuggestions] = useState<CitySuggestion[]>([]);
	const [show, setShow] = useState(false);
	const abortRef = useRef<AbortController | null>(null);

	useEffect(() => {
		if (query.length < 2) {
			setSuggestions([]);
			setShow(false);
			return;
		}
		abortRef.current?.abort();
		const controller = new AbortController();
		abortRef.current = controller;
		const timer = setTimeout(async () => {
			try {
				const res = await fetch(
					`https://nominatim.openstreetmap.org/search?format=json&addressdetails=1&featuretype=city&q=${encodeURIComponent(query)}&limit=6`,
					{
						signal: controller.signal,
						headers: { "Accept-Language": "de,en" },
					},
				);
				if (!res.ok) return;
				const data = (await res.json()) as NominatimPlace[];
				const results: CitySuggestion[] = data
					.map((r) => ({
						label:
							r.address?.city ??
							r.address?.town ??
							r.address?.village ??
							r.address?.municipality ??
							"",
						lat: parseFloat(r.lat),
						lng: parseFloat(r.lon),
					}))
					.filter((s) => s.label.length > 0)
					.filter(
						(s, i, arr) => arr.findIndex((x) => x.label === s.label) === i,
					)
					.slice(0, 6);
				setSuggestions(results);
				setShow(results.length > 0);
			} catch {
				// AbortError or network - ignore
			}
		}, 350);
		return () => {
			clearTimeout(timer);
			controller.abort();
		};
	}, [query]);

	function reset() {
		setSuggestions([]);
		setShow(false);
	}

	return { suggestions, show, setShow, reset };
}
