import { useEffect, useState } from "react";
import { useParams, Link } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	EngagementSummary,
	OpportunityFeedbackSummary,
	VolunteerOpportunityDetails,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import ConfirmDialog from "../components/ConfirmDialog";
import EmptyState from "../components/EmptyState";
import QRScannerModal from "../components/QRScannerModal";
import Spinner from "../components/Spinner";
import NotFoundPage from "./NotFoundPage";
import { formatDateTime } from "../lib/format";
import { usePageTitle } from "../hooks/usePageTitle";
import { useSetOrgBreadcrumbExtra } from "../contexts/OrgBreadcrumbContext";
import { dispatchToast } from "../lib/toastBus";
import { getApiErrorMessage, isApiNotFoundError } from "../lib/apiError";
import { ENGAGEMENT_STATUS_COLORS } from "../lib/engagementStatus";

const STATUS_COLORS = ENGAGEMENT_STATUS_COLORS;

export default function EngagementManagementPage() {
	const { opportunityId } = useParams<{ opportunityId: string }>();
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	const [opportunity, setOpportunity] =
		useState<VolunteerOpportunityDetails | null>(null);
	usePageTitle(
		opportunity?.title
			? `${t("engagementManagement.title")} - ${opportunity.title}`
			: t("engagementManagement.title"),
	);
	useSetOrgBreadcrumbExtra(opportunity?.title);

	const STATUS_LABELS: Record<string, string> = {
		Pending: t("engagementManagement.status.Pending"),
		Confirmed: t("engagementManagement.status.Confirmed"),
		Cancelled: t("engagementManagement.status.Cancelled"),
		Withdrawn: t("engagementManagement.status.Withdrawn"),
	};

	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

	const [engagements, setEngagements] = useState<EngagementSummary[]>([]);
	const [feedback, setFeedback] = useState<OpportunityFeedbackSummary | null>(
		null,
	);
	const [checkInPin, setCheckInPin] = useState<string | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [notFound, setNotFound] = useState(false);
	const [confirming, setConfirming] = useState<string | null>(null);
	const [confirmCancelId, setConfirmCancelId] = useState<string | null>(null);
	const [cancelling, setCancelling] = useState(false);
	const [cancelError, setCancelError] = useState<string | null>(null);
	const [checkingIn, setCheckingIn] = useState<string | null>(null);
	const [qrScannerOpen, setQrScannerOpen] = useState(false);

	useEffect(() => {
		if (!opportunityId) return;
		Promise.all([
			api.getEngagements(opportunityId),
			api
				.getVolunteerOpportunityDetails(opportunityId)
				.then((d) => setOpportunity(d))
				.catch(() => undefined),
			api
				.getOpportunityFeedback(opportunityId)
				.then(setFeedback)
				.catch(() => undefined),
		])
			.then(([e]) => setEngagements(e))
			.catch((err) => {
				if (isApiNotFoundError(err)) {
					setNotFound(true);
				} else {
					setError(getApiErrorMessage(err, t("error.serverError")));
				}
			})
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [opportunityId]);

	useEffect(() => {
		if (!opportunityId || opportunity?.checkInMethod !== "PINCode") return;
		api
			.getOpportunityCheckInPin(opportunityId)
			.then(setCheckInPin)
			.catch(() => undefined);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [opportunityId, opportunity?.checkInMethod]);

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

	if (notFound) {
		return <NotFoundPage />;
	}

	const checkInMethod = opportunity?.checkInMethod;
	const showManualCheckIn = checkInMethod === "Manual";
	const showQrScanner = checkInMethod === "QRCode";

	return (
		<>
			<div className="mb-6">
				<h1 className="text-2xl font-bold text-gray-900">
					{t("engagementManagement.title")}
				</h1>
			</div>

			{checkInMethod === "PINCode" && checkInPin && (
				<div className="mb-6 rounded-xl border border-brand-200 bg-brand-50 p-4">
					<p className="text-sm font-medium text-brand-900">
						{t("checkIn.organizerPin")}
					</p>
					<p className="mt-1 font-mono text-2xl font-bold tracking-widest text-brand-800">
						{checkInPin}
					</p>
					<p className="mt-1 text-xs text-brand-700">
						{t("checkIn.organizerPinHint")}
					</p>
				</div>
			)}

			{showQrScanner && (
				<div className="mb-6">
					<button
						type="button"
						onClick={() => setQrScannerOpen(true)}
						className="inline-flex items-center gap-2 rounded-xl bg-brand-700 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-brand-800"
					>
						<svg
							className="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="1.5"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M3.75 4.875c0-.621.504-1.125 1.125-1.125h4.5c.621 0 1.125.504 1.125 1.125v4.5c0 .621-.504 1.125-1.125 1.125h-4.5A1.125 1.125 0 0 1 3.75 9.375v-4.5ZM3.75 14.625c0-.621.504-1.125 1.125-1.125h4.5c.621 0 1.125.504 1.125 1.125v4.5c0 .621-.504 1.125-1.125 1.125h-4.5a1.125 1.125 0 0 1-1.125-1.125v-4.5ZM13.5 4.875c0-.621.504-1.125 1.125-1.125h4.5c.621 0 1.125.504 1.125 1.125v4.5c0 .621-.504 1.125-1.125 1.125h-4.5A1.125 1.125 0 0 1 13.5 9.375v-4.5Z"
							/>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M6.75 6.75h.75v.75h-.75v-.75ZM6.75 16.5h.75v.75h-.75v-.75ZM16.5 6.75h.75v.75h-.75v-.75ZM13.5 13.5h.75v.75h-.75v-.75ZM13.5 19.5h.75v.75h-.75v-.75ZM19.5 13.5h.75v.75h-.75v-.75ZM19.5 19.5h.75v.75h-.75v-.75ZM16.5 16.5h.75v.75h-.75v-.75Z"
							/>
						</svg>
						{t("checkIn.qrScanButton")}
					</button>
				</div>
			)}

			{loading && (
				<div className="flex items-center justify-center py-16">
					<Spinner label={t("engagementManagement.loading")} />
				</div>
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
				/>
			)}

			{!loading && !error && engagements.length > 0 && (
				<ul className="space-y-3">
					{engagements.map((e) => (
						<li
							key={e.id}
							className="rounded-xl border border-gray-100 bg-white px-4 py-4 shadow-sm"
						>
							<div className="flex items-start justify-between gap-2">
								<div className="min-w-0">
									<p className="text-sm font-medium text-gray-800">
										{e.volunteerName ? (
											<Link
												to={`/users/${e.volunteerId}`}
												className="hover:underline"
											>
												{e.volunteerName}
											</Link>
										) : e.volunteerId ? (
											<span className="font-mono text-xs text-gray-400">
												{t("engagementManagement.volunteer", {
													id: e.volunteerId.slice(0, 8) + "...",
												})}
											</span>
										) : (
											<span className="text-xs italic text-gray-400">
												{t("engagementManagement.anonymizedVolunteer")}
											</span>
										)}
									</p>
									{e.message && (
										<p className="mt-1 text-sm italic text-gray-700">
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
										<span className="mt-2 inline-flex items-center gap-1 rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-800">
											<svg
												className="h-3 w-3"
												fill="currentColor"
												viewBox="0 0 20 20"
												aria-hidden="true"
											>
												<path
													fillRule="evenodd"
													d="M16.704 4.153a.75.75 0 0 1 .143 1.052l-8 10.5a.75.75 0 0 1-1.127.075l-4.5-4.5a.75.75 0 0 1 1.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 0 1 1.05-.143Z"
													clipRule="evenodd"
												/>
											</svg>
											{t("checkIn.checkedInLabel")}
										</span>
									)}
								</div>
								<div className="flex shrink-0 flex-col items-end gap-2">
									<span
										className={`rounded-full border px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[e.status] ?? "bg-gray-100 text-gray-600 border-gray-200"}`}
									>
										{STATUS_LABELS[e.status] ?? e.status}
									</span>
									{e.status === "Pending" && (
										<div className="flex gap-2">
											<button
												onClick={() => handleConfirm(e.id)}
												disabled={confirming === e.id}
												className="rounded-xl bg-green-600 px-3 py-1 text-xs font-medium text-white transition-colors hover:bg-green-700 disabled:opacity-50"
											>
												{confirming === e.id
													? t("engagementManagement.processing")
													: t("engagementManagement.confirm")}
											</button>
											<button
												onClick={() => setConfirmCancelId(e.id)}
												className="rounded-xl border border-red-200 px-3 py-1 text-xs text-red-600 transition-colors hover:bg-red-50"
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
													className="rounded-xl bg-brand-700 px-3 py-1 text-xs font-medium text-white transition-colors hover:bg-brand-800 disabled:opacity-50"
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

			{qrScannerOpen && (
				<QRScannerModal
					engagements={engagements}
					onCheckedIn={(engagementId) => {
						setEngagements((prev) =>
							prev.map((e) =>
								e.id === engagementId ? { ...e, isCheckedIn: true } : e,
							),
						);
					}}
					onClose={() => setQrScannerOpen(false)}
				/>
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

			{feedback !== null && (
				<section className="mt-8">
					<h2 className="mb-4 text-lg font-semibold text-gray-900">
						{t("feedback.organizerTab")}
					</h2>
					{feedback.feedbackCount === 0 ? (
						<p className="text-sm text-gray-500">{t("feedback.noFeedback")}</p>
					) : (
						<>
							<p className="mb-4 text-sm text-gray-700">
								{t("feedback.averageRating", {
									rating: feedback.averageRating?.toFixed(1) ?? "-",
									count: feedback.feedbackCount,
								})}
							</p>
							<ul className="space-y-3">
								{feedback.items.map((item, idx) => (
									<li
										key={idx}
										className="rounded-xl border border-gray-100 bg-white px-4 py-3 shadow-sm"
									>
										<div className="flex items-center gap-1">
											{[1, 2, 3, 4, 5].map((s) => (
												<svg
													key={s}
													className={`h-4 w-4 ${s <= item.rating ? "text-yellow-400" : "text-gray-200"}`}
													fill="currentColor"
													viewBox="0 0 24 24"
													aria-hidden="true"
												>
													<path d="M10.788 3.21c.448-1.077 1.976-1.077 2.424 0l2.082 5.006 5.404.434c1.164.093 1.636 1.545.749 2.305l-4.117 3.527 1.257 5.273c.271 1.136-.964 2.033-1.96 1.425L12 18.354 7.373 21.18c-.996.608-2.231-.29-1.96-1.425l1.257-5.273-4.117-3.527c-.887-.76-.415-2.212.749-2.305l5.404-.434 2.082-5.005Z" />
												</svg>
											))}
											<span className="ml-1 text-xs text-gray-400">
												{new Date(item.submittedAt).toLocaleDateString(locale)}
											</span>
										</div>
										{item.comment && (
											<p className="mt-1 text-sm text-gray-700">
												{item.comment}
											</p>
										)}
									</li>
								))}
							</ul>
						</>
					)}
				</section>
			)}
		</>
	);
}
