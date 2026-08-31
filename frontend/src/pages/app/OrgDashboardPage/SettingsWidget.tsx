import { memo } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	OrganizationDetailsResponse,
	OrgInvitationDto,
} from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import { useSharedOrgFetch } from "../../../hooks/useSharedOrgFetch";
import WidgetCard from "./WidgetCard";
import type { WidgetSize } from "./widgetCatalog";

interface Props {
	org: OrganizationDetailsResponse;
	refreshKey: number;
	size: WidgetSize;

	isOrganizer: boolean;
}

function PendingInvitations({
	organizationId,
	refreshKey,
}: {
	organizationId: string;
	refreshKey: number;
}) {
	const { t } = useTranslation();
	const api = useApiClient();

	const [invitations] = useSharedOrgFetch<OrgInvitationDto[]>(
		`orgInvitations:${organizationId}:${refreshKey}`,
		() => api.getOrgInvitations(organizationId),
	);

	// Silent while loading and silent on failure: this is a footnote under the
	// member count, and a spinner or an error banner for it would shout louder
	// than the fact it is reporting.
	if (!invitations || invitations.length === 0) return null;

	return (
		<p
			data-testid="team-widget-invitations"
			className="mt-1 text-xs text-gray-600"
		>
			{t("orgDashboard.teamInvitationsPending", { count: invitations.length })}
		</p>
	);
}

/**
 * Who else can help run this, and who has been asked but has not answered.
 *
 * The tile used to be the organization's logo, its name and the date it was
 * created. The logo and the name are already in the page header directly above
 * the board and in the header's organization switcher, so two thirds of it was
 * a third copy of something on screen, and the creation date is trivia nobody
 * opens a dashboard for. What was actually worth having was the member count -
 * and, for an organizer, the invitations still sitting unanswered, which is the
 * one thing here that needs chasing and had nowhere on the board to say so.
 */
function SettingsWidget({ org, refreshKey, size, isOrganizer }: Props) {
	const { t } = useTranslation();
	const memberCount = org.members.length;

	return (
		<WidgetCard
			titleId="widget-settings-title"
			title={t("orgDashboard.settingsWidgetTitle")}
			action={
				isOrganizer ? (
					<Link
						to={`/app/${org.id}/dashboard/settings`}
						className="relative z-10 shrink-0 text-sm font-medium text-brand-700 hover:underline"
					>
						{t("orgDashboard.settingsEditLink")}
					</Link>
				) : undefined
			}
			stretchedLink={
				<Link
					to={`/app/${org.id}/dashboard/members`}
					className="absolute inset-0"
					aria-label={t("orgDashboard.teamManageLink")}
				/>
			}
		>
			<div className="min-w-0">
				<p
					data-testid="team-widget-member-count"
					className="font-display text-3xl font-bold text-gray-900 tabular-nums"
				>
					{memberCount}
				</p>
				<p className="text-xs text-gray-500 sm:text-sm">
					{t("orgDashboard.teamMemberCount", { count: memberCount })}
				</p>

				{/* One grid row holds the count and its label and nothing else. */}
				{isOrganizer && size.height !== "strip" && (
					<PendingInvitations organizationId={org.id} refreshKey={refreshKey} />
				)}
			</div>
		</WidgetCard>
	);
}

export default memo(SettingsWidget);
