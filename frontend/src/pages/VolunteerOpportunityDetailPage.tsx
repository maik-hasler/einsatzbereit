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
import { getActiveOrgId } from "../lib/activeOrg";
import SignUpModal from "../components/SignUpModal";
import CreateVolunteerOpportunityModal from "../components/CreateVolunteerOpportunityModal";
import ConfirmDialog from "../components/ConfirmDialog";
import SingleMarkerMap from "../components/SingleMarkerMap";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { usePageTitle } from "../hooks/usePageTitle";
import { dispatchToast } from "../lib/toastBus";
import { getApiErrorMessage } from "../lib/apiError";

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
	const activeOrgId = getActiveOrgId();

	usePageToolbar([
		{ label: t("breadcrumb.home"), href: "/" },
		{ label: t("breadcrumb.volunteerOpportunities"), href: "/#opportunities" },
		{ label: opportunity?.title ?? "" },
	]);

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
	const isOwner = isOrganisator && opportunity.organizationId === activeOrgId;
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

	return (
		<div className="max-w-2xl">
			{opportunity.bannerImageUrl && (
				<img
					src={opportunity.bannerImageUrl}
					alt=""
					className="mb-4 h-48 w-full rounded-2xl object-cover sm:h-64"
				/>
			)}

			{isDraft && (
				<div className="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3">
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

			<div className="mb-1 flex items-start justify-between gap-4">
				<h1 className="text-2xl font-bold text-gray-900">
					{opportunity.title}
				</h1>
				<div className="flex gap-2 shrink-0">
					<button
						onClick={handleShare}
						data-testid="share-opportunity"
						aria-label={t("opportunities.shareOpportunity")}
						className="inline-flex items-center gap-1.5 rounded border px-3 py-1 text-sm text-gray-600 hover:bg-gray-50"
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
								className="rounded border px-3 py-1 text-sm text-gray-600 hover:bg-gray-50"
							>
								{t("opportunities.edit")}
							</button>
							<button
								onClick={() => setShowDeleteConfirm(true)}
								disabled={deleting}
								className="rounded border border-red-200 px-3 py-1 text-sm text-red-600 hover:bg-red-50 disabled:opacity-50"
							>
								{deleting
									? t("opportunities.deleting")
									: t("opportunities.delete")}
							</button>
						</>
					)}
				</div>
			</div>

			<p className="mb-4 text-sm text-gray-500">
				<Link
					to={`/organizations/${opportunity.organizationId}`}
					className="hover:underline"
				>
					{opportunity.organizationName}
				</Link>
			</p>

			<p className="mb-6 text-gray-700">{opportunity.description}</p>

			<div className="mb-6 flex flex-wrap gap-2">
				<span className="rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-700">
					{formatOccurrence(opportunity.occurrence, t)}
				</span>
				<span className="rounded-full bg-blue-50 px-3 py-1 text-sm text-blue-700">
					{formatParticipationType(opportunity.participationType, t)}
				</span>
				{opportunity.isRemote ? (
					<span className="rounded-full bg-green-50 px-3 py-1 text-sm text-green-700">
						{t("opportunities.remote")}
					</span>
				) : (
					<span className="rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-700">
						{opportunity.street} {opportunity.houseNumber},{" "}
						{opportunity.zipCode} {opportunity.city}
					</span>
				)}
			</div>

			{!opportunity.isRemote &&
				opportunity.latitude !== undefined &&
				opportunity.longitude !== undefined && (
					<div className="mb-6">
						<SingleMarkerMap
							latitude={opportunity.latitude}
							longitude={opportunity.longitude}
							label={`${opportunity.street} ${opportunity.houseNumber}, ${opportunity.zipCode} ${opportunity.city}`}
						/>
					</div>
				)}

			{opportunity.participationType === "Waitlist" &&
				opportunity.timeSlots.length > 0 && (
					<div className="mb-6">
						<h2 className="mb-2 text-sm font-semibold text-gray-700">
							{t("opportunities.availableTimeSlots")}
						</h2>
						<ul className="space-y-2">
							{opportunity.timeSlots.map((ts) => (
								<li
									key={ts.id}
									className="rounded border px-3 py-2 text-sm text-gray-700"
								>
									{formatDateTime(
										ts.startDateTime as unknown as string,
										i18n.language,
									)}{" "}
									-{" "}
									{formatDateTime(
										ts.endDateTime as unknown as string,
										i18n.language,
									)}
									<span className="ml-2 text-gray-400">
										{t("opportunities.maxParticipants", {
											count: ts.maxParticipants,
										})}
									</span>
								</li>
							))}
						</ul>
					</div>
				)}

			{isOwner && (
				<div className="mb-6">
					<button
						onClick={() =>
							navigate(`/volunteer-opportunities/${opportunityId}/engagements`)
						}
						className="text-sm text-blue-600 hover:underline"
					>
						{t("opportunities.manageApplications")}
					</button>
				</div>
			)}

			{isAuthenticated &&
				!isOrganisator &&
				!signedUp &&
				!isDraft &&
				(() => {
					const totalMax = opportunity.timeSlots.reduce(
						(sum, ts) => sum + ts.maxParticipants,
						0,
					);
					const spotsLeft =
						totalMax > 0
							? totalMax - opportunity.currentParticipantCount
							: Infinity;
					const isFull = totalMax > 0 && spotsLeft <= 0;
					return (
						<div className="space-y-2">
							{totalMax > 0 && (
								<p
									className={`text-sm font-medium ${isFull ? "text-red-600" : spotsLeft <= 3 ? "text-orange-600" : "text-gray-600"}`}
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
								className="rounded bg-brand-700 px-5 py-2 text-sm text-white hover:bg-brand-800 disabled:cursor-not-allowed disabled:opacity-50"
							>
								{opportunity.participationType === "Waitlist"
									? t("opportunities.joinWaitlist")
									: t("opportunities.expressInterest")}
							</button>
						</div>
					);
				})()}

			{!isAuthenticated && !isDraft && (
				<p className="text-sm text-gray-500">
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

			{signedUp && (
				<p className="rounded bg-green-50 px-4 py-2 text-sm text-green-700">
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
