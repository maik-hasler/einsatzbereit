import { useEffect, useState } from "react";
import { useParams, Link } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	OrganizationDashboardResponse,
	VolunteerOpportunitySummary,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import Breadcrumb from "../components/Breadcrumb";
import { usePageTitle } from "../hooks/usePageTitle";

interface KpiCardProps {
	label: string;
	description: string;
	value: number | undefined;
	loading: boolean;
	to: string;
	color: "blue" | "yellow" | "green" | "red";
}

function KpiCard({
	label,
	description,
	value,
	loading,
	to,
	color,
}: KpiCardProps) {
	const colorMap = {
		blue: {
			bg: "bg-blue-50",
			border: "border-blue-100",
			text: "text-blue-700",
			value: "text-blue-900",
			hover: "hover:bg-blue-100",
		},
		yellow: {
			bg: "bg-yellow-50",
			border: "border-yellow-100",
			text: "text-yellow-700",
			value: "text-yellow-900",
			hover: "hover:bg-yellow-100",
		},
		green: {
			bg: "bg-green-50",
			border: "border-green-100",
			text: "text-green-700",
			value: "text-green-900",
			hover: "hover:bg-green-100",
		},
		red: {
			bg: "bg-red-50",
			border: "border-red-100",
			text: "text-red-700",
			value: "text-red-900",
			hover: "hover:bg-red-100",
		},
	};

	const c = colorMap[color];

	return (
		<Link
			to={to}
			className={`block rounded-xl border ${c.border} ${c.bg} ${c.hover} p-6 transition-colors`}
		>
			<p className={`text-sm font-medium ${c.text}`}>{label}</p>
			<p className={`mt-2 text-4xl font-bold tabular-nums ${c.value}`}>
				{loading ? (
					<span className="inline-block h-10 w-16 animate-pulse rounded bg-current opacity-20" />
				) : (
					value
				)}
			</p>
			<p className={`mt-1 text-xs ${c.text} opacity-75`}>{description}</p>
		</Link>
	);
}

export default function OrganizationDashboardPage() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const api = useApiClient();
	const { t } = useTranslation();

	const [data, setData] = useState<OrganizationDashboardResponse | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [drafts, setDrafts] = useState<VolunteerOpportunitySummary[]>([]);
	usePageTitle(data?.name ?? t("orgDashboard.title"));

	useEffect(() => {
		if (!organizationId) return;
		setLoading(true);
		setError(null);
		api
			.getOrganizationDashboard(organizationId)
			.then(setData)
			.catch((e: unknown) =>
				setError(e instanceof Error ? e.message : String(e)),
			)
			.finally(() => setLoading(false));
		api
			.getOrganizationOpportunityDrafts(organizationId)
			.then(setDrafts)
			.catch(() => {});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	if (error) {
		return (
			<div className="mx-auto max-w-4xl px-4 py-8">
				<p className="text-red-600">
					{t("orgDashboard.error", { message: error })}
				</p>
			</div>
		);
	}

	return (
		<div className="mx-auto max-w-4xl px-4 py-8">
			<Breadcrumb
				items={[
					{ label: t("breadcrumb.home"), href: "/" },
					{ label: t("orgDashboard.title") },
				]}
			/>

			<h1 className="mt-4 text-2xl font-bold text-gray-900">
				{t("orgDashboard.title")}
			</h1>

			<div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2">
				<KpiCard
					label={t("orgDashboard.openOpportunities")}
					description={t("orgDashboard.openOpportunitiesDesc")}
					value={data?.openOpportunities}
					loading={loading}
					to={`/organizations/${organizationId}`}
					color="blue"
				/>
				<KpiCard
					label={t("orgDashboard.pendingEngagements")}
					description={t("orgDashboard.pendingEngagementsDesc")}
					value={data?.pendingEngagements}
					loading={loading}
					to={`/organizations/${organizationId}`}
					color="yellow"
				/>
				<KpiCard
					label={t("orgDashboard.confirmedNext7Days")}
					description={t("orgDashboard.confirmedNext7DaysDesc")}
					value={data?.confirmedEngagementsNext7Days}
					loading={loading}
					to={`/organizations/${organizationId}`}
					color="green"
				/>
				<KpiCard
					label={t("orgDashboard.cancelledEngagements")}
					description={t("orgDashboard.cancelledEngagementsDesc")}
					value={data?.cancelledEngagements}
					loading={loading}
					to={`/organizations/${organizationId}`}
					color="red"
				/>
			</div>

			{drafts.length > 0 && (
				<section className="mt-8" data-testid="drafts-section">
					<h2 className="text-lg font-semibold text-gray-900">
						{t("orgDashboard.draftsTitle")}
					</h2>
					<p className="mt-1 text-sm text-gray-500">
						{t("orgDashboard.draftsDesc")}
					</p>
					<ul className="mt-4 space-y-3">
						{drafts.map((draft) => (
							<li
								key={draft.id}
								className="relative rounded-2xl border border-gray-100 bg-white p-4 shadow-sm transition hover:border-brand-200 hover:shadow-md"
							>
								<Link
									to={`/volunteer-opportunities/${draft.id}`}
									className="absolute inset-0"
									aria-label={draft.title}
								/>
								<div className="flex items-center justify-between gap-3">
									<div className="min-w-0">
										<p className="truncate text-sm font-semibold text-gray-900">
											{draft.title}
										</p>
										{draft.description && (
											<p className="mt-0.5 line-clamp-1 text-xs text-gray-500">
												{draft.description}
											</p>
										)}
									</div>
									<span className="shrink-0 rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-800">
										{t("opportunities.draftBadge")}
									</span>
								</div>
							</li>
						))}
					</ul>
				</section>
			)}
		</div>
	);
}
