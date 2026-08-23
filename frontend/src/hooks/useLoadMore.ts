import { useCallback, useEffect, useRef, useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import { useOnlineStatus } from "./useOnlineStatus";
import { isNetworkError } from "../lib/apiError";

export interface LoadMorePage<T> {
	items: T[];
	pageCount?: number;
}

export type FetchPage<T> = (page: number) => Promise<LoadMorePage<T>>;

export interface UseLoadMoreOptions {
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

	errorIsOffline: boolean;

	loadMoreError: string | null;

	loadMoreErrorIsOffline: boolean;
	hasMore: boolean;
	loadMore: () => void;

	retryLoadMore: () => void;
	reset: () => void;
}

function defaultGetErrorMessage(error: unknown): string {
	return error instanceof Error
		? error.message
		: "An unexpected error occurred.";
}

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
	const [errorIsNetworkFailure, setErrorIsNetworkFailure] = useState(false);
	const [loadMoreError, setLoadMoreError] = useState<string | null>(null);
	const [loadMoreErrorIsNetworkFailure, setLoadMoreErrorIsNetworkFailure] =
		useState(false);
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
			setErrorIsNetworkFailure(false);
		} else {
			setLoadingMore(true);
		}
		setLoadMoreError(null);
		setLoadMoreErrorIsNetworkFailure(false);

		fetchPageRef
			.current(page)
			.then((result) => {
				if (cancelled) return;
				setItems((prev) =>
					isInitialLoad ? result.items : [...prev, ...result.items],
				);
				const newPageCount = result.pageCount ?? 1;
				setPageCount(newPageCount);

				setHasMore(page < newPageCount);
			})
			.catch((err) => {
				if (cancelled) return;
				if (isInitialLoad) {
					setError(getErrorMessage(err));
					setErrorIsNetworkFailure(isNetworkError(err));
				} else {
					setLoadMoreError(getErrorMessage(err));
					setLoadMoreErrorIsNetworkFailure(isNetworkError(err));
				}
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

	const online = useOnlineStatus();
	const hasFailure = error !== null || loadMoreError !== null;
	const wasOnlineRef = useRef(online);
	useEffect(() => {
		const cameBackOnline = online && !wasOnlineRef.current;
		wasOnlineRef.current = online;
		if (cameBackOnline && hasFailure) setRetryToken((n) => n + 1);
	}, [online, hasFailure]);

	const errorIsOffline = error !== null && (!online || errorIsNetworkFailure);
	const loadMoreErrorIsOffline =
		loadMoreError !== null && (!online || loadMoreErrorIsNetworkFailure);

	const loadMore = useCallback(() => {
		setPage((p) => p + 1);
	}, []);

	const retryLoadMore = useCallback(() => {
		setRetryToken((n) => n + 1);
	}, []);

	const reset = useCallback(() => {
		setItems([]);
		setError(null);
		setErrorIsNetworkFailure(false);
		setLoadMoreError(null);
		setLoadMoreErrorIsNetworkFailure(false);
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
		errorIsOffline,
		loadMoreError,
		loadMoreErrorIsOffline,
		hasMore,
		loadMore,
		retryLoadMore,
		reset,
	};
}
