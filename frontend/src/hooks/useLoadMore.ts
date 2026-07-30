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
	/** Set only when the page-1 (initial) fetch fails - items is empty whenever this is set. */
	error: string | null;
	/**
	 * Set only when a page>1 (load-more) fetch fails - items keeps whatever was
	 * already loaded (einsatzbereit#1226: a failed load-more used to wipe the
	 * already-loaded rows because both cases shared a single `error`).
	 */
	loadMoreError: string | null;
	hasMore: boolean;
	loadMore: () => void;
	/** Re-attempts the page that produced `loadMoreError`, without advancing further. */
	retryLoadMore: () => void;
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
 * of this exact state (see einsatzbereit#868). `error` and `loadMoreError` are
 * deliberately separate state so a load-more failure never has to hide items
 * that already rendered successfully (see einsatzbereit#1226); `retryLoadMore`
 * re-runs the fetch effect for the current `page` (already advanced past the
 * last success by `loadMore` before the failing attempt), rather than
 * advancing again.
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
	const [hasMore, setHasMore] = useState(false);
	const [loading, setLoading] = useState(true);
	const [loadingMore, setLoadingMore] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [loadMoreError, setLoadMoreError] = useState<string | null>(null);
	const [resetToken, setResetToken] = useState(0);
	const [retryToken, setRetryToken] = useState(0);

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
		const isInitialLoad = page === 1;
		if (isInitialLoad) {
			setLoading(true);
			setError(null);
		} else {
			setLoadingMore(true);
		}
		setLoadMoreError(null);

		fetchPageRef
			.current(page)
			.then((result) => {
				if (cancelled) return;
				setItems((prev) =>
					isInitialLoad ? result.items : [...prev, ...result.items],
				);
				const newPageCount = result.pageCount ?? 1;
				setPageCount(newPageCount);
				// Set only on success, not derived live from `page < pageCount`:
				// `loadMore` optimistically advances `page` before its fetch
				// resolves, so a live derivation goes false the instant the last
				// page is requested - even if that request then fails - hiding the
				// load-more/retry affordance a moment too early (einsatzbereit#1226).
				setHasMore(page < newPageCount);
			})
			.catch((err) => {
				if (cancelled) return;
				if (isInitialLoad) setError(getErrorMessage(err));
				else setLoadMoreError(getErrorMessage(err));
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
	}, [page, resetToken, retryToken]);

	const loadMore = useCallback(() => {
		setPage((p) => p + 1);
	}, []);

	const retryLoadMore = useCallback(() => {
		setRetryToken((n) => n + 1);
	}, []);

	const reset = useCallback(() => {
		setItems([]);
		setError(null);
		setLoadMoreError(null);
		setPage(1);
		setHasMore(false);
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
		loadMoreError,
		hasMore,
		loadMore,
		retryLoadMore,
		reset,
	};
}
