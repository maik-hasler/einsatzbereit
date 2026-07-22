import { memo } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import WidgetCard from "./WidgetCard";
import type { WidgetSizeClass } from "./widgetCatalog";

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
				<svg
					className="h-8 w-8"
					fill="none"
					viewBox="0 0 24 24"
					strokeWidth="1.5"
					stroke="currentColor"
				>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.324.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.296 2.247a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.24-.438.613-.431.992a6.759 6.759 0 0 1 0 .255c-.007.378.138.75.43.99l1.005.828c.424.35.534.954.26 1.43l-1.298 2.247a1.125 1.125 0 0 1-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.57 6.57 0 0 1-.22.128c-.331.183-.581.495-.644.869l-.213 1.28c-.09.543-.56.941-1.11.941h-2.594c-.55 0-1.02-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 0 1-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.297-2.247a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.992a6.932 6.932 0 0 1 0-.255c.007-.378-.138-.75-.43-.99l-1.004-.828a1.125 1.125 0 0 1-.26-1.43l1.297-2.247a1.125 1.125 0 0 1 1.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.087.22-.128.332-.183.582-.495.644-.869l.214-1.28Z"
					/>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
					/>
				</svg>
				{size !== "compact" && (
					<span className="text-sm font-medium">{title}</span>
				)}
			</div>
		</WidgetCard>
	);
}

export default memo(SettingsIconWidget);
