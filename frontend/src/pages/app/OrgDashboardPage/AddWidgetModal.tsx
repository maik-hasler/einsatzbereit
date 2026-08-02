import { useTranslation } from "react-i18next";
import Modal from "../../../components/Modal";
import Button from "../../../components/Button";
import { WIDGET_CATALOG, type WidgetKey } from "./widgetCatalog";

const WIDGET_DESC_KEY: Record<WidgetKey, string> = {
	ToDo: "orgDashboard.todoWidgetDesc",
	UpcomingOpportunities: "orgDashboard.upcomingWidgetDesc",
	Calendar: "orgDashboard.calendarWidgetDesc",
	Settings: "orgDashboard.settingsWidgetDesc",
	CreateOpportunity: "orgDashboard.createOpportunityWidgetDesc",
	QuickCheckIn: "orgDashboard.quickCheckInWidgetDesc",
	SettingsIcon: "orgDashboard.settingsIconWidgetDesc",
};

function Bar({ className = "" }: { className?: string }) {
	return <div className={`h-2 rounded-full bg-gray-200 ${className}`} />;
}

// Small, static mockups hinting at each widget's shape - deliberately not a
// live render of the organizer's real data (that would mean extra API calls
// just to browse the picker, and isn't really "an example" so much as a
// leak of whatever happens to be in this org right now). Also reused by
// OrgDashboardPage's drag overlay (see EditableWidgetTile) for the same
// reason: the floating clone shown while dragging must not mount a second
// live instance of the widget (double data fetch, duplicate side effects).
function WidgetPreview({ widgetKey }: { widgetKey: WidgetKey }) {
	switch (widgetKey) {
		case "ToDo":
			return (
				<div className="grid grid-cols-2 gap-3" aria-hidden="true">
					<div className="space-y-1.5">
						<div className="h-4 w-8 rounded bg-brand-100" />
						<Bar className="w-3/4" />
					</div>
					<div className="space-y-1.5">
						<div className="h-4 w-8 rounded bg-brand-100" />
						<Bar className="w-3/4" />
					</div>
				</div>
			);
		case "UpcomingOpportunities":
			return (
				<div className="space-y-1.5" aria-hidden="true">
					<Bar className="w-full" />
					<Bar className="w-full" />
					<Bar className="w-2/3" />
				</div>
			);
		case "Calendar":
			return (
				<div className="grid grid-cols-7 gap-1" aria-hidden="true">
					{Array.from({ length: 14 }).map((_, i) => (
						<div
							key={i}
							className={`h-2.5 rounded-sm ${i === 5 || i === 9 ? "bg-brand-300" : "bg-gray-200"}`}
						/>
					))}
				</div>
			);
		case "Settings":
			return (
				<div className="flex items-center gap-2" aria-hidden="true">
					<div className="h-8 w-8 shrink-0 rounded-lg bg-brand-100" />
					<div className="w-full space-y-1.5">
						<Bar className="w-3/4" />
						<Bar className="w-1/2" />
					</div>
				</div>
			);
		case "CreateOpportunity":
			return (
				<div
					className="h-7 w-full rounded-lg bg-brand-200"
					aria-hidden="true"
				/>
			);
		case "QuickCheckIn":
			return (
				<div className="space-y-1.5" aria-hidden="true">
					<div className="h-6 w-full rounded-md border border-gray-200" />
					<div className="h-6 w-full rounded-lg bg-brand-200" />
				</div>
			);
		case "SettingsIcon":
			return (
				<div className="flex justify-center" aria-hidden="true">
					<div className="h-8 w-8 rounded-lg bg-brand-100" />
				</div>
			);
	}
}

interface Props {
	availableKeys: WidgetKey[];
	onAdd: (key: WidgetKey) => void;
	onClose: () => void;
}

// Opened from the "Add Widget" quick action (edit mode only, see
// widgetCatalog.ts/useEditModeQuickActions) - lets an organizer browse the
// widgets not currently on their dashboard, each with a small preview, and
// add one or more without leaving the picker (see #771 review feedback).
export default function AddWidgetModal({
	availableKeys,
	onAdd,
	onClose,
}: Props) {
	const { t } = useTranslation();

	return (
		<Modal
			onClose={onClose}
			labelledBy="add-widget-dialog-title"
			maxWidth="max-w-2xl"
			className="flex max-h-[min(85vh,640px)] flex-col overflow-hidden rounded-card bg-white shadow-modal"
		>
			<div className="border-b border-gray-100 px-6 py-4">
				<h2
					id="add-widget-dialog-title"
					className="text-lg font-semibold text-gray-900"
				>
					{t("orgDashboard.addWidgetHeading")}
				</h2>
				<p className="mt-1 text-sm text-gray-500">
					{t("orgDashboard.addWidgetModalDesc")}
				</p>
			</div>

			<div className="min-h-0 flex-1 overflow-y-auto px-6 py-4">
				{availableKeys.length === 0 ? (
					<p className="py-8 text-center text-sm text-gray-500">
						{t("orgDashboard.addWidgetAllAdded")}
					</p>
				) : (
					<div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
						{availableKeys.map((key) => {
							const entry = WIDGET_CATALOG[key];
							const title = t(entry.titleKey);
							return (
								<button
									key={key}
									type="button"
									data-testid={`add-widget-option-${key}`}
									onClick={() => onAdd(key)}
									className="flex flex-col gap-3 rounded-xl border border-gray-200 p-4 text-left transition-colors hover:border-brand-300 hover:bg-brand-50"
								>
									<WidgetPreview widgetKey={key} />
									<div>
										<p className="text-sm font-semibold text-gray-900">
											{title}
										</p>
										<p className="mt-0.5 text-xs text-gray-500">
											{t(WIDGET_DESC_KEY[key])}
										</p>
									</div>
								</button>
							);
						})}
					</div>
				)}
			</div>

			<div className="flex justify-end border-t border-gray-100 px-6 py-4">
				<Button type="button" onClick={onClose} data-testid="add-widget-done">
					{t("orgDashboard.addWidgetDone")}
				</Button>
			</div>
		</Modal>
	);
}
