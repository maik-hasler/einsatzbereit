import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router";

export type OpportunityView = "list" | "map";

export interface OpportunityBounds {
	north: number;
	south: number;
	east: number;
	west: number;
}

export interface OpportunityFilters {
	search: string;
	city: string;
	occurrence: string;
	participationType: string;
	isRemote: boolean | undefined;
	dateFrom: string;
	dateTo: string;
	view: OpportunityView;
	bounds: OpportunityBounds | undefined;
}

interface FilterUpdate {
	search?: string;
	city?: string;
	occurrence?: string;
	participationType?: string;
	isRemote?: boolean | undefined;
	dateFrom?: string;
	dateTo?: string;
	view?: OpportunityView;
	bounds?: OpportunityBounds | undefined;
}

function parseBool(value: string | null): boolean | undefined {
	if (value === "true") return true;
	if (value === "false") return false;
	return undefined;
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

export function useOpportunityFilters(): {
	filters: OpportunityFilters;
	update: (patch: FilterUpdate) => void;
	clear: () => void;
} {
	const [params, setParams] = useSearchParams();

	const filters = useMemo<OpportunityFilters>(
		() => ({
			search: params.get("q") ?? "",
			city: params.get("city") ?? "",
			occurrence: params.get("occurrence") ?? "",
			participationType: params.get("pt") ?? "",
			isRemote: parseBool(params.get("remote")),
			dateFrom: params.get("from") ?? "",
			dateTo: params.get("to") ?? "",
			view: params.get("view") === "map" ? "map" : "list",
			bounds: parseBounds(params),
		}),
		[params],
	);

	const update = useCallback(
		(patch: FilterUpdate) => {
			setParams(
				(prev) => {
					const next = new URLSearchParams(prev);
					const writeString = (key: string, value: string | undefined) => {
						if (value === undefined || value === "") next.delete(key);
						else next.set(key, value);
					};
					if ("search" in patch) writeString("q", patch.search);
					if ("city" in patch) writeString("city", patch.city);
					if ("occurrence" in patch)
						writeString("occurrence", patch.occurrence);
					if ("participationType" in patch)
						writeString("pt", patch.participationType);
					if ("isRemote" in patch) {
						if (patch.isRemote === undefined) next.delete("remote");
						else next.set("remote", String(patch.isRemote));
					}
					if ("dateFrom" in patch) writeString("from", patch.dateFrom);
					if ("dateTo" in patch) writeString("to", patch.dateTo);
					if ("view" in patch) {
						if (patch.view === "map") next.set("view", "map");
						else next.delete("view");
					}
					if ("bounds" in patch) {
						if (patch.bounds) {
							next.set("n", patch.bounds.north.toFixed(5));
							next.set("s", patch.bounds.south.toFixed(5));
							next.set("e", patch.bounds.east.toFixed(5));
							next.set("w", patch.bounds.west.toFixed(5));
						} else {
							next.delete("n");
							next.delete("s");
							next.delete("e");
							next.delete("w");
						}
					}
					return next;
				},
				{ replace: true },
			);
		},
		[setParams],
	);

	const clear = useCallback(() => {
		setParams(
			(prev) => {
				const next = new URLSearchParams(prev);
				for (const key of [
					"q",
					"city",
					"occurrence",
					"pt",
					"remote",
					"from",
					"to",
					"n",
					"s",
					"e",
					"w",
				]) {
					next.delete(key);
				}
				return next;
			},
			{ replace: true },
		);
	}, [setParams]);

	return { filters, update, clear };
}
