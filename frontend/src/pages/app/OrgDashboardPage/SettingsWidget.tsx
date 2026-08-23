import { memo } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrganizationDetailsResponse } from "../../../client/api-client";
import WidgetCard from "./WidgetCard";
import type { WidgetSizeClass } from "./widgetCatalog";
import { formatDate } from "../../../lib/format";

interface Props {
	org: OrganizationDetailsResponse;
	size: WidgetSizeClass;
}

function SettingsWidget({ org, size }: Props) {
	const { t, i18n } = useTranslation();
	const compact = size === "compact";

	const logo = org.logoUrl ? (
		<img
			src={org.logoUrl}
			alt=""
			width={48}
			height={48}
			className="h-12 w-12 shrink-0 rounded-lg object-contain ring-1 ring-gray-200"
		/>
	) : (
		<span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-brand-100 text-lg font-semibold text-brand-700">
			{org.name.charAt(0).toUpperCase()}
		</span>
	);

	return (
		<WidgetCard
			titleId="widget-settings-title"
			title={t("orgDashboard.settingsWidgetTitle")}
			action={
				<Link
					to={`/app/${org.id}/dashboard/settings`}
					className="shrink-0 text-sm font-medium text-brand-700 hover:underline"
				>
					{t("orgDashboard.settingsEditLink")}
				</Link>
			}
		>
			<div
				className={
					compact
						? "flex flex-col items-center gap-3 text-center"
						: "flex min-w-0 items-center gap-3"
				}
			>
				{logo}
				<div className="min-w-0">
					<p className="truncate text-sm font-semibold text-gray-900">
						{org.name}
					</p>
					<p className="text-xs text-gray-500">
						<Link
							to={`/app/${org.id}/dashboard/members`}
							className="text-brand-700 underline"
						>
							{t("orgDashboard.settingsMemberCount", {
								count: org.members.length,
							})}
						</Link>
						{!compact && (
							<>
								<span className="mx-1.5">&middot;</span>
								{t("orgSettings.createdOn", {
									date: formatDate(
										org.createdOn as unknown as string,
										i18n.language,
									),
								})}
							</>
						)}
					</p>
				</div>
			</div>
		</WidgetCard>
	);
}

export default memo(SettingsWidget);
