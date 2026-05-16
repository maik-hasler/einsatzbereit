import { useEffect, useRef, useState } from "react";
import { useNavigate, Link, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { getActiveOrgId } from "../lib/activeOrg";
import { formatOccurrence, formatParticipationType } from "../lib/format";
import CreateVolunteerOpportunityModal from "./CreateVolunteerOpportunityModal";

interface Props {
	canCreateOpportunity: boolean;
}

export default function VolunteerOpportunitiesList({
	canCreateOpportunity,
}: Props) {
	const api = useApiClient();
	const navigate = useNavigate();
	const { t } = useTranslation();
	const [searchParams, setSearchParams] = useSearchParams();
	const search = searchParams.get("search") ?? "";
	const city = searchParams.get("city") ?? "";
	const occurrence = searchParams.get("occurrence") ?? "";
	const participationType = searchParams.get("participationType") ?? "";

	const [items, setItems] = useState<VolunteerOpportunitySummary[]>([]);
	const [page, setPage] = useState(1);
	const [pageCount, setPageCount] = useState(1);
	const [loading, setLoading] = useState(true);
	const [loadingMore, setLoadingMore] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [refreshKey, setRefreshKey] = useState(0);
	const [showModal, setShowModal] = useState(false);

	const prevFiltersRef = useRef({
		search,
		city,
		occurrence,
		participationType,
		refreshKey,
	});

	useEffect(() => {
		const prev = prevFiltersRef.current;
		const filterChanged =
			prev.search !== search ||
			prev.city !== city ||
			prev.occurrence !== occurrence ||
			prev.participationType !== participationType ||
			prev.refreshKey !== refreshKey;

		prevFiltersRef.current = {
			search,
			city,
			occurrence,
			participationType,
			refreshKey,
		};

		if (filterChanged) {
			setItems([]);
			if (page !== 1) {
				setPage(1);
				return;
			}
		}

		if (page > 1) setLoadingMore(true);
		else setLoading(true);
		setError(null);

		let cancelled = false;
		api
			.getVolunteerOpportunities(
				page,
				10,
				search || undefined,
				city || undefined,
				occurrence || undefined,
				participationType || undefined,
			)
			.then((result) => {
				if (cancelled) return;
				if (page === 1) setItems(result.items);
				else setItems((prev) => [...prev, ...result.items]);
				setPageCount(result.pageCount ?? 1);
				setLoading(false);
				setLoadingMore(false);
			})
			.catch((err: Error) => {
				if (cancelled) return;
				setError(err.message);
				setLoading(false);
				setLoadingMore(false);
			});

		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [page, search, city, occurrence, participationType, refreshKey]);

	const activeOrgId = getActiveOrgId();

	function updateFilter(key: string, value: string) {
		setSearchParams(
			(prev) => {
				const next = new URLSearchParams(prev);
				if (value) next.set(key, value);
				else next.delete(key);
				return next;
			},
			{ replace: true },
		);
	}

	return (
		<div>
			<div className="mb-4 flex items-center justify-between">
				<h2 className="text-xl font-semibold">
					{t("opportunities.currentNeeds")}
				</h2>
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

			<div className="mb-4 flex flex-wrap gap-2">
				<input
					type="text"
					placeholder={t("opportunities.searchPlaceholder")}
					value={search}
					onChange={(e) => updateFilter("search", e.target.value)}
					className="rounded border px-3 py-1.5 text-sm"
				/>
				<input
					type="text"
					placeholder={t("opportunities.cityPlaceholder")}
					value={city}
					onChange={(e) => updateFilter("city", e.target.value)}
					className="rounded border px-3 py-1.5 text-sm"
				/>
				<select
					value={occurrence}
					onChange={(e) => updateFilter("occurrence", e.target.value)}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				>
					<option value="">{t("opportunities.allFrequencies")}</option>
					<option value="OneTime">{t("opportunities.oneTime")}</option>
					<option value="Recurring">{t("opportunities.recurring")}</option>
				</select>
				<select
					value={participationType}
					onChange={(e) => updateFilter("participationType", e.target.value)}
					className="rounded border px-3 py-1.5 text-sm text-gray-700"
				>
					<option value="">{t("opportunities.allTypes")}</option>
					<option value="Waitlist">{t("opportunities.waitlist")}</option>
					<option value="IndividualContact">
						{t("opportunities.individualContact")}
					</option>
				</select>
			</div>

			{loading && <p className="text-gray-500">{t("opportunities.loading")}</p>}
			{error && (
				<p className="text-red-600">
					{t("opportunities.error", { message: error })}
				</p>
			)}

			{!loading && !error && (
				<>
					{items.length === 0 ? (
						<p className="text-gray-500">{t("opportunities.noResults")}</p>
					) : (
						<ul className="space-y-3">
							{items.map((item: VolunteerOpportunitySummary) => (
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

					{items.length > 0 && page < pageCount && (
						<div className="mt-4 flex justify-center">
							<button
								onClick={() => setPage((p) => p + 1)}
								disabled={loadingMore}
								className="rounded px-4 py-2 text-sm hover:bg-gray-100 disabled:opacity-40"
							>
								{loadingMore
									? t("opportunities.loading")
									: t("opportunities.loadMore")}
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
