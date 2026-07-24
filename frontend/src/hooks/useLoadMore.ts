import { useCallback, useEffect, useRef, useState } from "react";
import type { Dispatch, SetStateAction } from "react";

export interface LoadMorePage<T> {
	items: T[];
	pageCount?: number;
}

export type FetchPage<T> = (page: number) => Promise<LoadMorePage<T>>;

export interface UseLoadMoreOptions {
	/**
	 * Values that, when changed, reset to page 1 and refetch - the
	 * useEffect-deps equivalent for "filters/search changed". Omit for lists
	 * that only ever reset via the returned `reset()` (e.g. a scope switch or
	 * search-submit handler that already has the fresher value in hand).
	 */
	deps?: unknown[];
	getErrorMessage?: (error: unknown) => string;
}

export interface UseLoadMoreResult<T> {
	items: T[];
	setItems: Dispatch<SetStateAction<T[]>>;
	page: number;
	pageCount: number;
	loading: boolean;
	loadingMore: boolean;
	error: string | null;
	hasMore: boolean;
	loadMore: () => void;
	reset: () => void;
}

function defaultGetErrorMessage(error: unknown): string {
	return error instanceof Error
		? error.message
		: "An unexpected error occurred.";
}

/**
 * Shared load-more pagination: items/page/pageCount/loading/loadingMore/error
 * plus the fetch-on-page-change effect, extracted from four independent copies
 * of this exact state (see einsatzbereit#868).
 *
 * `fetchPage` is read via a ref updated on every render rather than listed as
 * an effect dep, so a caller passing a fresh inline closure each render (the
 * common case - it captures current filters/search) doesn't retrigger the
 * fetch effect; only a `page` change or an explicit `reset()` does.
 *
 * `deps` covers the "reactive" case (filters change -> reset to page 1 and
 * refetch, mirrors VolunteerOpportunitiesList/OrganizationsPage's own effect
 * dependency arrays). For the "imperative" case (a scope tab, a search-submit
 * button), skip `deps` and call `reset()` directly from the event handler
 * instead, right after committing whatever changed to a state variable that
 * `fetchPage` itself closes over (not a plain local variable) - React batches
 * that state update with reset()'s, so the re-render in between picks up the
 * new value and refreshes this ref before the fetch effect reads it. `reset`
 * takes no override: the ref is unconditionally re-synced to `fetchPage`
 * every render, so an override would just get clobbered by the next one.
 */
export function useLoadMore<T>(
	fetchPage: FetchPage<T>,
	options: UseLoadMoreOptions = {},
): UseLoadMoreResult<T> {
	const { deps = [], getErrorMessage = defaultGetErrorMessage } = options;

	const [items, setItems] = useState<T[]>([]);
	const [page, setPage] = useState(1);
	const [pageCount, setPageCount] = useState(1);
	const [loading, setLoading] = useState(true);
	const [loadingMore, setLoadingMore] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [resetToken, setResetToken] = useState(0);

	const fetchPageRef = useRef(fetchPage);
	fetchPageRef.current = fetchPage;

	const isFirstDepsRun = useRef(true);
	useEffect(() => {
		if (isFirstDepsRun.current) {
			isFirstDepsRun.current = false;
			return;
		}
		setItems([]);
		setPage(1);
		setResetToken((n) => n + 1);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, deps);

	useEffect(() => {
		let cancelled = false;
		if (page > 1) setLoadingMore(true);
		else setLoading(true);
		setError(null);

		fetchPageRef
			.current(page)
			.then((result) => {
				if (cancelled) return;
				setItems((prev) =>
					page === 1 ? result.items : [...prev, ...result.items],
				);
				setPageCount(result.pageCount ?? 1);
			})
			.catch((err) => {
				if (cancelled) return;
				setError(getErrorMessage(err));
			})
			.finally(() => {
				if (cancelled) return;
				setLoading(false);
				setLoadingMore(false);
			});

		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [page, resetToken]);

	const loadMore = useCallback(() => {
		setPage((p) => p + 1);
	}, []);

	const reset = useCallback(() => {
		setItems([]);
		setError(null);
		setPage(1);
		setResetToken((n) => n + 1);
	}, []);

	return {
		items,
		setItems,
		page,
		pageCount,
		loading,
		loadingMore,
		error,
		hasMore: page < pageCount,
		loadMore,
		reset,
	};
}
