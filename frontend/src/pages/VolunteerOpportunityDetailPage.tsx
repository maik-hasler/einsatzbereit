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
import EditVolunteerOpportunityModal from "../components/EditVolunteerOpportunityModal";
import ConfirmDialog from "../components/ConfirmDialog";
import { usePageToolbar } from "../contexts/ToolbarContext";

export default function VolunteerOpportunityDetailPage() {
	const { opportunityId } = useParams<{ opportunityId: string }>();
	const navigate = useNavigate();
	const auth = useAuth();
	const api = useApiClient();
	const { t, i18n } = useTranslation();

	const [opportunity, setOpportunity] =
		useState<VolunteerOpportunityDetails | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [showSignUp, setShowSignUp] = useState(false);
	const [signedUp, setSignedUp] = useState(false);
	const [showEdit, setShowEdit] = useState(false);
	const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
	const [deleting, setDeleting] = useState(false);
	const [deleteError, setDeleteError] = useState<string | null>(null);

	const roles = (
		Array.isArray(auth.user?.profile?.roles) ? auth.user?.profile?.roles : []
	) as string[];
	const isOrganisator = roles.includes("organisator");

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
			.catch((err) => setError(err.message))
			.finally(() => setLoading(false));
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

	return (
		<div className="max-w-2xl">
			<div className="mb-1 flex items-start justify-between gap-4">
				<h1 className="text-2xl font-bold text-gray-900">
					{opportunity.title}
				</h1>
				{isOrganisator && (
					<div className="flex gap-2 shrink-0">
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
					</div>
				)}
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

			{isOrganisator && (
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

			{isAuthenticated && !isOrganisator && !signedUp && (
				<button
					onClick={() => setShowSignUp(true)}
					className="rounded bg-black px-5 py-2 text-sm text-white hover:bg-gray-800"
				>
					{opportunity.participationType === "Waitlist"
						? t("opportunities.joinWaitlist")
						: t("opportunities.expressInterest")}
				</button>
			)}

			{!isAuthenticated && (
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
				<EditVolunteerOpportunityModal
					opportunity={opportunity}
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
