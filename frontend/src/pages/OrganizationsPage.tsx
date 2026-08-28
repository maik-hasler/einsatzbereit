import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type { PublicOrganizationSummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { useLoadMore } from "../hooks/useLoadMore";
import { usePageDescription } from "../hooks/usePageDescription";
import { usePageTitle } from "../hooks/usePageTitle";
import { getApiErrorMessage } from "../lib/apiError";
import { cardClass } from "../lib/surfaceClasses";
import EmptyState from "../components/EmptyState";
import OrgAvatar from "../components/OrgAvatar";
import Skeleton from "../components/Skeleton";
import LoadMoreError from "../components/LoadMoreError";
import LoadMoreButton from "../components/LoadMoreButton";
import ReportFlagButton from "../components/ReportFlagButton";
import RouteState from "../components/RouteState";
import PageHeaderBand from "../components/PageHeaderBand";
import { MagnifyingGlassIcon } from "../components/icons";

const PAGE_SIZE = 10;
const SEARCH_DEBOUNCE_MS = 300;

export default function OrganizationsPage() {
	const api = useApiClient();
	const { t } = useTranslation();
	const auth = useAuth();
	const [searchParams, setSearchParams] = useSearchParams();

	const search = searchParams.get("q") ?? "";
	const [searchInput, setSearchInput] = useState(search);
	const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

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
	usePageDescription(t("organizationsPage.lead"));

	function commitSearch(value: string) {
		setSearchParams(
			(prev) => {
				const next = new URLSearchParams(prev);
				if (value) next.set("q", value);
				else next.delete("q");
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

			<p
				role="status"
				data-testid="organizations-result-count"
				className="sr-only"
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
						<>
							<h2 className="sr-only">
								{t("organizationsPage.resultsHeading")}
							</h2>
							<ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
								{items.map((org) => {
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
												<OrgAvatar
													name={org.name}
													logoUrl={org.logoUrl}
													size="2xl"
													lazy
												/>
												<div className="flex min-w-0 flex-1 items-center gap-2">
													<h3 className="block truncate text-sm font-semibold text-gray-900">
														{org.name}
													</h3>
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
													<p
														lang="de"
														className="line-clamp-2 text-sm text-gray-500"
													>
														{org.description}
													</p>
												)}
												{org.city && (
													<p className="mt-1 text-xs text-gray-500">
														{org.city}
													</p>
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
						</>
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
