import { memo, useState } from "react";
import { useTranslation } from "react-i18next";
import CreateVolunteerOpportunityModal from "../../../components/CreateVolunteerOpportunityModal";
import Button from "../../../components/Button";
import { PlusIcon, UsersIcon } from "../../../components/icons";
import WidgetCard from "./WidgetCard";
import type { WidgetSize } from "./widgetCatalog";

interface Props {
	organizationId: string;
	onCreated: (createdDraftId?: string) => void;
	size: WidgetSize;
}

/**
 * The two things that make an organization work: something to do, and people to
 * do it.
 *
 * This tile used to be a card whose entire body was one button, under a title
 * that repeated the button's own label. Card chrome - a border, a shadow, a
 * heading and 16px of padding on four sides - is a lot to spend on hosting a
 * single control, and it left the widget with nothing to say at any size.
 * Everything else an organizer might reach for is already a tab in the section
 * nav above the board; these two are flows, not pages, so this is the only
 * place they can be started from in one click.
 *
 * The widget key stays `CreateOpportunity` on purpose: it is what already-saved
 * dashboard layouts and the persisted DashboardWidgetKey enum call this slot,
 * and creating an opportunity is still its primary action.
 */
function CreateOpportunityWidget({ organizationId, onCreated, size }: Props) {
	const { t } = useTranslation();
	const [showCreateModal, setShowCreateModal] = useState(false);

	// Side by side only where a single grid row is also wide enough for two
	// German labels; everywhere else the buttons stack full-width.
	const inline = size.height === "strip" && size.width !== "compact";

	return (
		<WidgetCard
			titleId="widget-create-opportunity-title"
			title={t("orgDashboard.quickActionsWidgetTitle")}
		>
			<div
				className={
					inline ? "flex flex-wrap items-center gap-2" : "flex flex-col gap-2"
				}
			>
				<Button
					type="button"
					onClick={() => setShowCreateModal(true)}
					data-testid="create-opportunity-btn"
					fullWidth={!inline}
					className="shadow-sm"
				>
					<PlusIcon />
					{t("orgOverview.createOpportunity")}
				</Button>

				<Button
					to={`/app/${organizationId}/dashboard/members`}
					variant="outline"
					fullWidth={!inline}
					data-testid="quick-action-invite-member"
				>
					<UsersIcon className="h-4 w-4" />
					{t("orgDashboard.quickActionsInvite")}
				</Button>
			</div>

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
