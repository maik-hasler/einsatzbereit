import { memo, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import { dispatchToast } from "../../../lib/toastBus";
import QRScannerModal from "../../../components/QRScannerModal";
import Spinner from "../../../components/Spinner";
import Button from "../../../components/Button";
import ErrorBanner from "../../../components/ErrorBanner";
import WidgetCard from "./WidgetCard";
import { useSharedOrgFetch } from "../../../hooks/useSharedOrgFetch";
import type { WidgetSizeClass } from "./widgetCatalog";

const OPPORTUNITY_PAGE_SIZE = 100;

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
	// organization-wide published opportunities - see useSharedOrgFetch.
	const [opportunities, , error] = useSharedOrgFetch<
		VolunteerOpportunitySummary[]
	>(`opportunities:${organizationId}:${refreshKey}`, () =>
		api
			.getOrganizationOpportunities(
				organizationId,
				"Published",
				1,
				OPPORTUNITY_PAGE_SIZE,
			)
			.then((page) => page.items),
	);
	const [selectedId, setSelectedId] = useState("");
	const [scannerOpen, setScannerOpen] = useState(false);

	useEffect(() => {
		if (opportunities === null) return;
		setSelectedId((current) => current || (opportunities[0]?.id ?? ""));
	}, [opportunities]);

	function startScanning() {
		if (!selectedId) return;
		setScannerOpen(true);
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
						onClick={startScanning}
						data-testid="quick-checkin-scan-btn"
						className={`shadow-sm ${size !== "compact" ? "shrink-0" : "w-full"}`}
					>
						{t("orgDashboard.quickCheckInOpenScanner")}
					</Button>
				</div>
			)}

			{scannerOpen && (
				<QRScannerModal
					onCheckedIn={() => {
						dispatchToast("success", t("checkIn.success"));
					}}
					onClose={() => setScannerOpen(false)}
				/>
			)}
		</WidgetCard>
	);
}

export default memo(QuickCheckInWidget);
