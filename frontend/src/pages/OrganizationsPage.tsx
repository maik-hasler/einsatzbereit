import { useRef, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type { PublicOrganizationSummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { useLoadMore } from "../hooks/useLoadMore";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { getApiErrorMessage } from "../lib/apiError";
import { avatarColorClasses } from "../lib/avatarColor";
import { inputClass } from "../lib/formClasses";
import { pageTitleClass } from "../lib/headingClasses";
import EmptyState from "../components/EmptyState";
import Skeleton from "../components/Skeleton";
import ErrorBanner from "../components/ErrorBanner";
import LoadMoreError from "../components/LoadMoreError";
import ReportFlagButton from "../components/ReportFlagButton";

const PAGE_SIZE = 10;
const SEARCH_DEBOUNCE_MS = 300;

export default function OrganizationsPage() {
	const api = useApiClient();
	const { t } = useTranslation();
	const auth = useAuth();
	const [searchParams, setSearchParams] = useSearchParams();

	const search = searchParams.get("search") ?? "";
	const [searchInput, setSearchInput] = useState(search);
	const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

	const {
		items,
		loading,
		loadingMore,
		error,
		hasMore,
		loadMore,
		loadMoreError,
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
	usePageToolbar([{ label: t("breadcrumb.organizations") }]);

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

	return (
		<div>
			<h1 className={`text-gray-900 ${pageTitleClass}`}>
				{t("organizationsPage.title")}
			</h1>
			<p className="mt-1 text-sm text-gray-500">
				{t("organizationsPage.subtitle")}
			</p>

			<div className="mt-6">
				<label htmlFor="organizations-search" className="sr-only">
					{t("organizationsPage.searchPlaceholder")}
				</label>
				<input
					id="organizations-search"
					type="search"
					value={searchInput}
					onChange={(e) => handleSearchInputChange(e.target.value)}
					placeholder={t("organizationsPage.searchPlaceholder")}
					className={`${inputClass} max-w-md`}
				/>
			</div>

			{/* Always mounted (not conditional on the message) so the live region
			is registered before it ever gets content - see CheckInModal.tsx's
			identical pattern for why. Silent during the initial loading
			skeleton and on error; otherwise announces the settled result count
			whenever the debounced search rewrites the directory - previously
			nothing did. */}
			<p role="status" className="sr-only">
				{!loading && !error
					? t("organizationsPage.resultCount", { count: items.length })
					: ""}
			</p>

			<div className="mt-6">
				{loading ? (
					<div
						role="status"
						className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3"
					>
						<span className="sr-only">{t("organizationsPage.loading")}</span>
						{Array.from({ length: 3 }).map((_, i) => (
							<div
								key={i}
								aria-hidden="true"
								className="flex flex-col gap-3 rounded-card border border-gray-100 bg-white p-4 shadow-resting"
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
				) : error ? (
					<ErrorBanner
						message={t("organizationsPage.error", { message: error })}
					/>
				) : items.length === 0 ? (
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
						<ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
							{items.map((org) => {
								const avatarColor = avatarColorClasses(org.id);
								return (
									<li
										key={org.id}
										className="relative flex h-full flex-col gap-3 rounded-card border border-gray-100 bg-white p-4 shadow-resting transition-shadow hover:shadow-raised"
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
													{org.name.charAt(0).toUpperCase()}
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

						{hasMore &&
							(loadMoreError ? (
								<LoadMoreError
									message={t("organizationsPage.error", {
										message: loadMoreError,
									})}
									retrying={loadingMore}
									onRetry={retryLoadMore}
								/>
							) : (
								<div className="mt-8 flex justify-center">
									<button
										onClick={loadMore}
										disabled={loadingMore}
										className="rounded-xl border border-brand-200 bg-brand-50 px-8 py-3 text-sm font-semibold text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-40"
									>
										{loadingMore
											? t("organizationsPage.loading")
											: t("organizationsPage.loadMore")}
									</button>
								</div>
							))}
					</>
				)}
			</div>
		</div>
	);
}
