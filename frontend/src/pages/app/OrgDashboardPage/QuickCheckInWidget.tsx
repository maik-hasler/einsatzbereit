import { lazy, memo, Suspense, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import { dispatchToast } from "../../../lib/toastBus";
import Skeleton from "../../../components/Skeleton";
import Button from "../../../components/Button";
import Dropdown from "../../../components/Dropdown";
import ErrorBanner from "../../../components/ErrorBanner";
import EmptyState from "../../../components/EmptyState";
import ModalLoadingFallback from "../../../components/ModalLoadingFallback";
import CreateVolunteerOpportunityModal from "../../../components/CreateVolunteerOpportunityModal";
import WidgetCard from "./WidgetCard";
import { useSharedOrgFetch } from "../../../hooks/useSharedOrgFetch";
import type { WidgetSizeClass } from "./widgetCatalog";

const OPPORTUNITY_PAGE_SIZE = 100;

// Lazy-loaded: the camera/barcode-scanning code is only needed once an
// organizer actually opens the scanner, not on every dashboard visit - #971.
const QRScannerModal = lazy(() => import("../../../components/QRScannerModal"));

interface Props {
	organizationId: string;
	refreshKey: number;
	size: WidgetSizeClass;
	onOpportunityCreated: (createdDraftId?: string) => void;
}

// Lets an organizer jump straight to the QR scanner for any of their
// published opportunities, instead of navigating to that opportunity's
// engagement management page first.
function QuickCheckInWidget({
	organizationId,
	refreshKey,
	size,
	onOpportunityCreated,
}: Props) {
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
	const [showCreateModal, setShowCreateModal] = useState(false);

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
				<div
					role="status"
					className={
						size !== "compact" ? "flex items-center gap-3" : "space-y-3"
					}
				>
					<span className="sr-only">{t("orgDashboard.loading")}</span>
					<Skeleton
						className={`h-9 rounded-xl ${size !== "compact" ? "min-w-0 flex-1" : "w-full"}`}
					/>
					<Skeleton
						className={`h-9 rounded-lg ${size !== "compact" ? "w-24 shrink-0" : "w-full"}`}
					/>
				</div>
			)}
			{error && <ErrorBanner message={error} />}
			{opportunities !== null && !error && opportunities.length === 0 && (
				<EmptyState
					compact
					title={t("orgDashboard.quickCheckInNoOpportunities")}
					action={{
						label: t("orgDashboard.emptyStateCreateAction"),
						onClick: () => setShowCreateModal(true),
					}}
				/>
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
						<Dropdown
							id="quick-checkin-opportunity"
							value={selectedId}
							onChange={setSelectedId}
							className="w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400 focus:ring-2 focus:ring-brand-400/30 focus:outline-none"
							options={opportunities.map((o) => ({
								value: o.id,
								label: o.title || t("orgDashboard.unnamedDraft"),
							}))}
						/>
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
				<Suspense
					fallback={
						<ModalLoadingFallback onClose={() => setScannerOpen(false)} />
					}
				>
					<QRScannerModal
						onCheckedIn={() => {
							dispatchToast("success", t("checkIn.success"));
						}}
						onClose={() => setScannerOpen(false)}
					/>
				</Suspense>
			)}

			{showCreateModal && (
				<CreateVolunteerOpportunityModal
					organizationId={organizationId}
					onClose={() => setShowCreateModal(false)}
					onSuccess={onOpportunityCreated}
				/>
			)}
		</WidgetCard>
	);
}

export default memo(QuickCheckInWidget);
