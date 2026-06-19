import { useEffect, useState } from "react";
import { useParams, useNavigate, Link } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation, Trans } from "react-i18next";
import type { VolunteerOpportunityDetails } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import {
	formatDateTime,
	formatOccurrence,
	formatParticipationType,
} from "../lib/format";
import SignUpModal from "../components/SignUpModal";
import CreateVolunteerOpportunityModal from "../components/CreateVolunteerOpportunityModal";
import ConfirmDialog from "../components/ConfirmDialog";
import SingleMarkerMap from "../components/SingleMarkerMap";
import { usePageTitle } from "../hooks/usePageTitle";
import { dispatchToast } from "../lib/toastBus";
import { getApiErrorMessage } from "../lib/apiError";
import { runtimeConfig } from "../lib/runtimeConfig";

export default function VolunteerOpportunityDetailPage() {
	const { opportunityId } = useParams<{ opportunityId: string }>();
	const navigate = useNavigate();
	const auth = useAuth();
	const api = useApiClient();
	const { t, i18n } = useTranslation();

	const [opportunity, setOpportunity] =
		useState<VolunteerOpportunityDetails | null>(null);
	usePageTitle(opportunity?.title);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [showSignUp, setShowSignUp] = useState(false);
	const [signedUp, setSignedUp] = useState(false);
	const [showEdit, setShowEdit] = useState(false);
	const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
	const [deleting, setDeleting] = useState(false);
	const [deleteError, setDeleteError] = useState<string | null>(null);
	const [publishing, setPublishing] = useState(false);

	const roles = (
		Array.isArray(auth.user?.profile?.roles) ? auth.user?.profile?.roles : []
	) as string[];
	const isOrganisator = roles.includes("organisator");
	const [userOrgIds, setUserOrgIds] = useState<string[]>([]);

	useEffect(() => {
		if (!isOrganisator) return;
		api
			.getOrganizations()
			.then((orgs) => setUserOrgIds(orgs.map((o) => o.id)))
			.catch(() => {});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [isOrganisator]);

	useEffect(() => {
		if (!opportunityId) return;
		load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [opportunityId]);

	function load() {
		if (!opportunityId) return;
		setLoading(true);
		api
			.getVolunteerOpportunityDetails(opportunityId)
			.then(setOpportunity)
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
	}

	async function handleShare() {
		const url = window.location.href;
		try {
			if (navigator.share) {
				await navigator.share({
					title: opportunity?.title ?? document.title,
					url,
				});
			} else {
				await navigator.clipboard.writeText(url);
				dispatchToast("success", t("opportunities.linkCopied"));
			}
		} catch {
			// User dismissed the share sheet or clipboard access was denied - ignore.
		}
	}

	async function handleDeleteConfirm() {
		if (!opportunityId) return;
		setDeleting(true);
		setDeleteError(null);
		try {
			await api.deleteVolunteerOpportunity(opportunityId);
			navigate("/");
		} catch (err) {
			setDeleteError(
				err instanceof Error ? err.message : t("opportunities.deleteError"),
			);
			setDeleting(false);
		}
	}

	if (loading)
		return <p className="text-gray-500">{t("opportunities.loading")}</p>;
	if (error)
		return (
			<p className="text-red-600">
				{t("opportunities.error", { message: error })}
			</p>
		);
	if (!opportunity)
		return <p className="text-gray-500">{t("opportunities.notFound")}</p>;

	const isAuthenticated = auth.isAuthenticated;
	const isOwner =
		isOrganisator && userOrgIds.includes(opportunity.organizationId);
	const isDraft = opportunity.status === "Draft";

	async function handlePublish() {
		if (!opportunityId) return;
		setPublishing(true);
		try {
			await api.publishVolunteerOpportunity(opportunityId);
			dispatchToast("success", t("opportunities.publishSuccess"));
			load();
		} catch (err) {
			dispatchToast("error", getApiErrorMessage(err, t("error.serverError")));
		} finally {
			setPublishing(false);
		}
	}

	const totalMax = opportunity.timeSlots.reduce(
		(sum, ts) => sum + ts.maxParticipants,
		0,
	);
	const spotsLeft =
		totalMax > 0 ? totalMax - opportunity.currentParticipantCount : Infinity;
	const isFull = totalMax > 0 && spotsLeft <= 0;

	return (
		<div className="max-w-2xl">
			{/* Back navigation */}
			<Link
				to="/#opportunities"
				className="mb-6 inline-flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-700 transition-colors"
			>
				<svg
					className="h-4 w-4"
					fill="none"
					viewBox="0 0 24 24"
					strokeWidth="2"
					stroke="currentColor"
					aria-hidden="true"
				>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M10.5 19.5 3 12m0 0 7.5-7.5M3 12h18"
					/>
				</svg>
				{t("breadcrumb.volunteerOpportunities")}
			</Link>

			{/* Banner image */}
			{opportunity.bannerImageUrl && (
				<img
					src={opportunity.bannerImageUrl}
					alt=""
					className="mb-6 h-56 w-full rounded-2xl object-cover shadow-sm sm:h-72"
				/>
			)}

			{/* Draft notice */}
			{isDraft && (
				<div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3">
					<p className="text-sm text-amber-800">
						{t("opportunities.draftNotice")}
					</p>
					{isOwner && (
						<button
							onClick={() => void handlePublish()}
							disabled={publishing}
							data-testid="publish-opportunity"
							className="shrink-0 rounded-lg bg-brand-700 px-4 py-1.5 text-sm font-semibold text-white transition hover:bg-brand-800 disabled:opacity-50"
						>
							{publishing
								? t("opportunities.publishing")
								: t("opportunities.publish")}
						</button>
					)}
				</div>
			)}

			{/* Org chip + action buttons */}
			<div className="mb-3 flex items-center justify-between gap-3">
				<Link
					to={`/organizations/${opportunity.organizationId}`}
					className="inline-flex items-center rounded-full bg-brand-50 px-3 py-1 text-xs font-medium text-brand-700 hover:bg-brand-100 transition-colors"
				>
					{opportunity.organizationName}
				</Link>
				<div className="flex shrink-0 gap-2">
					<a
						href={`${runtimeConfig.apiUrl}/v1/volunteer-opportunities/${opportunityId}/calendar`}
						download="opportunity.ics"
						aria-label={t("opportunities.addToCalendar")}
						className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-50 transition-colors"
					>
						<svg
							className="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="2"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5m-9-6h.008v.008H12v-.008ZM12 15h.008v.008H12V15Zm0 2.25h.008v.008H12v-.008ZM9.75 15h.008v.008H9.75V15Zm0 2.25h.008v.008H9.75v-.008ZM7.5 15h.008v.008H7.5V15Zm0 2.25h.008v.008H7.5v-.008Zm6.75-4.5h.008v.008h-.008v-.008Zm0 2.25h.008v.008h-.008V15Zm0 2.25h.008v.008h-.008v-.008Zm2.25-4.5h.008v.008H16.5v-.008Zm0 2.25h.008v.008H16.5V15Z"
							/>
						</svg>
						<span className="hidden sm:inline">
							{t("opportunities.addToCalendar")}
						</span>
					</a>
					<button
						onClick={handleShare}
						data-testid="share-opportunity"
						aria-label={t("opportunities.shareOpportunity")}
						className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-50 transition-colors"
					>
						<svg
							className="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="2"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M7.217 10.907a2.25 2.25 0 1 0 0 2.186m0-2.186c.18.324.283.696.283 1.093s-.103.77-.283 1.093m0-2.186 9.566-5.314m-9.566 7.5 9.566 5.314m0 0a2.25 2.25 0 1 0 3.935 2.186 2.25 2.25 0 0 0-3.935-2.186Zm0-12.814a2.25 2.25 0 1 0 3.933-2.185 2.25 2.25 0 0 0-3.933 2.185Z"
							/>
						</svg>
						<span className="hidden sm:inline">{t("opportunities.share")}</span>
					</button>
					{isOwner && (
						<>
							<button
								onClick={() => setShowEdit(true)}
								className="rounded-lg border border-gray-200 px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-50 transition-colors"
							>
								{t("opportunities.edit")}
							</button>
							<button
								onClick={() => setShowDeleteConfirm(true)}
								disabled={deleting}
								className="rounded-lg border border-red-200 px-3 py-1.5 text-sm text-red-600 hover:bg-red-50 transition-colors disabled:opacity-50"
							>
								{deleting
									? t("opportunities.deleting")
									: t("opportunities.delete")}
							</button>
						</>
					)}
				</div>
			</div>

			{/* Title */}
			<h1 className="mb-3 text-2xl font-bold text-gray-900 sm:text-3xl">
				{opportunity.title}
			</h1>

			{/* Description */}
			{opportunity.description && (
				<p className="mb-6 leading-relaxed text-gray-600">
					{opportunity.description}
				</p>
			)}

			{/* Info card */}
			<div className="mb-6 space-y-3 rounded-2xl border border-gray-100 bg-gray-50 px-4 py-4">
				<div className="flex items-center gap-3 text-sm text-gray-700">
					<svg
						className="h-4 w-4 shrink-0 text-gray-400"
						fill="none"
						viewBox="0 0 24 24"
						strokeWidth="1.5"
						stroke="currentColor"
						aria-hidden="true"
					>
						<path
							strokeLinecap="round"
							strokeLinejoin="round"
							d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5"
						/>
					</svg>
					<span>{formatOccurrence(opportunity.occurrence, t)}</span>
				</div>

				<div className="flex items-center gap-3 text-sm text-gray-700">
					<svg
						className="h-4 w-4 shrink-0 text-gray-400"
						fill="none"
						viewBox="0 0 24 24"
						strokeWidth="1.5"
						stroke="currentColor"
						aria-hidden="true"
					>
						<path
							strokeLinecap="round"
							strokeLinejoin="round"
							d="M18 18.72a9.094 9.094 0 0 0 3.741-.479 3 3 0 0 0-4.682-2.72m.94 3.198.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0 1 12 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 0 1 6 18.719m12 0a5.971 5.971 0 0 0-.941-3.197m0 0A5.995 5.995 0 0 0 12 12.75a5.995 5.995 0 0 0-5.058 2.772m0 0a3 3 0 0 0-4.681 2.72 8.986 8.986 0 0 0 3.74.477m.94-3.197a5.971 5.971 0 0 0-.94 3.197M15 6.75a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm6 3a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Zm-13.5 0a2.25 2.25 0 1 1-4.5 0 2.25 2.25 0 0 1 4.5 0Z"
						/>
					</svg>
					<span>
						{formatParticipationType(opportunity.participationType, t)}
					</span>
				</div>

				{opportunity.isRemote ? (
					<div className="flex items-center gap-3 text-sm text-green-700">
						<svg
							className="h-4 w-4 shrink-0 text-green-500"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="1.5"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M12 21a9.004 9.004 0 0 0 8.716-6.747M12 21a9.004 9.004 0 0 1-8.716-6.747M12 21c2.485 0 4.5-4.03 4.5-9S14.485 3 12 3m0 18c-2.485 0-4.5-4.03-4.5-9S9.515 3 12 3m0 0a8.997 8.997 0 0 1 7.843 4.582M12 3a8.997 8.997 0 0 0-7.843 4.582m15.686 0A11.953 11.953 0 0 1 12 10.5c-2.998 0-5.74-1.1-7.843-2.918m15.686 0A8.959 8.959 0 0 1 21 12c0 .778-.099 1.533-.284 2.253m0 0A17.919 17.919 0 0 1 12 16.5c-3.162 0-6.133-.815-8.716-2.247m0 0A9.015 9.015 0 0 1 3 12c0-1.605.42-3.113 1.157-4.418"
							/>
						</svg>
						<span>{t("opportunities.remote")}</span>
					</div>
				) : (
					<div className="flex items-center gap-3 text-sm text-gray-700">
						<svg
							className="h-4 w-4 shrink-0 text-gray-400"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="1.5"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
							/>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1 1 15 0Z"
							/>
						</svg>
						<span>
							{opportunity.street} {opportunity.houseNumber},{" "}
							{opportunity.zipCode} {opportunity.city}
						</span>
					</div>
				)}
			</div>

			{/* Category and tags */}
			{(opportunity.category ||
				(opportunity.tags && opportunity.tags.length > 0)) && (
				<div className="mb-6 flex flex-wrap gap-2">
					{opportunity.category && (
						<span className="inline-flex items-center rounded-full bg-brand-50 px-3 py-1 text-xs font-medium text-brand-700">
							{t(`opportunities.category.${opportunity.category}`)}
						</span>
					)}
					{opportunity.tags &&
						opportunity.tags.map((tag) => (
							<span
								key={tag}
								className="inline-flex items-center rounded-full bg-gray-100 px-3 py-1 text-xs font-medium text-gray-600"
							>
								{tag}
							</span>
						))}
				</div>
			)}

			{/* Map */}
			{!opportunity.isRemote &&
				opportunity.latitude != null &&
				opportunity.longitude != null && (
					<div className="mb-6 overflow-hidden rounded-2xl border border-gray-100 shadow-sm">
						<SingleMarkerMap
							latitude={opportunity.latitude}
							longitude={opportunity.longitude}
							label={`${opportunity.street} ${opportunity.houseNumber}, ${opportunity.zipCode} ${opportunity.city}`}
						/>
					</div>
				)}

			{/* Time slots */}
			{opportunity.participationType === "Waitlist" &&
				opportunity.timeSlots.length > 0 && (
					<div className="mb-6">
						<h2 className="mb-3 text-xs font-semibold uppercase tracking-wider text-gray-400">
							{t("opportunities.availableTimeSlots")}
						</h2>
						<ul className="space-y-2">
							{opportunity.timeSlots.map((ts) => (
								<li
									key={ts.id}
									className="flex items-center justify-between rounded-xl border border-gray-100 bg-white px-4 py-3 text-sm text-gray-700 shadow-sm"
								>
									<span>
										{formatDateTime(
											ts.startDateTime as unknown as string,
											i18n.language,
										)}
										{" - "}
										{formatDateTime(
											ts.endDateTime as unknown as string,
											i18n.language,
										)}
									</span>
									<span className="ml-3 shrink-0 text-xs text-gray-400">
										{t("opportunities.maxParticipants", {
											count: ts.maxParticipants,
										})}
									</span>
								</li>
							))}
						</ul>
					</div>
				)}

			{/* Manage applications */}
			{isOwner && (
				<div className="mb-6">
					<button
						onClick={() =>
							navigate(`/volunteer-opportunities/${opportunityId}/engagements`)
						}
						className="text-sm font-medium text-brand-700 hover:text-brand-800 hover:underline"
					>
						{t("opportunities.manageApplications")}
					</button>
				</div>
			)}

			{/* Sign-up CTA */}
			{isAuthenticated && !isOwner && !signedUp && !isDraft && (
				<div className="space-y-3">
					{totalMax > 0 && (
						<p
							className={`text-sm font-medium ${
								isFull
									? "text-red-600"
									: spotsLeft <= 3
										? "text-orange-600"
										: "text-gray-600"
							}`}
						>
							{isFull
								? t("opportunities.noSpotsLeft")
								: spotsLeft <= 5
									? t("opportunities.fewSpotsLeft", { count: spotsLeft })
									: t("opportunities.spotsLeft", { count: spotsLeft })}
						</p>
					)}
					<button
						onClick={() => setShowSignUp(true)}
						disabled={isFull}
						className="w-full rounded-xl bg-brand-700 px-6 py-3.5 text-base font-semibold text-white transition hover:bg-brand-800 disabled:cursor-not-allowed disabled:opacity-50"
					>
						{opportunity.participationType === "Waitlist"
							? t("opportunities.joinWaitlist")
							: t("opportunities.expressInterest")}
					</button>
				</div>
			)}

			{/* Login prompt */}
			{!isAuthenticated && !isDraft && (
				<p className="rounded-xl border border-gray-100 bg-gray-50 px-4 py-3 text-sm text-gray-500">
					<Trans
						i18nKey="opportunities.loginToApply"
						components={{
							loginLink: (
								<button
									onClick={() => auth.signinRedirect()}
									className="underline hover:text-gray-800"
								/>
							),
						}}
					/>
				</p>
			)}

			{/* Signed up confirmation */}
			{signedUp && (
				<p className="rounded-xl border border-green-100 bg-green-50 px-4 py-3 text-sm text-green-700">
					{t("opportunities.signupSuccess")}
				</p>
			)}

			{showSignUp && (
				<SignUpModal
					opportunityId={opportunity.id}
					participationType={opportunity.participationType}
					timeSlots={opportunity.timeSlots}
					onClose={() => setShowSignUp(false)}
					onSuccess={() => setSignedUp(true)}
				/>
			)}

			{showEdit && (
				<CreateVolunteerOpportunityModal
					organizationId={opportunity.organizationId}
					initialOpportunity={opportunity}
					onClose={() => setShowEdit(false)}
					onSuccess={load}
				/>
			)}

			{showDeleteConfirm && (
				<ConfirmDialog
					title={t("confirmDialog.delete.title")}
					message={t("confirmDialog.delete.message")}
					confirmLabel={t("confirmDialog.delete.confirm")}
					onConfirm={handleDeleteConfirm}
					onClose={() => {
						setShowDeleteConfirm(false);
						setDeleteError(null);
					}}
					loading={deleting}
					error={deleteError}
				/>
			)}
		</div>
	);
}
