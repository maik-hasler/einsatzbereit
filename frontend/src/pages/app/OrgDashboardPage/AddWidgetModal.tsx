import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import Modal from "../../../components/Modal";
import Button from "../../../components/Button";
import EmptyState from "../../../components/EmptyState";
import { WIDGET_CATALOG, type WidgetKey } from "./widgetCatalog";

const WIDGET_DESC_KEY: Record<WidgetKey, string> = {
	ToDo: "orgDashboard.todoWidgetDesc",
	VolunteerStats: "orgDashboard.volunteerStatsWidgetDesc",
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
		// One stat plus the link out to the pending queue - the second stat
		// block moved to VolunteerStats below (#1780).
		case "ToDo":
			return (
				<div className="space-y-1.5" aria-hidden="true">
					<div className="h-4 w-8 rounded bg-brand-100" />
					<Bar className="w-3/4" />
					<div className="h-2 w-1/2 rounded-full bg-brand-200" />
				</div>
			);
		case "VolunteerStats":
			return (
				<div className="space-y-1.5" aria-hidden="true">
					<div className="h-4 w-8 rounded bg-brand-100" />
					<Bar className="w-3/4" />
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
	const [announcement, setAnnouncement] = useState("");
	const pendingFocusIndexRef = useRef<number | null>(null);

	// Adding a widget unmounts the very button that was just pressed (it drops
	// out of `availableKeys`), so focus needs somewhere deliberate to land
	// instead of falling to <body> while the dialog is still open - mirrors
	// EngagementManagementPage's focusEngagementRowControl for the same
	// "pressed control disappears" shape. Runs one frame after the prop
	// update so the replacement button (or the Done button, once the list
	// empties) already exists in the DOM.
	useEffect(() => {
		if (pendingFocusIndexRef.current === null) return;
		const index = pendingFocusIndexRef.current;
		pendingFocusIndexRef.current = null;
		requestAnimationFrame(() => {
			if (availableKeys.length === 0) {
				document
					.querySelector<HTMLElement>('[data-testid="add-widget-done"]')
					?.focus();
				return;
			}
			const nextKey = availableKeys[Math.min(index, availableKeys.length - 1)];
			document
				.querySelector<HTMLElement>(
					`[data-testid="add-widget-option-${nextKey}"]`,
				)
				?.focus();
		});
	}, [availableKeys]);

	function handleAdd(key: WidgetKey, index: number) {
		pendingFocusIndexRef.current = index;
		setAnnouncement(
			t("orgDashboard.addWidgetAnnounce", {
				title: t(WIDGET_CATALOG[key].titleKey),
			}),
		);
		onAdd(key);
	}

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

			{/* Always mounted (not conditional on `announcement`) so the live
			region is registered before it ever gets content - see
			CheckInModal.tsx's identical pattern for why. */}
			<p role="status" className="sr-only">
				{announcement}
			</p>

			<div className="min-h-0 flex-1 overflow-y-auto px-6 py-4">
				{availableKeys.length === 0 ? (
					<EmptyState compact title={t("orgDashboard.addWidgetAllAdded")} />
				) : (
					<div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
						{availableKeys.map((key, index) => {
							const entry = WIDGET_CATALOG[key];
							const title = t(entry.titleKey);
							return (
								<button
									key={key}
									type="button"
									data-testid={`add-widget-option-${key}`}
									onClick={() => handleAdd(key, index)}
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
