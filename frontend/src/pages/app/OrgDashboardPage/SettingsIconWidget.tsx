import { memo } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import WidgetCard from "./WidgetCard";
import type { WidgetSizeClass } from "./widgetCatalog";
import { Cog6ToothIcon } from "../../../components/icons";

interface Props {
	organizationId: string;
	size: WidgetSizeClass;
}

function SettingsIconWidget({ organizationId, size }: Props) {
	const { t } = useTranslation();
	const title = t("orgDashboard.settingsIconWidgetTitle");

	return (
		<WidgetCard
			titleId="widget-settings-icon-title"
			title={title}
			stretchedLink={
				<Link
					to={`/app/${organizationId}/dashboard/settings`}
					className="absolute inset-0"
					aria-label={title}
				/>
			}
		>
			<div
				aria-hidden="true"
				className={`flex items-center justify-center py-2 text-brand-700 ${size === "compact" ? "flex-col gap-1" : "flex-row gap-3"}`}
			>
				<Cog6ToothIcon className="h-8 w-8" />
				<span className="text-sm font-medium">{title}</span>
			</div>
		</WidgetCard>
	);
}

export default memo(SettingsIconWidget);
