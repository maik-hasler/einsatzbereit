import { useState } from "react";
import { useNavigate, useOutletContext } from "react-router";
import { useTranslation } from "react-i18next";
import CreateVolunteerOpportunityModal from "../../../components/CreateVolunteerOpportunityModal";
import type { OrgAppContext } from "../../../layouts/OrgAppLayout";
import CalendarWidget from "./CalendarWidget";
import UpcomingOpportunitiesWidget from "./UpcomingOpportunitiesWidget";
import ToDoWidget from "./ToDoWidget";
import SettingsWidget from "./SettingsWidget";

export default function OrgDashboardPage() {
	const { org } = useOutletContext<OrgAppContext>();
	const { t } = useTranslation();
	const navigate = useNavigate();
	const organizationId = org.id;

	const [showCreateModal, setShowCreateModal] = useState(false);
	// Bumped after a published opportunity is created so the Calendar and
	// Upcoming Opportunities widgets (which each own their own data) refetch.
	const [refreshKey, setRefreshKey] = useState(0);

	function handleOpportunityCreated(createdDraftId?: string) {
		// Drafts live on the Opportunities tab now. When one is saved from here,
		// take the organizer there with the new draft highlighted so it is never
		// lost (issue #708). A published opportunity just refreshes the widgets.
		if (createdDraftId) {
			navigate(
				`/app/${organizationId}/opportunities?highlight=${createdDraftId}`,
			);
			return;
		}
		setRefreshKey((k) => k + 1);
	}

	return (
		<div>
			<div className="mb-6 flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
				<h1 className="w-full min-w-0 break-words text-2xl font-bold text-gray-900 sm:w-auto">
					{org.name}
				</h1>
				<button
					type="button"
					onClick={() => setShowCreateModal(true)}
					data-testid="create-opportunity-btn"
					className="inline-flex w-full shrink-0 items-center justify-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-brand-800 focus:outline-none sm:w-auto"
				>
					{t("orgOverview.createOpportunity")}
				</button>
			</div>

			<div className="space-y-6">
				<div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
					<ToDoWidget organizationId={organizationId} />
					<UpcomingOpportunitiesWidget
						organizationId={organizationId}
						refreshKey={refreshKey}
					/>
				</div>

				<CalendarWidget
					organizationId={organizationId}
					refreshKey={refreshKey}
				/>

				<SettingsWidget org={org} />
			</div>

			{showCreateModal && (
				<CreateVolunteerOpportunityModal
					organizationId={organizationId}
					onClose={() => setShowCreateModal(false)}
					onSuccess={handleOpportunityCreated}
				/>
			)}
		</div>
	);
}
