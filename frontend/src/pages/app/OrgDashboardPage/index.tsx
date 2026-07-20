import { useEffect, useState, type ReactNode } from "react";
import { useNavigate, useOutletContext } from "react-router";
import { useTranslation } from "react-i18next";
import {
	DndContext,
	closestCenter,
	KeyboardSensor,
	PointerSensor,
	useSensor,
	useSensors,
	type DragEndEvent,
} from "@dnd-kit/core";
import {
	SortableContext,
	arrayMove,
	rectSortingStrategy,
	sortableKeyboardCoordinates,
	useSortable,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import type { OrgAppContext } from "../../../layouts/OrgAppLayout";
import { useApiClient } from "../../../hooks/useApiClient";
import { useEditModeQuickActions } from "../../../hooks/useEditModeQuickActions";
import { dispatchToast } from "../../../lib/toastBus";
import { getApiErrorMessage } from "../../../lib/apiError";
import { PlusIcon, CancelIcon } from "../../../components/QuickActionIcons";
import Spinner from "../../../components/Spinner";
import CalendarWidget from "./CalendarWidget";
import UpcomingOpportunitiesWidget from "./UpcomingOpportunitiesWidget";
import ToDoWidget from "./ToDoWidget";
import SettingsWidget from "./SettingsWidget";
import CreateOpportunityWidget from "./CreateOpportunityWidget";
import QuickCheckInWidget from "./QuickCheckInWidget";
import SettingsIconWidget from "./SettingsIconWidget";
import {
	DEFAULT_LAYOUT,
	WIDGET_CATALOG,
	WIDGET_KEYS,
	sanitizePlacement,
	widgetColSpanClass,
	type WidgetKey,
	type WidgetPlacement,
} from "./widgetCatalog";

function GripIcon() {
	return (
		<svg
			className="h-4 w-4"
			viewBox="0 0 16 16"
			fill="currentColor"
			aria-hidden="true"
		>
			<circle cx="5" cy="3" r="1.25" />
			<circle cx="11" cy="3" r="1.25" />
			<circle cx="5" cy="8" r="1.25" />
			<circle cx="11" cy="8" r="1.25" />
			<circle cx="5" cy="13" r="1.25" />
			<circle cx="11" cy="13" r="1.25" />
		</svg>
	);
}

function EditableWidgetTile({
	placement,
	editing,
	onRemove,
	onCycleSize,
	children,
}: {
	placement: WidgetPlacement;
	editing: boolean;
	onRemove: () => void;
	onCycleSize: () => void;
	children: ReactNode;
}) {
	const { t } = useTranslation();
	const {
		attributes,
		listeners,
		setNodeRef,
		transform,
		transition,
		isDragging,
	} = useSortable({ id: placement.widgetKey });
	const catalogEntry = WIDGET_CATALOG[placement.widgetKey];

	// Keep the same tree shape (wrapper > inner div > children, toolbar as an
	// optional sibling) whether editing is on or off - branching the returned
	// JSX shape itself (e.g. an extra wrapper only in one branch) would shift
	// `children` to a different position in the tree on every edit-mode
	// toggle, and React would remount every widget (losing its fetched data
	// and any open state) instead of just updating this wrapper in place.
	const style = editing
		? { transform: CSS.Transform.toString(transform), transition }
		: undefined;

	return (
		<div
			ref={setNodeRef}
			style={style}
			data-testid={`widget-tile-${placement.widgetKey}`}
			className={`relative ${widgetColSpanClass(placement.size)} ${editing && isDragging ? "z-10 opacity-50" : ""}`}
		>
			<div inert={editing} className={editing ? "opacity-75" : undefined}>
				{children}
			</div>
			{editing && (
				<div className="absolute right-2 top-2 z-20 flex items-center gap-1 rounded-lg bg-white/95 p-1 shadow-sm ring-1 ring-gray-200">
					<button
						type="button"
						{...attributes}
						{...listeners}
						className="cursor-grab rounded p-1.5 text-gray-500 hover:bg-gray-100 active:cursor-grabbing"
						aria-label={t("orgDashboard.dragToReorder", {
							widget: t(catalogEntry.titleKey),
						})}
					>
						<GripIcon />
					</button>
					{catalogEntry.allowedSizes.length > 1 && (
						<button
							type="button"
							onClick={onCycleSize}
							className="rounded px-1.5 py-1 text-xs font-medium text-gray-600 hover:bg-gray-100"
							aria-label={t("orgDashboard.changeWidgetSize", {
								widget: t(catalogEntry.titleKey),
								size: t(`orgDashboard.widgetSize${placement.size}`),
							})}
						>
							{t(`orgDashboard.widgetSize${placement.size}`)}
						</button>
					)}
					<button
						type="button"
						onClick={onRemove}
						className="rounded p-1.5 text-gray-500 hover:bg-red-50 hover:text-red-600"
						aria-label={t("orgDashboard.removeWidget", {
							widget: t(catalogEntry.titleKey),
						})}
					>
						<CancelIcon />
					</button>
				</div>
			)}
		</div>
	);
}

export default function OrgDashboardPage() {
	const { org } = useOutletContext<OrgAppContext>();
	const { t } = useTranslation();
	const navigate = useNavigate();
	const api = useApiClient();
	const organizationId = org.id;

	// Bumped after a published opportunity is created so the Calendar and
	// Upcoming Opportunities widgets (which each own their own data) refetch.
	const [refreshKey, setRefreshKey] = useState(0);

	const [savedLayout, setSavedLayout] = useState<WidgetPlacement[] | null>(
		null,
	);
	const [layoutLoading, setLayoutLoading] = useState(true);
	const [editing, setEditing] = useState(false);
	const [draftLayout, setDraftLayout] = useState<WidgetPlacement[] | null>(
		null,
	);
	const [saving, setSaving] = useState(false);

	useEffect(() => {
		setLayoutLoading(true);
		api
			.getDashboardLayout(organizationId)
			.then((response) => {
				const sanitized = response.widgets
					.map(sanitizePlacement)
					.filter((w): w is WidgetPlacement => w !== null);
				setSavedLayout(sanitized.length === 0 ? DEFAULT_LAYOUT : sanitized);
			})
			.catch(() => setSavedLayout(DEFAULT_LAYOUT))
			.finally(() => setLayoutLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	const layout = editing ? (draftLayout ?? []) : (savedLayout ?? []);

	function handleOpportunityCreated(createdDraftId?: string) {
		// Drafts live on the Opportunities tab now. When one is saved from here,
		// take the organizer there with the new draft highlighted so it is never
		// lost (issue #708). A published opportunity just refreshes the widgets.
		if (createdDraftId) {
			navigate(
				`/app/${organizationId}/opportunities?highlight=${createdDraftId}`,
			);
			return;
		}
		setRefreshKey((k) => k + 1);
	}

	async function handleSave() {
		if (!draftLayout) return;
		setSaving(true);
		try {
			await api.saveDashboardLayout(organizationId, {
				widgets: draftLayout.map((w) => ({
					widgetKey: w.widgetKey,
					size: w.size,
				})),
			});
			setSavedLayout(draftLayout);
			setEditing(false);
			setDraftLayout(null);
		} catch (e) {
			dispatchToast("error", getApiErrorMessage(e, t("error.serverError")));
		} finally {
			setSaving(false);
		}
	}

	function handleCancel() {
		setEditing(false);
		setDraftLayout(null);
	}

	useEditModeQuickActions({
		editing,
		saving,
		onEdit: () => {
			setDraftLayout(savedLayout ?? DEFAULT_LAYOUT);
			setEditing(true);
		},
		onSave: () => void handleSave(),
		onCancel: handleCancel,
	});

	function handleRemoveWidget(key: WidgetKey) {
		setDraftLayout((prev) => (prev ?? []).filter((w) => w.widgetKey !== key));
	}

	function handleAddWidget(key: WidgetKey) {
		setDraftLayout((prev) => [
			...(prev ?? []),
			{ widgetKey: key, size: WIDGET_CATALOG[key].defaultSize },
		]);
	}

	function handleCycleSize(key: WidgetKey) {
		setDraftLayout((prev) =>
			(prev ?? []).map((w) => {
				if (w.widgetKey !== key) return w;
				const sizes = WIDGET_CATALOG[key].allowedSizes;
				const next = sizes[(sizes.indexOf(w.size) + 1) % sizes.length];
				return { ...w, size: next };
			}),
		);
	}

	const sensors = useSensors(
		useSensor(PointerSensor),
		useSensor(KeyboardSensor, {
			coordinateGetter: sortableKeyboardCoordinates,
		}),
	);

	function handleDragEnd(event: DragEndEvent) {
		const { active, over } = event;
		if (!over || active.id === over.id) return;
		setDraftLayout((prev) => {
			const current = prev ?? [];
			const oldIndex = current.findIndex((w) => w.widgetKey === active.id);
			const newIndex = current.findIndex((w) => w.widgetKey === over.id);
			if (oldIndex === -1 || newIndex === -1) return current;
			return arrayMove(current, oldIndex, newIndex);
		});
	}

	function renderWidget(key: WidgetKey) {
		switch (key) {
			case "ToDo":
				return <ToDoWidget organizationId={organizationId} />;
			case "UpcomingOpportunities":
				return (
					<UpcomingOpportunitiesWidget
						organizationId={organizationId}
						refreshKey={refreshKey}
					/>
				);
			case "Calendar":
				return (
					<CalendarWidget
						organizationId={organizationId}
						refreshKey={refreshKey}
					/>
				);
			case "Settings":
				return <SettingsWidget org={org} />;
			case "CreateOpportunity":
				return (
					<CreateOpportunityWidget
						organizationId={organizationId}
						onCreated={handleOpportunityCreated}
					/>
				);
			case "QuickCheckIn":
				return <QuickCheckInWidget organizationId={organizationId} />;
			case "SettingsIcon":
				return <SettingsIconWidget organizationId={organizationId} />;
		}
	}

	const availableToAdd = WIDGET_KEYS.filter(
		(key) => !layout.some((w) => w.widgetKey === key),
	);

	if (layoutLoading) {
		return (
			<div className="flex items-center justify-center py-16">
				<Spinner label={t("orgDashboard.loading")} />
			</div>
		);
	}

	const grid = (
		<div className="grid grid-cols-1 gap-6 lg:grid-cols-4">
			{layout.map((placement) => (
				<EditableWidgetTile
					key={placement.widgetKey}
					placement={placement}
					editing={editing}
					onRemove={() => handleRemoveWidget(placement.widgetKey)}
					onCycleSize={() => handleCycleSize(placement.widgetKey)}
				>
					{renderWidget(placement.widgetKey)}
				</EditableWidgetTile>
			))}
			{editing && availableToAdd.length > 0 && (
				<div className="rounded-2xl border-2 border-dashed border-gray-300 p-5 lg:col-span-4">
					<p className="mb-3 text-sm font-medium text-gray-700">
						{t("orgDashboard.addWidgetHeading")}
					</p>
					<div className="flex flex-wrap gap-2">
						{availableToAdd.map((key) => (
							<button
								key={key}
								type="button"
								onClick={() => handleAddWidget(key)}
								className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50"
							>
								<PlusIcon />
								{t(WIDGET_CATALOG[key].titleKey)}
							</button>
						))}
					</div>
				</div>
			)}
		</div>
	);

	// Always render inside DndContext/SortableContext, even when not editing -
	// EditableWidgetTile's read-only path never attaches drag listeners, so
	// this is inert overhead outside edit mode, not a behavior change. Kept
	// unconditional so useSortable (called unconditionally by every tile,
	// per rules-of-hooks) always has the ancestor context it expects.
	return (
		<DndContext
			sensors={sensors}
			collisionDetection={closestCenter}
			onDragEnd={handleDragEnd}
		>
			<SortableContext
				items={layout.map((w) => w.widgetKey)}
				strategy={rectSortingStrategy}
			>
				{grid}
			</SortableContext>
		</DndContext>
	);
}
