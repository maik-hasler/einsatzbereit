import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { Link, useLocation, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type { PublicOrganizationSummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { useLoadMore } from "../hooks/useLoadMore";
import { usePageDescription } from "../hooks/usePageDescription";
import { usePageTitle } from "../hooks/usePageTitle";
import { getApiErrorMessage } from "../lib/apiError";
import { cardClass } from "../lib/surfaceClasses";
import { heroSearchInputClass } from "../lib/formClasses";
import {
	reportIntentSigninArgs,
	usePendingReportIntent,
} from "../lib/reportIntent";
import EmptyState from "../components/EmptyState";
import OrgAvatar from "../components/OrgAvatar";
import Skeleton from "../components/Skeleton";
import LoadMoreError from "../components/LoadMoreError";
import LoadMoreButton from "../components/LoadMoreButton";
import ReportFlagButton from "../components/ReportFlagButton";
import RouteState from "../components/RouteState";
import PageHeaderBand from "../components/PageHeaderBand";
import Button from "../components/Button";
import { MagnifyingGlassIcon } from "../components/icons";

const PAGE_SIZE = 10;
const SEARCH_DEBOUNCE_MS = 300;

export default function OrganizationsPage() {
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	const auth = useAuth();
	const location = useLocation();
	const [searchParams, setSearchParams] = useSearchParams();
	const pendingReportTargetId = usePendingReportIntent();

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

	function handleSearchSubmit(e: FormEvent) {
		e.preventDefault();
		if (searchTimer.current) clearTimeout(searchTimer.current);
		commitSearch(searchInput);
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
				<form onSubmit={handleSearchSubmit} className="max-w-md">
					<label htmlFor="organizations-search" className="sr-only">
						{t("organizationsPage.searchLabel")}
					</label>
					<div className="flex flex-col gap-3 rounded-3xl bg-white/10 p-3 shadow-lg backdrop-blur-sm sm:flex-row sm:items-stretch sm:rounded-full">
						<div className="relative flex-1 rounded-full border border-gray-200 bg-gray-50 text-left transition-colors focus-within:border-brand-400 focus-within:bg-white">
							<MagnifyingGlassIcon className="pointer-events-none absolute top-1/2 left-4 h-4 w-4 -translate-y-1/2 text-gray-400" />
							<input
								id="organizations-search"
								data-testid="organizations-search"
								type="search"
								value={searchInput}
								onChange={(e) => handleSearchInputChange(e.target.value)}
								placeholder={t("organizationsPage.searchPlaceholder")}
								className={heroSearchInputClass}
							/>
						</div>
						<Button
							type="submit"
							size="lg"
							pill
							variant="onDark"
							className="shrink-0 shadow-md"
						>
							{t("landing.heroSearchButton")}
						</Button>
					</div>
				</form>
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
													{/*
													 * Two lines plus the full name in `title`: a single truncated
													 * line showed about a third of a 95-character name, with nothing
													 * but the stretched link's aria-label holding the rest, and empty
													 * card space right below it. `relative z-10` lifts the text out
													 * from under that link so it can still be selected and copied
													 * (#2331).
													 */}
													<h3
														title={org.name}
														className="relative z-10 line-clamp-2 text-sm font-semibold break-words text-gray-900"
													>
														{org.name}
													</h3>
												</div>
												{/* Shown to anonymous visitors too: the profile one click deeper
												already offers this to everyone and routes them through
												sign-in, so hiding it here made the affordance appear and
												disappear by page (#2326). */}
												<ReportFlagButton
													targetLabel={org.name}
													ariaLabel={t("orgProfile.reportOrganization")}
													onReport={async (reason, details) => {
														await api.reportOrganization(org.id, {
															reason,
															details: details || undefined,
														});
													}}
													onRequireSignIn={
														auth.isAuthenticated
															? undefined
															: () =>
																	void auth.signinRedirect(
																		reportIntentSigninArgs(
																			location.pathname,
																			location.search,
																			org.id,
																		),
																	)
													}
													autoOpen={
														auth.isAuthenticated &&
														pendingReportTargetId === org.id
													}
												/>
											</div>
											{/*
											 * Every line here used to be conditional, so an org with no
											 * description, no city and no open opportunities rendered a
											 * bordered card that was blank below the name - which reads as a
											 * failed render rather than a sparse organization (#2331). The
											 * description and the count now always say something.
											 */}
											<div className="min-w-0 flex-1">
												{org.description ? (
													<>
														<p
															lang="de"
															className="relative z-10 line-clamp-2 text-sm text-gray-500"
														>
															{org.description}
														</p>
														{i18n.language !== "de" && (
															<p className="relative z-10 mt-0.5 text-xs text-gray-500">
																{t("opportunities.germanOnlyNotice")}
															</p>
														)}
													</>
												) : (
													<p className="relative z-10 text-sm text-gray-500 italic">
														{t("organizationsPage.noDescription")}
													</p>
												)}
												{org.city && (
													<p className="relative z-10 mt-1 text-xs text-gray-500">
														{org.city}
													</p>
												)}
												<p
													className={`relative z-10 mt-1 text-xs font-medium ${
														org.openOpportunityCount > 0
															? "text-brand-700"
															: "text-gray-500"
													}`}
												>
													{org.openOpportunityCount > 0
														? t("organizationsPage.openOpportunities", {
																count: org.openOpportunityCount,
															})
														: t("organizationsPage.noOpenOpportunities")}
												</p>
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
