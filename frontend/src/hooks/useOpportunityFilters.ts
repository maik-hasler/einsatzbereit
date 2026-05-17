import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router";

export type OpportunityView = "list" | "map";

export interface OpportunityBounds {
	north: number;
	south: number;
	east: number;
	west: number;
}

function parseBounds(params: URLSearchParams): OpportunityBounds | undefined {
	const n = parseFloat(params.get("n") ?? "");
	const s = parseFloat(params.get("s") ?? "");
	const e = parseFloat(params.get("e") ?? "");
	const w = parseFloat(params.get("w") ?? "");
	if (
		Number.isFinite(n) &&
		Number.isFinite(s) &&
		Number.isFinite(e) &&
		Number.isFinite(w)
	) {
		return { north: n, south: s, east: e, west: w };
	}
	return undefined;
}

export function useOpportunityViewFilters(): {
	view: OpportunityView;
	bounds: OpportunityBounds | undefined;
	setView: (view: OpportunityView) => void;
	setBounds: (bounds: OpportunityBounds | undefined) => void;
} {
	const [params, setParams] = useSearchParams();

	const view: OpportunityView = useMemo(
		() => (params.get("view") === "map" ? "map" : "list"),
		[params],
	);

	const bounds = useMemo(() => parseBounds(params), [params]);

	const setView = useCallback(
		(next: OpportunityView) => {
			setParams(
				(prev) => {
					const params = new URLSearchParams(prev);
					if (next === "map") params.set("view", "map");
					else {
						params.delete("view");
						params.delete("n");
						params.delete("s");
						params.delete("e");
						params.delete("w");
					}
					return params;
				},
				{ replace: true },
			);
		},
		[setParams],
	);

	const setBounds = useCallback(
		(next: OpportunityBounds | undefined) => {
			setParams(
				(prev) => {
					const params = new URLSearchParams(prev);
					if (next) {
						params.set("n", next.north.toFixed(5));
						params.set("s", next.south.toFixed(5));
						params.set("e", next.east.toFixed(5));
						params.set("w", next.west.toFixed(5));
					} else {
						params.delete("n");
						params.delete("s");
						params.delete("e");
						params.delete("w");
					}
					return params;
				},
				{ replace: true },
			);
		},
		[setParams],
	);

	return { view, bounds, setView, setBounds };
}
