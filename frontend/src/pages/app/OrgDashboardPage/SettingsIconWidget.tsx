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

// A minimal shortcut tile to the settings page - distinct from
// SettingsWidget's full organization summary card, for organizers who just
// want a quick link rather than a preview. Icon-only at its default compact
// footprint; a wider placement (#15) has room to also spell out the label
// instead of just growing empty whitespace around the icon.
function SettingsIconWidget({ organizationId, size }: Props) {
	const { t } = useTranslation();
	const title = t("orgDashboard.settingsIconWidgetTitle");

	return (
		<WidgetCard
			titleId="widget-settings-icon-title"
			title={title}
			className="relative"
		>
			<Link
				to={`/app/${organizationId}/dashboard/settings`}
				className="absolute inset-0"
				aria-label={title}
			/>
			<div
				aria-hidden="true"
				className={`flex items-center justify-center py-2 text-brand-700 ${size === "compact" ? "flex-col" : "flex-row gap-3"}`}
			>
				<Cog6ToothIcon className="h-8 w-8" />
				{size !== "compact" && (
					<span className="text-sm font-medium">{title}</span>
				)}
			</div>
		</WidgetCard>
	);
}

export default memo(SettingsIconWidget);
