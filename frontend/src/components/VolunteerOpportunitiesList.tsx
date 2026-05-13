import { useEffect, useState, useCallback } from "react";
import { useNavigate, Link } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	PagedListOfVolunteerOpportunitySummary,
	VolunteerOpportunitySummary,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import {
	useOpportunityFilters,
	type OpportunityBounds,
} from "../hooks/useOpportunityFilters";
import { getActiveOrgId } from "../lib/activeOrg";
import { formatOccurrence, formatParticipationType } from "../lib/format";
import CreateVolunteerOpportunityModal from "./CreateVolunteerOpportunityModal";
import OpportunityMap from "./OpportunityMap";

interface Props {
	canCreateOpportunity: boolean;
}

const LIST_PAGE_SIZE = 10;
const MAP_PAGE_SIZE = 200;

export default function VolunteerOpportunitiesList({
	canCreateOpportunity,
}: Props) {
	const api = useApiClient();
	const navigate = useNavigate();
	const { t } = useTranslation();
	const { filters, update, clear } = useOpportunityFilters();

	const [data, setData] =
		useState<PagedListOfVolunteerOpportunitySummary | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [page, setPage] = useState(1);
	const [refreshKey, setRefreshKey] = useState(0);
	const [showModal, setShowModal] = useState(false);

	const isMap = filters.view === "map";

	useEffect(() => {
		setPage(1);
	}, [
		filters.search,
		filters.city,
		filters.occurrence,
		filters.participationType,
		filters.isRemote,
		filters.dateFrom,
		filters.dateTo,
	]);

	useEffect(() => {
		setLoading(true);
		setError(null);

		const dateFrom = filters.dateFrom ? new Date(filters.dateFrom) : undefined;
		const dateTo = filters.dateTo ? new Date(filters.dateTo) : undefined;
		const bounds = isMap ? filters.bounds : undefined;

		api
			.getVolunteerOpportunities(
				isMap ? 1 : page,
				isMap ? MAP_PAGE_SIZE : LIST_PAGE_SIZE,
				filters.search || undefined,
				filters.city || undefined,
				filters.occurrence || undefined,
				filters.participationType || undefined,
				filters.isRemote,
				dateFrom,
				dateTo,
				bounds?.north,
				bounds?.south,
				bounds?.east,
				bounds?.west,
				undefined,
				undefined,
				undefined,
			)
			.then((json: PagedListOfVolunteerOpportunitySummary) => setData(json))
			.catch((err: Error) => setError(err.message))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [
		page,
		refreshKey,
		filters.search,
		filters.city,
		filters.occurrence,
		filters.participationType,
		filters.isRemote,
		filters.dateFrom,
		filters.dateTo,
		isMap,
		filters.bounds?.north,
		filters.bounds?.south,
		filters.bounds?.east,
		filters.bounds?.west,
	]);

	const activeOrgId = getActiveOrgId();

	const handleBoundsChange = useCallback(
		(bounds: OpportunityBounds) => {
			update({ bounds });
		},
		[update],
	);

	const remoteValue =
		filters.isRemote === undefined
			? ""
			: filters.isRemote
				? "remote"
				: "onsite";

	return (
		<div>
			<div className="mb-4 flex items-center justify-between">
				<h2 className="text-xl font-semibold">
					{t("opportunities.currentNeeds")}
				</h2>
				<div className="flex items-center gap-2">
					<div
						role="group"
						className="inline-flex overflow-hidden rounded border"
					>
						<button
							type="button"
							data-testid="view-toggle-list"
							onClick={() => update({ view: "list" })}
							className={
								!isMap
									? "bg-black px-3 py-1.5 text-sm text-white"
									: "px-3 py-1.5 text-sm hover:bg-gray-100"
							}
						>
							{t("opportunities.view.list")}
						</button>
						<button
							type="button"
							data-testid="view-toggle-map"
							onClick={() => update({ view: "map" })}
							className={
								isMap
									? "bg-black px-3 py-1.5 text-sm text-white"
									: "px-3 py-1.5 text-sm hover:bg-gray-100"
							}
						>
							{t("opportunities.view.map")}
						</button>
					</div>
					{canCreateOpportunity && (
						<button
							onClick={() => setShowModal(true)}
							data-testid="create-opportunity-btn"
							className="rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800"
						>
							{t("opportunities.createNeed")}
						</button>
					)}
				</div>
			</div>

			<div className="mb-4 flex flex-wrap items-center gap-2">
				<input
					type="text"
					placeholder={t("opportunities.searchPlaceholder")}
					value={filters.search}
					onChange={(e) => update({ search: e.target.value })}
					className="rounded border px-3 py-1.5 text-sm"
				/>
				<input
					type="text"
					placeholder={t("opportunities.cityPlaceholder")}
					value={filters.city}
					onChange={(e) => update({ city: e.target.value })}
					className="rounded border px-3 py-1.5 text-sm"
				/>
				<select
					value={filters.occurrence}
					onChange={(e) => update({ occurrence: e.target.value })}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				>
					<option value="">{t("opportunities.allFrequencies")}</option>
					<option value="OneTime">{t("opportunities.oneTime")}</option>
					<option value="Recurring">{t("opportunities.recurring")}</option>
				</select>
				<select
					value={filters.participationType}
					onChange={(e) => update({ participationType: e.target.value })}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				>
					<option value="">{t("opportunities.allTypes")}</option>
					<option value="Waitlist">{t("opportunities.waitlist")}</option>
					<option value="IndividualContact">
						{t("opportunities.individualContact")}
					</option>
				</select>
				<select
					value={remoteValue}
					onChange={(e) => {
						const v = e.target.value;
						update({
							isRemote:
								v === "remote" ? true : v === "onsite" ? false : undefined,
						});
					}}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				>
					<option value="">{t("opportunities.allLocations")}</option>
					<option value="onsite">{t("opportunities.onsite")}</option>
					<option value="remote">{t("opportunities.remote")}</option>
				</select>
				<input
					type="date"
					value={filters.dateFrom}
					onChange={(e) => update({ dateFrom: e.target.value })}
					aria-label={t("opportunities.dateFromLabel")}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				/>
				<input
					type="date"
					value={filters.dateTo}
					onChange={(e) => update({ dateTo: e.target.value })}
					aria-label={t("opportunities.dateToLabel")}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				/>
				<button
					type="button"
					onClick={clear}
					data-testid="clear-filters"
					className="rounded border px-3 py-1.5 text-sm hover:bg-gray-100"
				>
					{t("opportunities.clearFilters")}
				</button>
			</div>

			{loading && <p className="text-gray-500">{t("opportunities.loading")}</p>}
			{error && (
				<p className="text-red-600">
					{t("opportunities.error", { message: error })}
				</p>
			)}

			{!loading && !error && data && (
				<>
					{isMap && (
						<OpportunityMap
							items={data.items}
							bounds={filters.bounds}
							onBoundsChange={handleBoundsChange}
						/>
					)}

					{data.items.length === 0 ? (
						<p className="mt-4 text-gray-500">
							{isMap ? t("map.noPinsInView") : t("opportunities.noResults")}
						</p>
					) : (
						<ul className={isMap ? "mt-4 space-y-3" : "space-y-3"}>
							{data.items.map((item: VolunteerOpportunitySummary) => (
								<li
									key={item.id}
									className="cursor-pointer rounded border p-4 hover:bg-gray-50 transition-colors"
									onClick={() =>
										navigate(`/volunteer-opportunities/${item.id}`)
									}
								>
									<div className="flex items-start justify-between">
										<div>
											<strong className="block text-sm font-medium">
												{item.title}
											</strong>
											<p className="mt-1 text-sm text-gray-600">
												{item.description}
											</p>
										</div>
										<div className="flex flex-col items-end gap-1 shrink-0 ml-2">
											<span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
												{formatOccurrence(item.occurrence, t)}
											</span>
											<span className="rounded-full bg-blue-50 px-2 py-0.5 text-xs text-blue-700">
												{formatParticipationType(item.participationType, t)}
											</span>
										</div>
									</div>
									<div className="mt-2 flex items-center gap-4 text-xs text-gray-500">
										<Link
											to={`/organizations/${item.organizationId}`}
											className="hover:underline"
											onClick={(e) => e.stopPropagation()}
										>
											{item.organizationName}
										</Link>
										{item.isRemote ? (
											<span>{t("opportunities.remote")}</span>
										) : (
											<span>
												{item.street} {item.houseNumber}, {item.zipCode}{" "}
												{item.city}
											</span>
										)}
									</div>
								</li>
							))}
						</ul>
					)}

					{!isMap && (data.pageCount ?? 1) > 1 && (
						<div className="mt-4 flex items-center gap-3">
							<button
								onClick={() => setPage((p) => p - 1)}
								disabled={page <= 1}
								className="rounded px-3 py-1 text-sm hover:bg-gray-100 disabled:opacity-40"
							>
								{t("opportunities.previous")}
							</button>
							<span className="text-sm text-gray-500">
								{t("opportunities.page", {
									current: page,
									total: data.pageCount,
								})}
							</span>
							<button
								onClick={() => setPage((p) => p + 1)}
								disabled={page >= (data.pageCount ?? 1)}
								className="rounded px-3 py-1 text-sm hover:bg-gray-100 disabled:opacity-40"
							>
								{t("opportunities.next")}
							</button>
						</div>
					)}
				</>
			)}

			{showModal && activeOrgId && (
				<CreateVolunteerOpportunityModal
					organizationId={activeOrgId}
					onClose={() => setShowModal(false)}
					onSuccess={() => setRefreshKey((k) => k + 1)}
				/>
			)}
		</div>
	);
}
