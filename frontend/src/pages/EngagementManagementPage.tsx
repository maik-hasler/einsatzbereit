import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	EngagementSummary,
	VolunteerOpportunityDetails,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import ConfirmDialog from "../components/ConfirmDialog";
import EmptyState from "../components/EmptyState";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { formatDateTime } from "../lib/format";
import { usePageTitle } from "../hooks/usePageTitle";
import { dispatchToast } from "../lib/toastBus";
import { getApiErrorMessage } from "../lib/apiError";

const STATUS_COLORS: Record<string, string> = {
	Pending: "bg-yellow-50 text-yellow-700",
	Confirmed: "bg-green-50 text-green-700",
	Cancelled: "bg-red-50 text-red-700",
	Withdrawn: "bg-gray-100 text-gray-500",
};

export default function EngagementManagementPage() {
	const { opportunityId } = useParams<{ opportunityId: string }>();
	const api = useApiClient();
	const navigate = useNavigate();
	const { t, i18n } = useTranslation();
	const [opportunity, setOpportunity] =
		useState<VolunteerOpportunityDetails | null>(null);
	usePageTitle(
		opportunity?.title
			? `${t("engagementManagement.title")} - ${opportunity.title}`
			: t("engagementManagement.title"),
	);

	const STATUS_LABELS: Record<string, string> = {
		Pending: t("engagementManagement.status.Pending"),
		Confirmed: t("engagementManagement.status.Confirmed"),
		Cancelled: t("engagementManagement.status.Cancelled"),
		Withdrawn: t("engagementManagement.status.Withdrawn"),
	};

	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

	const [engagements, setEngagements] = useState<EngagementSummary[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [confirming, setConfirming] = useState<string | null>(null);
	const [confirmCancelId, setConfirmCancelId] = useState<string | null>(null);
	const [cancelling, setCancelling] = useState(false);
	const [cancelError, setCancelError] = useState<string | null>(null);
	const [checkingIn, setCheckingIn] = useState<string | null>(null);

	usePageToolbar([
		{ label: t("breadcrumb.home"), href: "/" },
		{
			label: opportunity?.title || t("breadcrumb.volunteerOpportunities"),
			href: opportunityId ? `/volunteer-opportunities/${opportunityId}` : "/",
		},
		{ label: t("breadcrumb.engagements") },
	]);

	useEffect(() => {
		if (!opportunityId) return;
		Promise.all([
			api.getEngagements(opportunityId),
			api
				.getVolunteerOpportunityDetails(opportunityId)
				.then((d) => setOpportunity(d))
				.catch(() => undefined),
		])
			.then(([e]) => setEngagements(e))
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [opportunityId]);

	async function handleConfirm(engagementId: string) {
		setConfirming(engagementId);
		try {
			const updated = await api.confirmEngagement(engagementId);
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === engagementId ? { ...e, status: updated.status } : e,
				),
			);
		} catch (err) {
			dispatchToast(
				"error",
				err instanceof Error
					? err.message
					: t("engagementManagement.confirmError"),
			);
		} finally {
			setConfirming(null);
		}
	}

	async function handleCheckIn(engagementId: string) {
		setCheckingIn(engagementId);
		try {
			await api.checkInEngagement(engagementId);
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === engagementId ? { ...e, isCheckedIn: true } : e,
				),
			);
		} catch (err) {
			dispatchToast(
				"error",
				err instanceof Error ? err.message : t("checkIn.markCheckedIn"),
			);
		} finally {
			setCheckingIn(null);
		}
	}

	async function handleCancelConfirm() {
		if (!confirmCancelId) return;
		setCancelling(true);
		setCancelError(null);
		try {
			const updated = await api.cancelEngagement(confirmCancelId, null);
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === confirmCancelId ? { ...e, status: updated.status } : e,
				),
			);
			setConfirmCancelId(null);
		} catch (err) {
			setCancelError(
				err instanceof Error
					? err.message
					: t("engagementManagement.cancelError"),
			);
		} finally {
			setCancelling(false);
		}
	}

	function handleCancelClose() {
		if (cancelling) return;
		setConfirmCancelId(null);
		setCancelError(null);
	}

	const checkInMethod = opportunity?.checkInMethod;
	const showManualCheckIn =
		checkInMethod === "Manual" || checkInMethod === "QRCode";

	return (
		<>
			<h1 className="mb-6 text-2xl font-bold text-gray-900">
				{t("engagementManagement.title")}
			</h1>

			{checkInMethod === "PINCode" && opportunity?.checkInPin && (
				<div className="mb-6 rounded-lg border border-blue-200 bg-blue-50 p-4">
					<p className="text-sm font-medium text-blue-900">
						{t("checkIn.organizerPin")}
					</p>
					<p className="mt-1 font-mono text-2xl font-bold tracking-widest text-blue-800">
						{opportunity.checkInPin}
					</p>
					<p className="mt-1 text-xs text-blue-600">
						{t("checkIn.organizerPinHint")}
					</p>
				</div>
			)}

			{loading && (
				<p className="text-gray-500">{t("engagementManagement.loading")}</p>
			)}
			{error && (
				<p className="text-red-600">
					{t("engagementManagement.error", { message: error })}
				</p>
			)}

			{!loading && !error && engagements.length === 0 && (
				<EmptyState
					title={t("engagementManagement.noApplications")}
					message={t("engagementManagement.noApplicationsHint")}
					action={{
						label: t("engagementManagement.backToOpportunity"),
						onClick: () =>
							navigate(
								opportunityId
									? `/volunteer-opportunities/${opportunityId}`
									: "/",
							),
					}}
				/>
			)}

			{!loading && !error && engagements.length > 0 && (
				<ul className="space-y-3">
					{engagements.map((e) => (
						<li key={e.id} className="rounded border p-4">
							<div className="flex items-start justify-between gap-2">
								<div className="min-w-0">
									<p className="font-mono text-xs text-gray-500">
										{t("engagementManagement.volunteer", {
											id: e.volunteerId.slice(0, 8) + "...",
										})}
									</p>
									{e.message && (
										<p className="mt-1 text-sm text-gray-700">
											&ldquo;{e.message}&rdquo;
										</p>
									)}
									{e.timeSlotId &&
										(() => {
											const slot = opportunity?.timeSlots.find(
												(s) => s.id === e.timeSlotId,
											);
											return (
												<p className="mt-1 text-xs text-gray-400">
													{slot
														? `${formatDateTime(slot.startDateTime as unknown as string, i18n.language)} - ${formatDateTime(slot.endDateTime as unknown as string, i18n.language)}`
														: e.timeSlotId.slice(0, 8) + "..."}
												</p>
											);
										})()}
									<p className="mt-1 text-xs text-gray-400">
										{t("engagementManagement.receivedOn", {
											date: new Date(e.createdOn).toLocaleDateString(locale),
										})}
									</p>
									{e.isCheckedIn && (
										<span className="mt-1 inline-block rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800">
											{t("checkIn.checkedInLabel")}
										</span>
									)}
								</div>
								<div className="flex shrink-0 flex-col items-end gap-2">
									<span
										className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[e.status] ?? "bg-gray-100 text-gray-600"}`}
									>
										{STATUS_LABELS[e.status] ?? e.status}
									</span>
									{e.status === "Pending" && (
										<div className="flex gap-2">
											<button
												onClick={() => handleConfirm(e.id)}
												disabled={confirming === e.id}
												className="rounded bg-green-600 px-2 py-1 text-xs text-white hover:bg-green-700 disabled:opacity-50"
											>
												{confirming === e.id
													? t("engagementManagement.processing")
													: t("engagementManagement.confirm")}
											</button>
											<button
												onClick={() => setConfirmCancelId(e.id)}
												className="rounded bg-red-600 px-2 py-1 text-xs text-white hover:bg-red-700"
											>
												{t("engagementManagement.cancel")}
											</button>
										</div>
									)}
									{e.status === "Confirmed" && (
										<div className="flex gap-2">
											{showManualCheckIn && !e.isCheckedIn && (
												<button
													onClick={() => handleCheckIn(e.id)}
													disabled={checkingIn === e.id}
													className="rounded bg-brand-800 px-2 py-1 text-xs text-white hover:bg-brand-700 disabled:opacity-50"
												>
													{checkingIn === e.id
														? t("checkIn.markingCheckedIn")
														: t("checkIn.markCheckedIn")}
												</button>
											)}
											<button
												onClick={() => setConfirmCancelId(e.id)}
												className="text-xs text-red-600 hover:underline"
											>
												{t("engagementManagement.revoke")}
											</button>
										</div>
									)}
								</div>
							</div>
						</li>
					))}
				</ul>
			)}

			{confirmCancelId && (
				<ConfirmDialog
					title={t("confirmDialog.cancel.title")}
					message={t("confirmDialog.cancel.message")}
					confirmLabel={t("confirmDialog.cancel.confirm")}
					onConfirm={handleCancelConfirm}
					onClose={handleCancelClose}
					loading={cancelling}
					error={cancelError}
				/>
			)}
		</>
	);
}
