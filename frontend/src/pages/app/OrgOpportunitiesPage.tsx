import { useEffect, useRef, useState } from "react";
import { Link, useOutletContext, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	VolunteerOpportunityDetails,
	VolunteerOpportunitySummary,
} from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { dispatchToast } from "../../lib/toastBus";
import { getApiErrorMessage } from "../../lib/apiError";
import CreateVolunteerOpportunityModal from "../../components/CreateVolunteerOpportunityModal";
import ConfirmDialog from "../../components/ConfirmDialog";
import EmptyState from "../../components/EmptyState";
import type { OrgAppContext } from "../../layouts/OrgAppLayout";

export default function OrgOpportunitiesPage() {
	const { org } = useOutletContext<OrgAppContext>();
	const { t } = useTranslation();
	const api = useApiClient();
	const organizationId = org.id;

	const [items, setItems] = useState<VolunteerOpportunitySummary[] | null>(
		null,
	);
	const [error, setError] = useState<string | null>(null);

	const [showCreate, setShowCreate] = useState(false);
	const [editDetails, setEditDetails] =
		useState<VolunteerOpportunityDetails | null>(null);
	const [editLoadingId, setEditLoadingId] = useState<string | null>(null);
	const [publishingId, setPublishingId] = useState<string | null>(null);
	const [deleteTargetId, setDeleteTargetId] = useState<string | null>(null);
	const [deleting, setDeleting] = useState(false);
	const [deleteError, setDeleteError] = useState<string | null>(null);

	const [searchParams, setSearchParams] = useSearchParams();
	// Id of a just-saved / just-arrived-at draft to reveal, so the organizer
	// can see where a draft landed (issue #708).
	const [highlightedId, setHighlightedId] = useState<string | null>(null);
	const highlightRef = useRef<HTMLLIElement | null>(null);

	function load() {
		setError(null);
		api
			.getOrganizationOpportunities(organizationId)
			.then(setItems)
			.catch((e: unknown) =>
				setError(e instanceof Error ? e.message : String(e)),
			);
	}

	useEffect(() => {
		load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	// A draft saved from the Calendar tab navigates in with ?highlight=<id>.
	// Surface it once, then drop the param so a later refresh doesn't keep
	// re-highlighting the same row.
	useEffect(() => {
		const h = searchParams.get("highlight");
		if (!h) return;
		setHighlightedId(h);
		const next = new URLSearchParams(searchParams);
		next.delete("highlight");
		setSearchParams(next, { replace: true });
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	// Once the highlighted row has actually rendered (list loaded), scroll it
	// into view and let the highlight ring linger briefly before clearing.
	useEffect(() => {
		if (!highlightedId || !highlightRef.current) return;
		const reduceMotion = window.matchMedia(
			"(prefers-reduced-motion: reduce)",
		).matches;
		highlightRef.current.scrollIntoView({
			behavior: reduceMotion ? "auto" : "smooth",
			block: "center",
		});
		const timer = setTimeout(() => setHighlightedId(null), 2500);
		return () => clearTimeout(timer);
	}, [highlightedId, items]);

	async function openEdit(id: string) {
		setEditLoadingId(id);
		try {
			const details = await api.getVolunteerOpportunityDetails(id);
			setEditDetails(details);
		} catch (e) {
			dispatchToast("error", getApiErrorMessage(e, t("error.serverError")));
		} finally {
			setEditLoadingId(null);
		}
	}

	async function publish(id: string) {
		setPublishingId(id);
		try {
			await api.publishVolunteerOpportunity(id);
			dispatchToast("success", t("opportunities.publishSuccess"));
			load();
		} catch (e) {
			dispatchToast("error", getApiErrorMessage(e, t("error.serverError")));
		} finally {
			setPublishingId(null);
		}
	}

	function handleCreated(createdDraftId?: string) {
		load();
		if (createdDraftId) setHighlightedId(createdDraftId);
	}

	function handleEdited() {
		setEditDetails(null);
		load();
	}

	async function handleDeleteConfirm() {
		if (!deleteTargetId) return;
		setDeleting(true);
		setDeleteError(null);
		try {
			await api.deleteVolunteerOpportunity(deleteTargetId);
			setDeleteTargetId(null);
			load();
		} catch (err) {
			setDeleteError(
				err instanceof Error ? err.message : t("opportunities.deleteError"),
			);
		} finally {
			setDeleting(false);
		}
	}

	function renderRow(item: VolunteerOpportunitySummary) {
		const isDraft = item.status === "Draft";
		const isHighlighted = item.id === highlightedId;
		return (
			<li
				key={item.id}
				ref={isHighlighted ? highlightRef : null}
				data-highlighted={isHighlighted ? "true" : undefined}
				data-testid="opportunity-row"
				className={`scroll-mt-24 rounded-2xl border bg-white p-4 shadow-sm transition ${
					isHighlighted
						? "border-brand-400 ring-2 ring-brand-500 ring-offset-2"
						: "border-gray-100"
				}`}
			>
				<div className="flex items-start justify-between gap-3">
					<div className="min-w-0">
						<div className="flex items-center gap-2">
							<Link
								to={`/volunteer-opportunities/${item.id}`}
								className="truncate text-sm font-semibold text-gray-900 hover:text-brand-700 hover:underline"
							>
								{item.title || t("orgDashboard.unnamedDraft")}
							</Link>
							<span
								className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-semibold ${
									isDraft
										? "bg-amber-100 text-amber-800"
										: "bg-green-100 text-green-800"
								}`}
							>
								{isDraft
									? t("opportunities.draftBadge")
									: t("orgOpportunities.publishedBadge")}
							</span>
						</div>
						{item.description && (
							<p className="mt-0.5 line-clamp-1 text-xs text-gray-500">
								{item.description}
							</p>
						)}
						{item.totalMaxParticipants > 0 && (
							<p className="mt-1 text-xs text-gray-500">
								{t("orgOpportunities.participants", {
									booked: item.currentParticipantCount,
									max: item.totalMaxParticipants,
								})}
							</p>
						)}
					</div>
					<div className="flex shrink-0 flex-wrap items-center justify-end gap-2">
						<button
							type="button"
							onClick={() => void openEdit(item.id)}
							disabled={editLoadingId === item.id}
							data-testid="opportunity-edit"
							className="rounded-lg border border-gray-200 px-3 py-1.5 text-sm text-gray-600 transition hover:bg-gray-50 disabled:opacity-50"
						>
							{editLoadingId === item.id
								? t("orgOpportunities.editLoading")
								: t("opportunities.edit")}
						</button>
						<button
							type="button"
							onClick={() => {
								setDeleteTargetId(item.id);
								setDeleteError(null);
							}}
							data-testid="opportunity-delete"
							className="rounded-lg border border-red-200 px-3 py-1.5 text-sm text-red-600 transition hover:bg-red-50"
						>
							{t("opportunities.delete")}
						</button>
						{isDraft ? (
							<button
								type="button"
								onClick={() => void publish(item.id)}
								disabled={publishingId === item.id}
								data-testid="opportunity-publish"
								className="rounded-lg bg-brand-700 px-3 py-1.5 text-sm font-semibold text-white transition hover:bg-brand-800 disabled:opacity-50"
							>
								{publishingId === item.id
									? t("opportunities.publishing")
									: t("opportunities.publish")}
							</button>
						) : (
							<Link
								to={`/app/${organizationId}/opportunities/${item.id}/engagements`}
								className="inline-flex items-center gap-1 rounded-lg border border-gray-200 px-3 py-1.5 text-sm font-medium text-brand-700 transition hover:bg-brand-50"
							>
								{t("orgOpportunities.manageApplications")}
								<svg
									className="h-3.5 w-3.5"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="2"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3"
									/>
								</svg>
							</Link>
						)}
					</div>
				</div>
			</li>
		);
	}

	const drafts = items?.filter((i) => i.status === "Draft") ?? [];
	const published = items?.filter((i) => i.status === "Published") ?? [];

	return (
		<div>
			<div className="mb-6 flex items-center justify-between gap-3">
				<h1 className="text-2xl font-bold text-gray-900">{org.name}</h1>
				<button
					type="button"
					onClick={() => setShowCreate(true)}
					data-testid="create-opportunity-btn"
					className="inline-flex shrink-0 items-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-brand-800 focus:outline-none"
				>
					{t("orgOverview.createOpportunity")}
				</button>
			</div>

			{items === null && !error && (
				<div className="flex items-center justify-center py-16">
					<span className="text-gray-500">{t("orgOpportunities.loading")}</span>
				</div>
			)}

			{error && (
				<p className="text-red-600">
					{t("orgOpportunities.error", { message: error })}
				</p>
			)}

			{items !== null && !error && items.length === 0 && (
				<EmptyState
					title={t("orgOpportunities.emptyTitle")}
					message={t("orgOpportunities.emptyDesc")}
					action={{
						label: t("orgOverview.createOpportunity"),
						onClick: () => setShowCreate(true),
					}}
				/>
			)}

			{items !== null && !error && items.length > 0 && (
				<div className="space-y-8">
					{drafts.length > 0 && (
						<section data-testid="drafts-section">
							<h2 className="text-lg font-semibold text-gray-900">
								{t("orgOpportunities.draftsHeading")}
							</h2>
							<p className="mt-1 text-sm text-gray-500">
								{t("orgOpportunities.draftsDesc")}
							</p>
							<ul className="mt-4 space-y-3">{drafts.map(renderRow)}</ul>
						</section>
					)}

					{published.length > 0 && (
						<section data-testid="published-section">
							<h2 className="text-lg font-semibold text-gray-900">
								{t("orgOpportunities.publishedHeading")}
							</h2>
							<p className="mt-1 text-sm text-gray-500">
								{t("orgOpportunities.publishedDesc")}
							</p>
							<ul className="mt-4 space-y-3">{published.map(renderRow)}</ul>
						</section>
					)}
				</div>
			)}

			{showCreate && (
				<CreateVolunteerOpportunityModal
					organizationId={organizationId}
					onClose={() => setShowCreate(false)}
					onSuccess={handleCreated}
				/>
			)}

			{editDetails && (
				<CreateVolunteerOpportunityModal
					organizationId={organizationId}
					initialOpportunity={editDetails}
					onClose={() => setEditDetails(null)}
					onSuccess={handleEdited}
				/>
			)}

			{deleteTargetId && (
				<ConfirmDialog
					title={t("confirmDialog.delete.title")}
					message={t("confirmDialog.delete.message")}
					confirmLabel={t("confirmDialog.delete.confirm")}
					onConfirm={handleDeleteConfirm}
					onClose={() => {
						if (deleting) return;
						setDeleteTargetId(null);
						setDeleteError(null);
					}}
					loading={deleting}
					error={deleteError}
				/>
			)}
		</div>
	);
}
