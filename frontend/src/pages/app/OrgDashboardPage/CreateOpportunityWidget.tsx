import { useState } from "react";
import { useTranslation } from "react-i18next";
import CreateVolunteerOpportunityModal from "../../../components/CreateVolunteerOpportunityModal";
import WidgetCard from "./WidgetCard";

interface Props {
	organizationId: string;
	onCreated: (createdDraftId?: string) => void;
}

// Replaces the dashboard's former standalone "+ Create Opportunity" button
// (see PR #771 review feedback) - the same action, now a widget an organizer
// can place/resize/remove like any other.
export default function CreateOpportunityWidget({
	organizationId,
	onCreated,
}: Props) {
	const { t } = useTranslation();
	const [showCreateModal, setShowCreateModal] = useState(false);

	return (
		<WidgetCard
			titleId="widget-create-opportunity-title"
			title={t("orgDashboard.createOpportunityWidgetTitle")}
		>
			<p className="text-sm text-gray-500">
				{t("orgDashboard.createOpportunityWidgetDesc")}
			</p>
			<button
				type="button"
				onClick={() => setShowCreateModal(true)}
				data-testid="create-opportunity-btn"
				className="mt-4 inline-flex w-full items-center justify-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-brand-800 focus:outline-none"
			>
				{t("orgOverview.createOpportunity")}
			</button>

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
