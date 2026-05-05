import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import type { EngagementSummary } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";

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
	const [engagements, setEngagements] = useState<EngagementSummary[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [withdrawing, setWithdrawing] = useState<string | null>(null);

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
			.catch((err) => setError(err.message))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	async function handleWithdraw(engagementId: string) {
		setWithdrawing(engagementId);
		try {
			const updated = await api.withdrawEngagement(engagementId);
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === engagementId ? { ...e, status: updated.status } : e,
				),
			);
		} catch (err) {
			alert(err instanceof Error ? err.message : t("myEngagements.withdrawError"));
		} finally {
			setWithdrawing(null);
		}
	}

	return (
		<>
			<h1 className="mb-6 text-2xl font-bold text-gray-900">
				{t("myEngagements.title")}
			</h1>

			{loading && <p className="text-gray-500">{t("myEngagements.loading")}</p>}
			{error && <p className="text-red-600">{t("myEngagements.error", { message: error })}</p>}

			{!loading && !error && engagements.length === 0 && (
				<div className="text-center py-12">
					<p className="text-gray-500 mb-4">{t("myEngagements.noEngagements")}</p>
					<button
						onClick={() => navigate("/")}
						className="rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800"
					>
						{t("myEngagements.exploreNeeds")}
					</button>
				</div>
			)}

			{!loading && !error && engagements.length > 0 && (
				<ul className="space-y-3">
					{engagements.map((e) => (
						<li key={e.id} className="rounded border p-4">
							<div className="flex items-start justify-between gap-2">
								<div className="min-w-0">
									<button
										onClick={() =>
											navigate(`/volunteer-opportunities/${e.opportunityId}`)
										}
										className="text-sm font-medium text-gray-900 hover:underline text-left"
									>
										{t("myEngagements.viewOpportunity")}
									</button>
									{e.message && (
										<p className="mt-1 text-sm text-gray-500 truncate">
											&ldquo;{e.message}&rdquo;
										</p>
									)}
									<p className="mt-1 text-xs text-gray-400">
										{t("myEngagements.registeredOn", {
											date: new Date(e.createdOn).toLocaleDateString(locale),
										})}
									</p>
								</div>
								<div className="flex flex-col items-end gap-2 shrink-0">
									<span
										className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[e.status] ?? "bg-gray-100 text-gray-600"}`}
									>
										{STATUS_LABELS[e.status] ?? e.status}
									</span>
									{(e.status === "Pending" || e.status === "Confirmed") && (
										<button
											onClick={() => handleWithdraw(e.id)}
											disabled={withdrawing === e.id}
											className="text-xs text-red-600 hover:underline disabled:opacity-50"
										>
											{withdrawing === e.id ? t("myEngagements.withdrawing") : t("myEngagements.withdraw")}
										</button>
									)}
								</div>
							</div>
						</li>
					))}
				</ul>
			)}
		</>
	);
}
