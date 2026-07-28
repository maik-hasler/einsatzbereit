import { lazy, memo, Suspense, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type {
	EngagementSummary,
	VolunteerOpportunitySummary,
} from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import { dispatchToast } from "../../../lib/toastBus";
import { getApiErrorMessage } from "../../../lib/apiError";
import Spinner from "../../../components/Spinner";
import Button from "../../../components/Button";
import ErrorBanner from "../../../components/ErrorBanner";
import ModalLoadingFallback from "../../../components/ModalLoadingFallback";
import WidgetCard from "./WidgetCard";
import { useSharedOrgFetch } from "../../../hooks/useSharedOrgFetch";
import type { WidgetSizeClass } from "./widgetCatalog";

// Lazy-loaded: the camera/barcode-scanning code is only needed once an
// organizer actually opens the scanner, not on every dashboard visit - #971.
const QRScannerModal = lazy(() => import("../../../components/QRScannerModal"));

interface Props {
	organizationId: string;
	refreshKey: number;
	size: WidgetSizeClass;
}

// Lets an organizer jump straight to the QR scanner for any of their
// published opportunities, instead of navigating to that opportunity's
// engagement management page first.
function QuickCheckInWidget({ organizationId, refreshKey, size }: Props) {
	const { t } = useTranslation();
	const api = useApiClient();

	// Shared with UpcomingOpportunitiesWidget, which fetches the same
	// organization-wide opportunities - see useSharedOrgFetch.
	const [allOpportunities, , error] = useSharedOrgFetch<
		VolunteerOpportunitySummary[]
	>(`opportunities:${organizationId}:${refreshKey}`, () =>
		api.getOrganizationOpportunities(organizationId),
	);
	const opportunities = useMemo(
		() => allOpportunities?.filter((o) => o.status === "Published") ?? null,
		[allOpportunities],
	);
	const [selectedId, setSelectedId] = useState("");
	const [engagements, setEngagements] = useState<EngagementSummary[] | null>(
		null,
	);
	const [loadingEngagements, setLoadingEngagements] = useState(false);

	useEffect(() => {
		if (opportunities === null) return;
		setSelectedId((current) => current || (opportunities[0]?.id ?? ""));
	}, [opportunities]);

	async function startScanning() {
		if (!selectedId) return;
		setLoadingEngagements(true);
		try {
			const data = await api.getEngagements(selectedId);
			setEngagements(data);
		} catch (e) {
			dispatchToast("error", getApiErrorMessage(e, t("error.serverError")));
		} finally {
			setLoadingEngagements(false);
		}
	}

	return (
		<WidgetCard
			titleId="widget-quick-checkin-title"
			title={t("orgDashboard.quickCheckInWidgetTitle")}
		>
			{opportunities === null && !error && (
				<Spinner label={t("orgDashboard.loading")} size="sm" />
			)}
			{error && <ErrorBanner message={error} />}
			{opportunities !== null && !error && opportunities.length === 0 && (
				<p className="text-sm text-gray-500">
					{t("orgDashboard.quickCheckInNoOpportunities")}
				</p>
			)}
			{opportunities !== null && !error && opportunities.length > 0 && (
				// Side by side once there's enough width for both to stay
				// readable - the select grows to fill whatever room it's
				// given via flex-1, so this already scales continuously with
				// the organizer's own placement instead of needing a
				// separate "full" treatment; stacked at compact - #771
				// follow-up review feedback (adaptive layouts per size).
				<div
					className={
						size !== "compact" ? "flex items-center gap-3" : "space-y-3"
					}
				>
					<div className={size !== "compact" ? "min-w-0 flex-1" : undefined}>
						<label htmlFor="quick-checkin-opportunity" className="sr-only">
							{t("orgDashboard.quickCheckInSelectOpportunity")}
						</label>
						<select
							id="quick-checkin-opportunity"
							value={selectedId}
							onChange={(e) => setSelectedId(e.target.value)}
							className="block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
						>
							{opportunities.map((o) => (
								<option key={o.id} value={o.id}>
									{o.title || t("orgDashboard.unnamedDraft")}
								</option>
							))}
						</select>
					</div>
					<Button
						type="button"
						onClick={() => void startScanning()}
						disabled={loadingEngagements}
						data-testid="quick-checkin-scan-btn"
						className={`shadow-sm ${size !== "compact" ? "shrink-0" : "w-full"}`}
					>
						{loadingEngagements
							? t("orgDashboard.loading")
							: t("orgDashboard.quickCheckInOpenScanner")}
					</Button>
				</div>
			)}

			{engagements !== null && (
				<Suspense
					fallback={
						<ModalLoadingFallback onClose={() => setEngagements(null)} />
					}
				>
					<QRScannerModal
						engagements={engagements}
						onCheckedIn={() => {
							dispatchToast("success", t("checkIn.success"));
						}}
						onClose={() => setEngagements(null)}
					/>
				</Suspense>
			)}
		</WidgetCard>
	);
}

export default memo(QuickCheckInWidget);
