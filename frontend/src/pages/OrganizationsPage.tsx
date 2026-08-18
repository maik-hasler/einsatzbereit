import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type { PublicOrganizationSummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { useLoadMore } from "../hooks/useLoadMore";
import { usePageTitle } from "../hooks/usePageTitle";
import { getApiErrorMessage } from "../lib/apiError";
import { avatarColorClasses } from "../lib/avatarColor";
import { getInitials } from "../lib/initials";
import { cardClass } from "../lib/surfaceClasses";
import EmptyState from "../components/EmptyState";
import Skeleton from "../components/Skeleton";
import LoadMoreError from "../components/LoadMoreError";
import LoadMoreButton from "../components/LoadMoreButton";
import ReportFlagButton from "../components/ReportFlagButton";
import RouteState from "../components/RouteState";
import PageHeaderBand from "../components/PageHeaderBand";
import { MagnifyingGlassIcon } from "../components/icons";

const PAGE_SIZE = 10;
const SEARCH_DEBOUNCE_MS = 300;

// The public organization directory - restored per #1852 after it was
// silently dropped as unreviewed collateral damage from #1751's footer
// redesign (it had originally shipped in #772/#763). Rebuilt against the
// PageHeaderBand + offline-aware list conventions #1755/#1774 established
// after the original page was written, rather than reintroducing the old
// usePageToolbar breadcrumb bar those retired.
export default function OrganizationsPage() {
	const api = useApiClient();
	const { t } = useTranslation();
	const auth = useAuth();
	const [searchParams, setSearchParams] = useSearchParams();

	const search = searchParams.get("search") ?? "";
	const [searchInput, setSearchInput] = useState(search);
	const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

	// Without this, typing then navigating away within the debounce window
	// (SEARCH_DEBOUNCE_MS) still fires commitSearch after the route change,
	// rewriting whatever page the user navigated to with a stray ?search=
	// param (#1239).
	useEffect(() => {
		return () => {
			if (searchTimer.current) clearTimeout(searchTimer.current);
		};
	}, []);

	const {
		items,
		loading,
		loadingMore,
		error,
		errorIsOffline,
		hasMore,
		loadMore,
		loadMoreError,
		loadMoreErrorIsOffline,
		retryLoadMore,
	} = useLoadMore<PublicOrganizationSummary>(
		(pageNumber) =>
			api.getPublicOrganizations(pageNumber, PAGE_SIZE, search || undefined),
		{
			deps: [search],
			getErrorMessage: (err) => getApiErrorMessage(err, t("error.serverError")),
		},
	);

	usePageTitle(t("organizationsPage.title"));

	function commitSearch(value: string) {
		setSearchParams(
			(prev) => {
				const next = new URLSearchParams(prev);
				if (value) next.set("search", value);
				else next.delete("search");
				return next;
			},
			{ replace: true },
		);
	}

	function handleSearchInputChange(value: string) {
		setSearchInput(value);
		if (searchTimer.current) clearTimeout(searchTimer.current);
		searchTimer.current = setTimeout(
			() => commitSearch(value),
			SEARCH_DEBOUNCE_MS,
		);
	}

	function handleClearSearch() {
		if (searchTimer.current) clearTimeout(searchTimer.current);
		setSearchInput("");
		commitSearch("");
	}

	const isInitialLoad = loading && items.length === 0;
	const countMessage =
		!error && !isInitialLoad
			? hasMore
				? t("organizationsPage.resultCountPartial", { count: items.length })
				: t("organizationsPage.resultCount", { count: items.length })
			: "";

	// Same online-vs-offline split as OpportunityResultsList (#1774/#1901):
	// while offline, this one live region carries the offline announcement
	// instead of a result count.
	const liveMessage =
		error && errorIsOffline ? t("organizationsPage.offline") : countMessage;

	return (
		<>
			<PageHeaderBand
				eyebrow={t("organizationsPage.eyebrow")}
				title={t("organizationsPage.title")}
				lead={t("organizationsPage.lead")}
			>
				<div className="max-w-md">
					<label htmlFor="organizations-search" className="sr-only">
						{t("organizationsPage.searchLabel")}
					</label>
					<div className="relative">
						<MagnifyingGlassIcon className="pointer-events-none absolute top-1/2 left-4 h-4 w-4 -translate-y-1/2 text-white/60" />
						<input
							id="organizations-search"
							data-testid="organizations-search"
							type="search"
							value={searchInput}
							onChange={(e) => handleSearchInputChange(e.target.value)}
							placeholder={t("organizationsPage.searchPlaceholder")}
							className="w-full rounded-full border border-white/20 bg-white/10 py-3 pr-4 pl-10 text-sm text-white backdrop-blur-sm placeholder:text-white/70 focus:border-white/40 focus:outline-none"
						/>
					</div>
				</div>
			</PageHeaderBand>

			{/* Always mounted (not conditional on the message) so the live region
			is registered before it ever gets content - see CheckInModal.tsx's
			identical pattern for why. Silent during the initial loading
			skeleton and on error; otherwise announces the settled result count
			whenever the debounced search rewrites the directory. */}
			<p
				role="status"
				data-testid="organizations-result-count"
				className={
					!error && items.length > 0
						? "mb-4 text-center text-sm text-gray-600"
						: "sr-only"
				}
			>
				{liveMessage}
			</p>

			{isInitialLoad && (
				<div
					role="status"
					className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3"
				>
					<span className="sr-only">{t("organizationsPage.loading")}</span>
					{Array.from({ length: 3 }).map((_, i) => (
						<div
							key={i}
							aria-hidden="true"
							className={`flex flex-col gap-3 ${cardClass}`}
						>
							<div className="flex items-center gap-3">
								<Skeleton className="h-12 w-12 shrink-0 rounded-full" />
								<div className="flex-1 space-y-2">
									<Skeleton className="h-4 w-2/3" />
								</div>
							</div>
							<Skeleton className="h-3 w-1/2" />
						</div>
					))}
				</div>
			)}

			{error &&
				(errorIsOffline ? (
					<RouteState
						inline
						variant="offline"
						title={t("routeState.offline.title")}
						message={t("organizationsPage.offline")}
						onRetry={retryLoadMore}
						data-testid="organizations-offline"
					/>
				) : (
					<LoadMoreError
						message={t("organizationsPage.error", { message: error })}
						retrying={loading}
						onRetry={retryLoadMore}
						data-testid="organizations-error"
					/>
				))}

			{!error && (
				<>
					{!loading && items.length === 0 ? (
						<EmptyState
							title={
								search
									? t("organizationsPage.noResultsWithSearch", { search })
									: t("organizationsPage.noResults")
							}
							action={
								search
									? {
											label: t("organizationsPage.clearSearch"),
											onClick: handleClearSearch,
										}
									: undefined
							}
						/>
					) : (
						<ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
							{items.map((org) => {
								const avatarColor = avatarColorClasses(org.id);
								return (
									<li
										key={org.id}
										className={`relative flex h-full flex-col gap-3 ${cardClass} transition-shadow hover:shadow-raised`}
									>
										<Link
											to={`/organizations/${org.id}`}
											className="absolute inset-0 rounded-card"
											aria-label={org.name}
										/>
										<div className="flex items-center gap-3">
											{org.logoUrl ? (
												<img
													src={org.logoUrl}
													alt=""
													width={48}
													height={48}
													loading="lazy"
													className="h-12 w-12 shrink-0 rounded-full object-cover"
												/>
											) : (
												<span
													className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-full text-lg font-semibold ${avatarColor.bg} ${avatarColor.text}`}
												>
													{getInitials(org.name)}
												</span>
											)}
											<div className="flex min-w-0 flex-1 items-center gap-2">
												<strong className="block truncate text-sm font-semibold text-gray-900">
													{org.name}
												</strong>
											</div>
											{auth.isAuthenticated && (
												<ReportFlagButton
													targetLabel={org.name}
													ariaLabel={t("orgProfile.reportOrganization")}
													onReport={async (reason, details) => {
														await api.reportOrganization(org.id, {
															reason,
															details: details || undefined,
														});
													}}
												/>
											)}
										</div>
										<div className="min-w-0 flex-1">
											{org.description && (
												<p className="line-clamp-2 text-sm text-gray-500">
													{org.description}
												</p>
											)}
											{org.city && (
												<p className="mt-1 text-xs text-gray-500">{org.city}</p>
											)}
											{org.openOpportunityCount > 0 && (
												<p className="mt-1 text-xs font-medium text-brand-700">
													{t("organizationsPage.openOpportunities", {
														count: org.openOpportunityCount,
													})}
												</p>
											)}
										</div>
									</li>
								);
							})}
						</ul>
					)}

					{items.length > 0 &&
						hasMore &&
						(loadMoreError ? (
							loadMoreErrorIsOffline ? (
								<RouteState
									inline
									variant="offline"
									title={t("routeState.offline.title")}
									message={t("organizationsPage.offlineLoadMore")}
									onRetry={retryLoadMore}
									data-testid="organizations-offline-load-more"
								/>
							) : (
								<LoadMoreError
									message={t("organizationsPage.error", {
										message: loadMoreError,
									})}
									retrying={loadingMore}
									onRetry={retryLoadMore}
								/>
							)
						) : (
							<LoadMoreButton
								loading={loadingMore}
								label={t("organizationsPage.loadMore")}
								loadingLabel={t("organizationsPage.loading")}
								onClick={loadMore}
							/>
						))}
				</>
			)}
		</>
	);
}
