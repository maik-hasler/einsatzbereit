import { useTranslation } from "react-i18next";
import type { NotificationSummary } from "../../client/api-client";

export default function NotificationItem({
	notification,
	onSelect,
}: {
	notification: NotificationSummary;
	onSelect: (notification: NotificationSummary) => Promise<void>;
}) {
	const { t } = useTranslation();
	const n = notification;

	return (
		<li>
			<button
				type="button"
				className={`w-full text-left px-4 py-3 text-sm transition-colors cursor-pointer hover:bg-brand-50 ${!n.isRead ? "font-medium text-gray-900" : "text-gray-500"}`}
				onClick={() => void onSelect(n)}
			>
				<span className="flex items-start gap-2">
					{!n.isRead && (
						<span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-brand-500" />
					)}
					<span className={!n.isRead ? "" : "pl-4"}>
						{t(`notifications.kinds.${n.kind}` as Parameters<typeof t>[0], {
							title:
								n.relatedTitle ??
								t("notifications.deletedOpportunityPlaceholder"),
							defaultValue: n.kind,
						})}
						<br />
						<span className="text-xs text-gray-400">
							{new Date(n.createdOn).toLocaleString()}
						</span>
					</span>
				</span>
			</button>
		</li>
	);
}
