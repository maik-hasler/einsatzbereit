import { useTranslation } from "react-i18next";
import type { NotificationSummary } from "../../client/api-client";
import { formatDateTime } from "../../lib/format";
import { EyeSlashIcon, TrashIcon } from "../icons";

export default function NotificationItem({
	notification,
	onSelect,
	onMarkUnread,
	onDelete,
}: {
	notification: NotificationSummary;
	onSelect: (notification: NotificationSummary) => Promise<void>;
	onMarkUnread: (id: string) => Promise<void>;
	onDelete: (id: string) => Promise<void>;
}) {
	const { t, i18n } = useTranslation();
	const n = notification;
	const text = t(`notifications.kinds.${n.kind}` as Parameters<typeof t>[0], {
		title: n.relatedTitle ?? t("notifications.deletedOpportunityPlaceholder"),
		defaultValue: n.kind,
	});

	return (
		<li className="relative">
			<button
				type="button"
				className={`w-full cursor-pointer px-4 py-3 pr-16 text-left text-sm transition-colors hover:bg-brand-50 ${!n.isRead ? "font-medium text-gray-900" : "text-gray-500"}`}
				onClick={() => void onSelect(n)}
			>
				<span className="flex items-start gap-2">
					{!n.isRead && (
						<>
							{/* Unread was signalled by this dot's colour and the row's font
							weight alone (#1786) - the span is empty, so a screen reader
							read an unread row exactly like a read one. The marker goes
							inside the row's own button to join its accessible name,
							rather than being a standalone live region like the bell's
							aggregate count in NotificationDropdown. */}
							<span
								aria-hidden="true"
								className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-brand-500"
							/>
							<span className="sr-only">{t("notifications.unread")}</span>
						</>
					)}
					<span className={!n.isRead ? "" : "pl-4"}>
						{text}
						<br />
						<span className="text-xs text-gray-500">
							{formatDateTime(n.createdOn as unknown as string, i18n.language)}
						</span>
					</span>
				</span>
			</button>
			<span className="absolute top-2 right-2 z-10 flex gap-1">
				{n.isRead && (
					<button
						type="button"
						aria-label={t("notifications.markUnreadNamed", {
							notification: text,
						})}
						onClick={() => void onMarkUnread(n.id)}
						className="cursor-pointer rounded p-1.5 text-gray-500 hover:bg-brand-50 hover:text-brand-600"
					>
						<EyeSlashIcon className="h-3.5 w-3.5" />
					</button>
				)}
				<button
					type="button"
					aria-label={t("notifications.deleteNamed", { notification: text })}
					onClick={() => void onDelete(n.id)}
					className="cursor-pointer rounded p-1.5 text-gray-500 hover:bg-red-50 hover:text-red-600"
				>
					<TrashIcon className="h-3.5 w-3.5" />
				</button>
			</span>
		</li>
	);
}
