import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useNavigate, useOutletContext } from "react-router";
import { useTranslation } from "react-i18next";
import {
	DndContext,
	DragOverlay,
	closestCenter,
	KeyboardSensor,
	MouseSensor,
	TouchSensor,
	useSensor,
	useSensors,
	type DragEndEvent,
	type DragStartEvent,
} from "@dnd-kit/core";
import {
	SortableContext,
	arrayMove,
	rectSortingStrategy,
	sortableKeyboardCoordinates,
	useSortable,
} from "@dnd-kit/sortable";
import type { OrgAppContext } from "../../../layouts/OrgAppLayout";
import { useApiClient } from "../../../hooks/useApiClient";
import { useEditModeQuickActions } from "../../../hooks/useEditModeQuickActions";
import { dispatchToast } from "../../../lib/toastBus";
import { getApiErrorMessage } from "../../../lib/apiError";
import { PlusIcon, CancelIcon } from "../../../components/QuickActionIcons";
import EmptyState from "../../../components/EmptyState";
import AddWidgetModal, { WidgetPreview } from "./AddWidgetModal";
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
	const { attributes, listeners, setNodeRef, isDragging } = useSortable({
		id: placement.widgetKey,
	});
	const catalogEntry = WIDGET_CATALOG[placement.widgetKey];

	// No transform/transition from useSortable is applied here on purpose -
	// dnd-kit's built-in sortable animation projects a translate offset
	// assuming every item is the same size, which falls apart on this grid
	// (mixed col-spans + grid-flow-dense) and produced the "other widgets...
	// look so weirdly" glitch from #771 follow-up review feedback. The
	// dragged tile is hidden in place (opacity-0 below) while
	// OrgDashboardPage's <DragOverlay> shows a floating clone that actually
	// follows the pointer/keyboard; every other tile just stays put until the
	// drop reorders the array, then snaps straight to its new dense-packed
	// slot - no mid-drag animation left to get wrong.
	return (
		// The grip button below keeps its own {...attributes} {...listeners}
		// as the accessible, keyboard-operable drag handle (dnd-kit's
		// KeyboardSensor needs a focusable element with those attributes, and
		// a whole free-shaped card exposed as one giant nested-interactive
		// control would trip the "no interactive control inside another" a11y
		// rule against the toolbar's own buttons). onMouseDown/onTouchStart
		// below additionally let mouse/touch users grab the card ANYWHERE, not
		// just the grip icon - purely as a supplementary trigger for the same
		// drag dnd-kit's KeyboardSensor already makes fully keyboard-operable
		// via that button, which is why suppressing the static-element-
		// interactions rule here is safe: this div never needs its own
		// role/keyboard handling, the real interactive element is the grip
		// button beside it. The sensors' activationConstraint (see
		// OrgDashboardPage) keeps this from swallowing plain clicks on the
		// resize/remove buttons, which live inside the same element. Touch
		// specifically uses TouchSensor's delay (not MouseSensor's distance),
		// which - unlike distance-based activation - doesn't require disabling
		// touch-action, so a quick swipe still scrolls the page normally and
		// only a deliberate hold claims the gesture for dragging (#771
		// follow-up review feedback - "moving doesnt work at all on mobile" -
		// without also breaking scroll while editing).
		// eslint-disable-next-line jsx-a11y/no-static-element-interactions
		<div
			ref={setNodeRef}
			data-testid={`widget-tile-${placement.widgetKey}`}
			onMouseDown={(event) => {
				if (editing) listeners?.onMouseDown?.(event);
			}}
			onTouchStart={(event) => {
				if (editing) listeners?.onTouchStart?.(event);
			}}
			// A low but nonzero opacity (not opacity-0) while dragging - a
			// keyboard-initiated drag (KeyboardSensor) leaves real DOM focus on
			// the grip button inside this tile, and a fully invisible ancestor
			// would take its focus ring with it (WCAG 2.4.7 Focus Visible),
			// since <DragOverlay>'s floating clone has no focus of its own to
			// show one instead.
			className={`relative h-full ${widgetColSpanClass(placement.size)} ${editing && isDragging ? "opacity-20" : ""}`}
		>
			<div inert={editing} className={`h-full ${editing ? "opacity-75" : ""}`}>
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

// Static, non-interactive preview shown in <DragOverlay> while a widget is
// being dragged - reuses AddWidgetModal's per-widget mockup rather than
// rendering the real widget a second time, which would mount a second live
// instance (double data fetch, duplicate side effects) for however long the
// drag lasts.
function WidgetDragPreview({
	widgetKey,
	size,
}: {
	widgetKey: WidgetKey;
	size: { width: number; height: number } | null;
}) {
	const { t } = useTranslation();
	const catalogEntry = WIDGET_CATALOG[widgetKey];
	return (
		<div
			style={size ? { width: size.width, height: size.height } : undefined}
			className="cursor-grabbing rounded-2xl border border-gray-100 bg-white p-5 shadow-lg ring-2 ring-brand-300"
		>
			<div className="mb-4 flex items-center justify-between gap-3">
				<h2 className="text-base font-semibold text-gray-900">
					{t(catalogEntry.titleKey)}
				</h2>
			</div>
			<WidgetPreview widgetKey={widgetKey} />
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

	// Renders the default layout immediately rather than gating the whole
	// grid behind the GET .../dashboard/layout round trip - most visits get
	// exactly this layout back anyway (no customization saved yet), and a
	// returning organizer's customized layout swaps in a moment later instead
	// of blocking first paint on it.
	const [savedLayout, setSavedLayout] =
		useState<WidgetPlacement[]>(DEFAULT_LAYOUT);
	const [editing, setEditing] = useState(false);
	const [draftLayout, setDraftLayout] = useState<WidgetPlacement[] | null>(
		null,
	);
	const [saving, setSaving] = useState(false);
	const [showAddWidgetModal, setShowAddWidgetModal] = useState(false);
	// Tracks which widget (if any) is mid-drag so <DragOverlay> can render a
	// floating clone of it - see EditableWidgetTile's comment on why the
	// dragged tile itself no longer carries a dnd-kit transform.
	const [activeDragKey, setActiveDragKey] = useState<WidgetKey | null>(null);
	const [activeDragSize, setActiveDragSize] = useState<{
		width: number;
		height: number;
	} | null>(null);

	useEffect(() => {
		api
			.getDashboardLayout(organizationId)
			.then((response) => {
				const sanitized = response.widgets
					.map(sanitizePlacement)
					.filter((w): w is WidgetPlacement => w !== null);
				// Only a brand-new organizer (no saved layout row at all,
				// hasCustomLayout=false) gets the default layout applied. An
				// organizer who deliberately removed every widget and saved that
				// has hasCustomLayout=true with an empty list - respecting that
				// as a real empty layout, instead of silently reapplying the
				// default, is the #771 follow-up fix for "remove all widgets,
				// it resets back to the default set on refresh".
				setSavedLayout(response.hasCustomLayout ? sanitized : DEFAULT_LAYOUT);
			})
			.catch(() => setSavedLayout(DEFAULT_LAYOUT));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	const layout = editing ? (draftLayout ?? []) : savedLayout;
	const availableToAdd = WIDGET_KEYS.filter(
		(key) => !layout.some((w) => w.widgetKey === key),
	);

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

	function startEditing() {
		setDraftLayout(savedLayout);
		setEditing(true);
	}

	// Used by EmptyState's CTA when the dashboard has zero widgets and isn't
	// already in edit mode - jumps straight into editing and opens the picker,
	// rather than making the organizer find the "Edit" quick action first.
	function handleStartEditingAndAddWidget() {
		if (!editing) startEditing();
		setShowAddWidgetModal(true);
	}

	// Memoized on just the primitive/visual deps (see useQuickActions) - the
	// "Add Widget" action only needs to change when there's actually nothing
	// left to add, not on every render.
	const hasWidgetsToAdd = availableToAdd.length > 0;
	const extraEditingActions = useMemo(
		() =>
			hasWidgetsToAdd
				? [
						{
							key: "add-widget",
							label: t("orgDashboard.addWidgetHeading"),
							icon: <PlusIcon />,
							onClick: () => setShowAddWidgetModal(true),
						},
					]
				: [],
		[hasWidgetsToAdd, t],
	);

	useEditModeQuickActions({
		editing,
		saving,
		onEdit: startEditing,
		onSave: () => void handleSave(),
		onCancel: handleCancel,
		extraEditingActions,
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
		// A movement threshold before a drag activates, now that the whole
		// tile (not just the grip button) carries a mousedown listener -
		// without it, a plain click on the resize/remove buttons (which live
		// inside that same tile) would be swallowed as a zero-distance drag
		// instead of firing their own onClick.
		useSensor(MouseSensor, { activationConstraint: { distance: 8 } }),
		// Touch gets a short hold instead of a distance threshold. Unlike
		// MouseSensor/PointerSensor's distance-based activation, dnd-kit's
		// delay-based TouchSensor doesn't need `touch-action: none` to work
		// (see EditableWidgetTile's comment) - a quick swipe still scrolls the
		// page as normal, and only a deliberate ~200ms hold on a tile claims
		// the gesture for dragging.
		useSensor(TouchSensor, {
			activationConstraint: { delay: 200, tolerance: 8 },
		}),
		useSensor(KeyboardSensor, {
			coordinateGetter: sortableKeyboardCoordinates,
		}),
	);

	function handleDragStart(event: DragStartEvent) {
		setActiveDragKey(event.active.id as WidgetKey);
		const rect = event.active.rect.current.initial;
		setActiveDragSize(rect ? { width: rect.width, height: rect.height } : null);
	}

	function handleDragEnd(event: DragEndEvent) {
		setActiveDragKey(null);
		setActiveDragSize(null);
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

	// dnd-kit fires this on Escape (and a few other abandon paths) instead of
	// onDragEnd - without it, an escaped drag would leave the source tile
	// permanently hidden (opacity-0, see EditableWidgetTile) with no overlay
	// to show for it.
	function handleDragCancel() {
		setActiveDragKey(null);
		setActiveDragSize(null);
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

	// An empty layout is ambiguous only in the API response (see the
	// useEffect above) - once resolved into `layout`, zero widgets always
	// means "genuinely nothing here right now" (either a deliberately
	// emptied saved layout, or a draft mid-edit), so it always gets the same
	// empty state rather than silently falling back to the default set.
	const isEmpty = layout.length === 0;

	// grid-flow-row-dense backfills gaps left by mixed widget sizes (e.g. a
	// Small next to two Mediums doesn't evenly divide the 4 columns) with a
	// later widget that DOES fit, instead of leaving ragged empty cells -
	// #771 review feedback ("sizes... dont fully align with the layout").
	const grid = isEmpty ? (
		<div data-testid="dashboard-empty-state">
			<EmptyState
				title={t("orgDashboard.emptyStateTitle")}
				message={t("orgDashboard.emptyStateMessage")}
				action={{
					label: t("orgDashboard.addWidgetHeading"),
					onClick: handleStartEditingAndAddWidget,
				}}
			/>
		</div>
	) : (
		<div
			data-testid="dashboard-widget-grid"
			className="grid grid-cols-1 gap-6 lg:grid-cols-4 lg:grid-flow-row-dense"
		>
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
		</div>
	);

	return (
		<>
			{/* Always render inside DndContext/SortableContext, even when not
			editing - EditableWidgetTile's read-only path never attaches drag
			listeners, so this is inert overhead outside edit mode, not a
			behavior change. Kept unconditional so useSortable (called
			unconditionally by every tile, per rules-of-hooks) always has the
			ancestor context it expects. */}
			<DndContext
				sensors={sensors}
				collisionDetection={closestCenter}
				onDragStart={handleDragStart}
				onDragEnd={handleDragEnd}
				onDragCancel={handleDragCancel}
			>
				<SortableContext
					items={layout.map((w) => w.widgetKey)}
					strategy={rectSortingStrategy}
				>
					{grid}
				</SortableContext>
				<DragOverlay>
					{activeDragKey && (
						<WidgetDragPreview
							widgetKey={activeDragKey}
							size={activeDragSize}
						/>
					)}
				</DragOverlay>
			</DndContext>

			{showAddWidgetModal && (
				<AddWidgetModal
					availableKeys={availableToAdd}
					onAdd={handleAddWidget}
					onClose={() => setShowAddWidgetModal(false)}
				/>
			)}
		</>
	);
}
