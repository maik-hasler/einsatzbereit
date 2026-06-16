import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import type { EngagementSummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import ConfirmDialog from "../components/ConfirmDialog";
import EmptyState from "../components/EmptyState";
import CheckInModal from "../components/CheckInModal";
import { usePageTitle } from "../hooks/usePageTitle";
import { getApiErrorMessage } from "../lib/apiError";

const STATUS_COLORS: Record<string, string> = {
	Pending: "bg-yellow-50 text-yellow-700",
	Confirmed: "bg-green-50 text-green-700",
	Cancelled: "bg-red-50 text-red-700",
	Withdrawn: "bg-gray-100 text-gray-500",
};

export default function MyEngagementsPage() {
	const api = useApiClient();
	const navigate = useNavigate();
	const { t, i18n } = useTranslation();
	usePageTitle(t("myEngagements.title"));
	const [engagements, setEngagements] = useState<EngagementSummary[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [confirmWithdrawId, setConfirmWithdrawId] = useState<string | null>(
		null,
	);
	const [withdrawing, setWithdrawing] = useState(false);
	const [withdrawError, setWithdrawError] = useState<string | null>(null);
	const [checkInEngagement, setCheckInEngagement] =
		useState<EngagementSummary | null>(null);

	const STATUS_LABELS: Record<string, string> = {
		Pending: t("myEngagements.status.Pending"),
		Confirmed: t("myEngagements.status.Confirmed"),
		Cancelled: t("myEngagements.status.Cancelled"),
		Withdrawn: t("myEngagements.status.Withdrawn"),
	};

	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

	useEffect(() => {
		api
			.getMyEngagements()
			.then(setEngagements)
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	async function handleWithdrawConfirm() {
		if (!confirmWithdrawId) return;
		setWithdrawing(true);
		setWithdrawError(null);
		try {
			const updated = await api.withdrawEngagement(confirmWithdrawId);
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === confirmWithdrawId ? { ...e, status: updated.status } : e,
				),
			);
			setConfirmWithdrawId(null);
		} catch (err) {
			setWithdrawError(
				err instanceof Error ? err.message : t("myEngagements.withdrawError"),
			);
		} finally {
			setWithdrawing(false);
		}
	}

	function handleWithdrawClose() {
		if (withdrawing) return;
		setConfirmWithdrawId(null);
		setWithdrawError(null);
	}

	function handleCheckedIn() {
		if (!checkInEngagement) return;
		setEngagements((prev) =>
			prev.map((e) =>
				e.id === checkInEngagement.id ? { ...e, isCheckedIn: true } : e,
			),
		);
	}

	return (
		<>
			<h1 className="mb-6 text-2xl font-bold text-gray-900">
				{t("myEngagements.title")}
			</h1>

			{loading && <p className="text-gray-500">{t("myEngagements.loading")}</p>}
			{error && (
				<p className="text-red-600">
					{t("myEngagements.error", { message: error })}
				</p>
			)}

			{!loading && !error && engagements.length === 0 && (
				<EmptyState
					title={t("myEngagements.noEngagements")}
					message={t("myEngagements.noEngagementsHint")}
					action={{
						label: t("myEngagements.exploreNeeds"),
						onClick: () => navigate("/"),
					}}
				/>
			)}

			{!loading && !error && engagements.length > 0 && (
				<ul className="space-y-3">
					{engagements.map((e) => (
						<li
							key={e.id}
							className="rounded-xl border border-gray-100 bg-white px-4 py-4 shadow-sm"
						>
							<div className="flex items-start justify-between gap-3">
								<div className="min-w-0">
									<button
										onClick={() =>
											navigate(`/volunteer-opportunities/${e.opportunityId}`)
										}
										className="text-left text-sm font-semibold text-gray-900 hover:text-brand-700 transition-colors"
									>
										{e.opportunityTitle}
									</button>
									{e.message && (
										<p className="mt-1 truncate text-sm text-gray-500 italic">
											&ldquo;{e.message}&rdquo;
										</p>
									)}
									<p className="mt-1.5 text-xs text-gray-400">
										{t("myEngagements.registeredOn", {
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
										className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[e.status] ?? "bg-gray-100 text-gray-600"}`}
									>
										{STATUS_LABELS[e.status] ?? e.status}
									</span>
									{e.status === "Confirmed" && !e.isCheckedIn && (
										<button
											onClick={() => setCheckInEngagement(e)}
											className="rounded-lg bg-brand-700 px-3 py-1 text-xs font-medium text-white hover:bg-brand-800 transition-colors"
										>
											{t("checkIn.buttonLabel")}
										</button>
									)}
									{(e.status === "Pending" || e.status === "Confirmed") && (
										<button
											onClick={() => setConfirmWithdrawId(e.id)}
											className="rounded-lg border border-red-200 px-3 py-1 text-xs text-red-600 hover:bg-red-50 transition-colors"
										>
											{t("myEngagements.withdraw")}
										</button>
									)}
								</div>
							</div>
						</li>
					))}
				</ul>
			)}

			{confirmWithdrawId && (
				<ConfirmDialog
					title={t("confirmDialog.withdraw.title")}
					message={t("confirmDialog.withdraw.message")}
					confirmLabel={t("confirmDialog.withdraw.confirm")}
					onConfirm={handleWithdrawConfirm}
					onClose={handleWithdrawClose}
					loading={withdrawing}
					error={withdrawError}
				/>
			)}

			{checkInEngagement && (
				<CheckInModal
					engagementId={checkInEngagement.id}
					opportunityId={checkInEngagement.opportunityId}
					onCheckedIn={handleCheckedIn}
					onClose={() => setCheckInEngagement(null)}
				/>
			)}
		</>
	);
}
