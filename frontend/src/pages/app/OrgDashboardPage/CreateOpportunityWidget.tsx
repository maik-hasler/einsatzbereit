import { memo, useState } from "react";
import { useTranslation } from "react-i18next";
import CreateVolunteerOpportunityModal from "../../../components/CreateVolunteerOpportunityModal";
import Button from "../../../components/Button";
import WidgetCard from "./WidgetCard";
import type { WidgetSizeClass } from "./widgetCatalog";
import { PlusIcon } from "../../../components/QuickActionIcons";

interface Props {
	organizationId: string;
	onCreated: (createdDraftId?: string) => void;
	size: WidgetSizeClass;
}

// Replaces the dashboard's former standalone "+ Create Opportunity" button
// (see PR #771 review feedback) - the same action, now a widget an organizer
// can place/remove like any other, auto-sized to whatever room it gets.
function CreateOpportunityWidget({ organizationId, onCreated, size }: Props) {
	const { t } = useTranslation();
	const [showCreateModal, setShowCreateModal] = useState(false);
	const compact = size === "compact";

	return (
		<WidgetCard
			titleId="widget-create-opportunity-title"
			title={t("orgDashboard.createOpportunityWidgetTitle")}
		>
			{/* The description drops out at compact size, leaving just an
			icon+label CTA - #771 follow-up review feedback (adaptive layouts
			per size). */}
			{!compact && (
				<p className="text-sm text-gray-500">
					{t("orgDashboard.createOpportunityWidgetDesc")}
				</p>
			)}
			<Button
				type="button"
				onClick={() => setShowCreateModal(true)}
				data-testid="create-opportunity-btn"
				fullWidth
				className={`shadow-sm ${compact ? "" : "mt-4"}`}
			>
				{compact && <PlusIcon />}
				{t("orgOverview.createOpportunity")}
			</Button>

			{showCreateModal && (
				<CreateVolunteerOpportunityModal
					organizationId={organizationId}
					onClose={() => setShowCreateModal(false)}
					onSuccess={onCreated}
				/>
			)}
		</WidgetCard>
	);
}

export default memo(CreateOpportunityWidget);
