import { Trans, useTranslation } from "react-i18next";
import { Link } from "react-router";
import type { NotificationSummary } from "../../client/api-client";
import { formatDateTime, pickLocalizedText } from "../../lib/format";
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
	const localizedTitle = pickLocalizedText(
		n.relatedTitle,
		n.relatedTitleEn,
		i18n.language,
	);
	const titleText =
		localizedTitle?.text ?? t("notifications.deletedOpportunityPlaceholder");
	// kinds.* strings wrap {{title}} in <titleSpan> for the Trans render below;
	// t() doesn't parse that markup, so strip it for the plain-text aria-labels.
	const text = t(`notifications.kinds.${n.kind}` as Parameters<typeof t>[0], {
		title: titleText,
		defaultValue: n.kind,
	}).replace(/<\/?titleSpan>/g, "");
	const href = n.actionUrl ?? "/my-signups";

	return (
		<li className="relative">
			<Link
				to={href}
				className={`block w-full px-4 py-3 pr-16 text-sm transition-colors hover:bg-brand-50 ${!n.isRead ? "font-medium text-gray-900" : "text-gray-500"}`}
				onClick={() => void onSelect(n)}
			>
				<span className="flex items-start gap-2">
					{!n.isRead && (
						<>
							<span
								aria-hidden="true"
								className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-brand-500"
							/>
							<span className="sr-only">{t("notifications.unread")}</span>
						</>
					)}
					<span className={!n.isRead ? "" : "pl-4"}>
						<Trans
							i18nKey={`notifications.kinds.${n.kind}`}
							values={{ title: titleText }}
							components={{ titleSpan: <span lang={localizedTitle?.lang} /> }}
							defaults={n.kind}
						/>
						<br />
						<span className="text-xs text-gray-500">
							{formatDateTime(n.createdOn as unknown as string, i18n.language)}
						</span>
					</span>
				</span>
			</Link>
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
