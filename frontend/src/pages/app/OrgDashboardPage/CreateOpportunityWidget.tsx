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

function CreateOpportunityWidget({ organizationId, onCreated, size }: Props) {
	const { t } = useTranslation();
	const [showCreateModal, setShowCreateModal] = useState(false);
	const compact = size === "compact";

	return (
		<WidgetCard
			titleId="widget-create-opportunity-title"
			title={t("orgDashboard.createOpportunityWidgetTitle")}
		>
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
