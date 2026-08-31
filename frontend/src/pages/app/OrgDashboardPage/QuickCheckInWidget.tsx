import { lazy, memo, Suspense, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import { dispatchToast } from "../../../lib/toastBus";
import { inputSurfaceClass } from "../../../lib/formClasses";
import {
	filterCheckInOpportunities,
	isQrCheckIn,
	pickCheckInOpportunity,
} from "../../../lib/quickCheckIn";
import { formatDateTimeRange, pickLocalizedText } from "../../../lib/format";
import Skeleton from "../../../components/Skeleton";
import Button from "../../../components/Button";
import Dropdown from "../../../components/Dropdown";
import ErrorBanner from "../../../components/ErrorBanner";
import EmptyState from "../../../components/EmptyState";
import ModalLoadingFallback from "../../../components/ModalLoadingFallback";
import CreateVolunteerOpportunityModal from "../../../components/CreateVolunteerOpportunityModal";
import WidgetCard from "./WidgetCard";
import { useSharedOrgFetch } from "../../../hooks/useSharedOrgFetch";

const OPPORTUNITY_PAGE_SIZE = 100;

const QRScannerModal = lazy(() => import("../../../components/QRScannerModal"));

interface Props {
	organizationId: string;
	refreshKey: number;
	onOpportunityCreated: (createdDraftId?: string) => void;
}

function QuickCheckInWidget({
	organizationId,
	refreshKey,
	onOpportunityCreated,
}: Props) {
	const { t, i18n } = useTranslation();
	const api = useApiClient();

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

	const checkInOpportunities = useMemo(
		() =>
			opportunities === null ? null : filterCheckInOpportunities(opportunities),
		[opportunities],
	);
	const [selectedId, setSelectedId] = useState("");
	const [scannerOpen, setScannerOpen] = useState(false);
	const [showCreateModal, setShowCreateModal] = useState(false);

	// Derived with a fallback rather than seeded into state by an effect: the
	// action below is a scanner for a QR opportunity and a link for the rest,
	// so a render where the list has arrived but the selection has not yet
	// caught up would paint a link to nowhere.
	//
	// The fallback is the opportunity being run right now, not whichever one the
	// API listed first - check-in is a thing organizers do standing at a door,
	// and the right default is the door they are standing at.
	const selected =
		checkInOpportunities?.find((o) => o.id === selectedId) ??
		(checkInOpportunities
			? pickCheckInOpportunity(checkInOpportunities, Date.now())
			: undefined);
	const scannable = isQrCheckIn(selected);

	// Which occurrence the action below will actually check people into, spelled
	// out under the picker. Two Einsaetze can read almost identically in a
	// dropdown; their slot times never do.
	const selectedWhen =
		selected?.nextTimeSlotStart && selected.nextTimeSlotEnd
			? formatDateTimeRange(
					selected.nextTimeSlotStart as unknown as string,
					selected.nextTimeSlotEnd as unknown as string,
					i18n.language,
				)
			: null;

	function startScanning() {
		if (!selected) return;
		setScannerOpen(true);
	}

	return (
		<WidgetCard
			titleId="widget-quick-checkin-title"
			title={t("orgDashboard.quickCheckInWidgetTitle")}
		>
			{checkInOpportunities === null && !error && (
				<div role="status" className="space-y-3">
					<span className="sr-only">{t("orgDashboard.loading")}</span>
					<Skeleton className="h-9 w-full rounded-xl" />
					<Skeleton className="h-9 w-full rounded-xl" />
				</div>
			)}
			{error && <ErrorBanner message={error} />}
			{checkInOpportunities !== null &&
				!error &&
				checkInOpportunities.length === 0 && (
					<EmptyState
						compact
						title={t("orgDashboard.quickCheckInNoOpportunities")}
						action={{
							label: t("orgDashboard.emptyStateCreateAction"),
							onClick: () => setShowCreateModal(true),
						}}
					/>
				)}
			{checkInOpportunities !== null &&
				!error &&
				checkInOpportunities.length > 0 && (
					<div className="space-y-3">
						<div>
							<label htmlFor="quick-checkin-opportunity" className="sr-only">
								{t("orgDashboard.quickCheckInSelectOpportunity")}
							</label>
							<Dropdown
								id="quick-checkin-opportunity"
								value={selected?.id ?? ""}
								onChange={setSelectedId}
								className={inputSurfaceClass}
								options={checkInOpportunities.map((o) => ({
									value: o.id,
									label:
										pickLocalizedText(o.titleDe, o.titleEn, i18n.language)
											.text || t("orgDashboard.unnamedDraft"),
								}))}
							/>
						</div>
						{/*
							Only QR check-in can be finished here; PIN and manual both
							need the opportunity's own sign-up list (#2322 F6).
						*/}
						{selectedWhen && (
							<p
								data-testid="quick-checkin-selected-when"
								className="text-xs text-gray-600 tabular-nums"
							>
								{selectedWhen}
							</p>
						)}

						{scannable ? (
							<Button
								type="button"
								onClick={startScanning}
								data-testid="quick-checkin-scan-btn"
								fullWidth
								className="shadow-sm"
							>
								{t("orgDashboard.quickCheckInOpenScanner")}
							</Button>
						) : (
							<Button
								to={`/app/${organizationId}/dashboard/opportunities/${selected?.id ?? ""}/engagements`}
								data-testid="quick-checkin-open-btn"
								fullWidth
								className="shadow-sm"
							>
								{t("orgDashboard.quickCheckInOpenSignUps")}
							</Button>
						)}
					</div>
				)}

			{scannerOpen && (
				<Suspense
					fallback={
						<ModalLoadingFallback onClose={() => setScannerOpen(false)} />
					}
				>
					<QRScannerModal
						opportunityId={selected?.id ?? ""}
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
