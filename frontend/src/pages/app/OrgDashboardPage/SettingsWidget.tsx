import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrganizationDetailsResponse } from "../../../client/api-client";
import WidgetCard from "./WidgetCard";

interface Props {
	org: OrganizationDetailsResponse;
}

export default function SettingsWidget({ org }: Props) {
	const { t, i18n } = useTranslation();
	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

	return (
		<WidgetCard
			titleId="widget-settings-title"
			title={t("orgDashboard.settingsWidgetTitle")}
			action={
				<Link
					to={`/app/${org.id}/settings`}
					className="shrink-0 text-sm font-medium text-brand-700 hover:underline"
				>
					{t("orgDashboard.settingsEditLink")}
				</Link>
			}
		>
			<div className="flex min-w-0 items-center gap-3">
				{org.logoUrl ? (
					<img
						src={org.logoUrl}
						alt=""
						className="h-12 w-12 shrink-0 rounded-lg object-contain ring-1 ring-gray-200"
					/>
				) : (
					<span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-brand-100 text-lg font-semibold text-brand-700">
						{org.name.charAt(0).toUpperCase()}
					</span>
				)}
				<div className="min-w-0">
					<div className="flex items-center gap-2">
						<p className="truncate text-sm font-semibold text-gray-900">
							{org.name}
						</p>
						{org.isVerified && (
							<span className="shrink-0 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-medium text-brand-700">
								{t("orgProfile.verified")}
							</span>
						)}
					</div>
					<p className="text-xs text-gray-500">
						{t("orgDashboard.settingsMemberCount", {
							count: org.members.length,
						})}
						<span className="mx-1.5">&middot;</span>
						{t("orgSettings.createdOn", {
							date: new Date(org.createdOn).toLocaleDateString(locale, {
								day: "2-digit",
								month: "long",
								year: "numeric",
							}),
						})}
					</p>
				</div>
			</div>
		</WidgetCard>
	);
}
