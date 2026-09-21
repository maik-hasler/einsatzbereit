// Split out of the single 1,849-line AdministrationPage: the four admin areas
// share no state, no helper and no component, so keeping them in one module
// only meant every admin route downloaded all four.

import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useApiClient } from "../../hooks/useApiClient";
import { useLoadMore } from "../../hooks/useLoadMore";
import { labelClass } from "../../lib/formClasses";
import { cardClass } from "../../lib/surfaceClasses";
import { formatDateTime, pickLocalizedText } from "../../lib/format";
import Chip from "../../components/Chip";
import Skeleton from "../../components/Skeleton";
import EmptyState from "../../components/EmptyState";
import Button from "../../components/Button";
import LoadMoreError from "../../components/LoadMoreError";
import LoadMoreButton from "../../components/LoadMoreButton";
import Dropdown from "../../components/Dropdown";
import DatePicker from "../../components/DatePicker";
import { ADMIN_PAGE_SIZE } from "./pageSize";

interface AuditLogRow {
	id: string;
	actorUserId: string;
	actorDisplayName: string;
	actionType: string;
	subjectType: string;
	subjectId: string;
	subjectDisplayName: string;
	subjectDisplayNameEn: string | undefined;
	reason: string | null;
	createdOn: string;
}

// Kept in step with Domain.AuditLogs.AuditActionType / AuditSubjectType: the API rejects any
// other value, and every entry here needs a matching `administration.auditLog.actionType.*` /
// `subjectType.*` translation, which the i18n parity check already guards.
const AUDIT_ACTION_TYPES = [
	"UserPromotedToAdmin",
	"UserDemotedFromAdmin",
	"UserEnabled",
	"UserDisabled",
	"UserShadowDeleted",
	"UserRestored",
	"OrganizationShadowDeleted",
	"OrganizationRestored",
	"VolunteerOpportunityShadowDeleted",
	"VolunteerOpportunityRestored",
	"EngagementCancelled",
	"ReportDismissed",
] as const;

const AUDIT_SUBJECT_TYPES = [
	"User",
	"Organization",
	"VolunteerOpportunity",
	"Engagement",
] as const;

/**
 * Turns a date-only value ("yyyy-MM-dd", `DatePicker`'s value shape) into the
 * instant the API filters on.
 *
 * The bounds are the admin's own local midnights - `from` inclusive, `to` exclusive, so picking
 * the same day at both ends selects exactly that day rather than an empty range.
 */
function dayBoundary(value: string, offsetDays: number): Date | undefined {
	if (!value) return undefined;
	const parsed = new Date(`${value}T00:00:00`);
	if (Number.isNaN(parsed.getTime())) return undefined;
	parsed.setDate(parsed.getDate() + offsetDays);
	return parsed;
}

function auditSubjectHref(
	subjectType: string,
	subjectId: string,
): string | null {
	switch (subjectType) {
		case "VolunteerOpportunity":
			return `/volunteer-opportunities/${subjectId}`;
		case "Organization":
			return `/organizations/${subjectId}`;
		case "User":
			return `/users/${subjectId}`;
		default:
			return null;
	}
}

function AuditLogSection() {
	const { t, i18n } = useTranslation();
	const api = useApiClient();

	const [actionType, setActionType] = useState("");
	const [subjectType, setSubjectType] = useState("");
	const [fromDate, setFromDate] = useState("");
	const [toDate, setToDate] = useState("");
	const [oldestFirst, setOldestFirst] = useState(false);
	// Not a picker: an admin selector would need the whole realm's user list, where every row
	// already names its own actor. "Only this admin" on a row is the same filter, reachable
	// from the entry that prompted the question (#2326).
	const [actor, setActor] = useState<{ id: string; name: string } | null>(null);

	const filtersActive =
		actionType !== "" ||
		subjectType !== "" ||
		fromDate !== "" ||
		toDate !== "" ||
		actor !== null;

	function clearFilters() {
		setActionType("");
		setSubjectType("");
		setFromDate("");
		setToDate("");
		setActor(null);
	}

	const {
		items: rows,
		loading,
		loadingMore,
		error,
		loadMoreError,
		hasMore,
		loadMore,
		retryLoadMore,
	} = useLoadMore<AuditLogRow>(
		(pageNumber) =>
			api
				.listAuditLogs({
					query: {
						pageNumber,
						pageSize: ADMIN_PAGE_SIZE,
						actionType: actionType || undefined,
						subjectType: subjectType || undefined,
						actorUserId: actor?.id,
						from: dayBoundary(fromDate, 0),
						to: dayBoundary(toDate, 1),
						oldestFirst,
					},
				})
				.then((result) => ({
					items: result.items.map((entry) => ({
						id: entry.id,
						actorUserId: entry.actorUserId,
						actorDisplayName: entry.actorDisplayName,
						actionType: entry.actionType,
						subjectType: entry.subjectType,
						subjectId: entry.subjectId,
						subjectDisplayName: entry.subjectDisplayName,
						subjectDisplayNameEn: entry.subjectDisplayNameEn ?? undefined,
						reason: entry.reason ?? null,
						createdOn: entry.createdOn as unknown as string,
					})),
					pageCount: result.pageCount,
				})),
		{
			deps: [actionType, subjectType, fromDate, toDate, oldestFirst, actor?.id],
			getErrorMessage: () => t("administration.auditLog.error"),
		},
	);

	const actionTypeOptions = useMemo(
		() => [
			{ value: "", label: t("administration.auditLog.filters.anyAction") },
			...AUDIT_ACTION_TYPES.map((value) => ({
				value,
				label: t(`administration.auditLog.actionType.${value}`),
			})),
		],
		[t],
	);

	const subjectTypeOptions = useMemo(
		() => [
			{ value: "", label: t("administration.auditLog.filters.anySubject") },
			...AUDIT_SUBJECT_TYPES.map((value) => ({
				value,
				label: t(`administration.auditLog.subjectType.${value}`),
			})),
		],
		[t],
	);

	const filterCard = (
		<div className={`mb-6 ${cardClass} sm:p-5`}>
			<div className="grid gap-4 sm:grid-cols-2">
				<div>
					<label htmlFor="admin-audit-action" className={labelClass}>
						{t("administration.auditLog.filters.actionLabel")}
					</label>
					<Dropdown
						id="admin-audit-action"
						value={actionType}
						onChange={setActionType}
						options={actionTypeOptions}
					/>
				</div>
				<div>
					<label htmlFor="admin-audit-subject" className={labelClass}>
						{t("administration.auditLog.filters.subjectLabel")}
					</label>
					<Dropdown
						id="admin-audit-subject"
						value={subjectType}
						onChange={setSubjectType}
						options={subjectTypeOptions}
					/>
				</div>
				<div>
					<label htmlFor="admin-audit-from" className={labelClass}>
						{t("administration.auditLog.filters.fromLabel")}
					</label>
					<DatePicker
						id="admin-audit-from"
						value={fromDate}
						max={toDate || undefined}
						onChange={setFromDate}
					/>
				</div>
				<div>
					<label htmlFor="admin-audit-to" className={labelClass}>
						{t("administration.auditLog.filters.toLabel")}
					</label>
					<DatePicker
						id="admin-audit-to"
						value={toDate}
						min={fromDate || undefined}
						onChange={setToDate}
					/>
				</div>
			</div>

			<div className="mt-4 flex flex-wrap items-center justify-between gap-3">
				<div className="flex flex-wrap items-center gap-3">
					<Button
						type="button"
						variant="outline"
						size="sm"
						onClick={() => setOldestFirst((prev) => !prev)}
						aria-pressed={oldestFirst}
					>
						{t(
							oldestFirst
								? "administration.auditLog.filters.sortOldestFirst"
								: "administration.auditLog.filters.sortNewestFirst",
						)}
					</Button>
					{actor && (
						<Button
							type="button"
							variant="outline"
							size="sm"
							onClick={() => setActor(null)}
							aria-label={t("administration.auditLog.filters.clearActorNamed", {
								name: actor.name,
							})}
						>
							{t("administration.auditLog.filters.actorChip", {
								name: actor.name,
							})}
							<span aria-hidden="true">&times;</span>
						</Button>
					)}
				</div>
				{filtersActive && (
					<Button
						type="button"
						variant="tertiary"
						size="sm"
						onClick={clearFilters}
					>
						{t("administration.clearFilters")}
					</Button>
				)}
			</div>
		</div>
	);

	if (loading) {
		return (
			<>
				{filterCard}
				<div
					role="status"
					className="overflow-hidden rounded-card border border-gray-500"
				>
					<span className="sr-only">
						{t("administration.auditLog.loading")}
					</span>
					<div className="divide-y divide-gray-100">
						{Array.from({ length: 5 }).map((_, i) => (
							<div key={i} aria-hidden="true" className="space-y-2 px-4 py-3">
								<Skeleton className="h-4 w-1/2" />
								<Skeleton className="h-3 w-1/3" />
							</div>
						))}
					</div>
				</div>
			</>
		);
	}
	if (error)
		return (
			<>
				{filterCard}
				<LoadMoreError
					message={error}
					retrying={loading}
					onRetry={retryLoadMore}
				/>
			</>
		);
	if (rows.length === 0)
		return (
			<>
				{filterCard}
				<EmptyState
					title={t(
						filtersActive
							? "administration.auditLog.noMatchesTitle"
							: "administration.auditLog.noEntries",
					)}
					message={t(
						filtersActive
							? "administration.auditLog.noMatchesMessage"
							: "administration.auditLog.noEntriesMessage",
					)}
				/>
			</>
		);

	return (
		<>
			{filterCard}
			<ul className="divide-y divide-gray-100 overflow-hidden rounded-card border border-gray-500">
				{rows.map((row) => {
					const href = auditSubjectHref(row.subjectType, row.subjectId);
					// Same per-language handling as the moderation queue, and the same restriction:
					// an Engagement's subject label is the opportunity's title, so those two are
					// the only bilingual cases here (#2326).
					const localizedSubject = pickLocalizedText(
						row.subjectDisplayName,
						row.subjectDisplayNameEn,
						i18n.language,
					);
					const subjectLabel = localizedSubject.text || row.subjectId;
					const subjectLang =
						row.subjectType === "VolunteerOpportunity" ||
						row.subjectType === "Engagement"
							? localizedSubject.lang
							: undefined;
					const actorName = row.actorDisplayName || row.actorUserId;
					const isActorFiltered = actor?.id === row.actorUserId;
					return (
						<li key={row.id} className="px-4 py-3">
							<div className="flex flex-wrap items-center gap-2">
								<span className="font-medium text-gray-900">
									{t(`administration.auditLog.actionType.${row.actionType}`)}
								</span>
								<Chip tone="neutral" size="sm">
									{t(`administration.auditLog.subjectType.${row.subjectType}`)}
								</Chip>
								{href ? (
									<Link
										to={href}
										lang={subjectLang}
										className="text-sm text-brand-700 hover:underline"
									>
										{subjectLabel}
									</Link>
								) : (
									<span lang={subjectLang} className="text-sm text-gray-500">
										{subjectLabel}
									</span>
								)}
							</div>
							<p className="mt-1 text-xs text-gray-500">
								{actorName}
								{" · "}
								{formatDateTime(row.createdOn, i18n.language)}
								{row.reason && (
									<>
										{" · "}
										{t("administration.auditLog.reason", {
											reason: row.reason,
										})}
									</>
								)}
							</p>
							{!isActorFiltered && (
								<button
									type="button"
									onClick={() =>
										setActor({ id: row.actorUserId, name: actorName })
									}
									className="mt-1 text-xs font-medium text-brand-700 hover:underline"
								>
									{t("administration.auditLog.filters.onlyThisAdmin", {
										name: actorName,
									})}
								</button>
							)}
						</li>
					);
				})}
			</ul>
			{hasMore &&
				(loadMoreError ? (
					<LoadMoreError
						message={loadMoreError}
						retrying={loadingMore}
						onRetry={retryLoadMore}
					/>
				) : (
					<LoadMoreButton
						loading={loadingMore}
						label={t("administration.auditLog.loadMore")}
						loadingLabel={t("administration.loadingMore")}
						onClick={loadMore}
					/>
				))}
		</>
	);
}

export default function AdminAuditLogPage() {
	const { t } = useTranslation();
	return (
		<>
			<p className="mb-4 text-sm text-gray-500">
				{t("administration.auditLog.scopeDescription")}
			</p>
			<AuditLogSection />
		</>
	);
}
