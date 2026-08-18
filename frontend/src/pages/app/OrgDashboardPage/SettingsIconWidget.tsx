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
// want a quick link rather than a preview. #2045: the label is always
// visible, even at this widget's own tiny default compact footprint - a
// bare gear icon with no on-screen text gave a sighted organizer no clue
// what it was or that it was clickable, even though the link already had an
// accessible name (aria-label below) for assistive tech.
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
				className={`flex items-center justify-center py-2 text-brand-700 ${size === "compact" ? "flex-col gap-1" : "flex-row gap-3"}`}
			>
				<Cog6ToothIcon className="h-8 w-8" />
				<span className="text-sm font-medium">{title}</span>
			</div>
		</WidgetCard>
	);
}

export default memo(SettingsIconWidget);
