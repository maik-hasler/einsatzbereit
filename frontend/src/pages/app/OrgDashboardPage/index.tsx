import {
	useEffect,
	useMemo,
	useState,
	type CSSProperties,
	type ReactNode,
} from "react";
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
	type DragOverEvent,
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
import { PlusIcon, TrashIcon } from "../../../components/QuickActionIcons";
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
	classifyWidth,
	packWidgets,
	sanitizeWidgetKey,
	type WidgetKey,
	type WidgetSizeClass,
} from "./widgetCatalog";

// Matches Tailwind's default `lg` breakpoint, which is also where the
// widget grid switches from a single stacked column to the real 8-column
// grid (see the grid container's className below) - auto-fit column/row
// placement and the green cell backdrop only make sense once that grid
// exists, so both are gated on this.
function useIsLargeViewport() {
	const [isLarge, setIsLarge] = useState(
		() => window.matchMedia("(min-width: 1024px)").matches,
	);
	useEffect(() => {
		const mql = window.matchMedia("(min-width: 1024px)");
		const handler = () => setIsLarge(mql.matches);
		mql.addEventListener("change", handler);
		return () => mql.removeEventListener("change", handler);
	}, []);
	return isLarge;
}

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
	widgetKey,
	gridStyle,
	editing,
	onRemove,
	children,
}: {
	widgetKey: WidgetKey;
	gridStyle?: CSSProperties;
	editing: boolean;
	onRemove: () => void;
	children: ReactNode;
}) {
	const { t } = useTranslation();
	const { attributes, listeners, setNodeRef, isDragging } = useSortable({
		id: widgetKey,
	});
	const catalogEntry = WIDGET_CATALOG[widgetKey];

	// No transform/transition from useSortable is applied here on purpose -
	// dnd-kit's built-in sortable animation projects a translate offset
	// assuming every item is the same size, which falls apart on this grid
	// (auto-fit spans + a dense shelf packer) and produced the "other
	// widgets... look so weirdly" glitch from #771 follow-up review
	// feedback. The dragged tile is hidden in place (opacity-20 below)
	// while OrgDashboardPage's <DragOverlay> shows a floating clone that
	// actually follows the pointer/keyboard; every other tile just stays
	// put until the drop reorders the array, then snaps straight to its
	// new packed slot - no mid-drag animation left to get wrong.
	return (
		// onMouseDown/onTouchStart below let mouse/touch users grab the card
		// ANYWHERE to move it - a plain cursor-grab affordance instead of a
		// dedicated grip icon. A separate, visually-hidden-until-focused
		// button further down keeps this fully keyboard-operable (dnd-kit's
		// KeyboardSensor needs a real focusable element carrying
		// {...attributes} {...listeners}) without adding a permanently
		// visible icon back - which is also why suppressing the
		// static-element-interactions rule here is safe: this div never
		// needs its own role/keyboard handling, that hidden button is the
		// real interactive element for keyboard users. The sensors'
		// activationConstraint (see OrgDashboardPage) keeps this from
		// swallowing plain clicks on the remove button, which lives inside
		// the same element. Touch specifically uses TouchSensor's delay (not
		// MouseSensor's distance), which - unlike distance-based activation -
		// doesn't require disabling touch-action, so a quick swipe still
		// scrolls the page normally and only a deliberate hold claims the
		// gesture for dragging.
		// eslint-disable-next-line jsx-a11y/no-static-element-interactions
		<div
			ref={setNodeRef}
			data-testid={`widget-tile-${widgetKey}`}
			onMouseDown={(event) => {
				if (editing) listeners?.onMouseDown?.(event);
			}}
			onTouchStart={(event) => {
				if (editing) listeners?.onTouchStart?.(event);
			}}
			style={gridStyle}
			// A low but nonzero opacity (not opacity-0) while dragging - a
			// keyboard-initiated drag (KeyboardSensor) leaves real DOM focus on
			// the hidden grip button inside this tile, and a fully invisible
			// ancestor would take its focus ring with it (WCAG 2.4.7 Focus
			// Visible), since <DragOverlay>'s floating clone has no focus of its
			// own to show one instead.
			// No z-index here on purpose: this div is `position: relative`
			// (for the absolutely-positioned grip/remove buttons) but must NOT
			// also get a z-index, because that would give it its own stacking
			// context - any modal a widget renders inside itself (e.g.
			// CreateOpportunityWidget's wizard) would then be scoped to THIS
			// tile's stacking order instead of the page's, so a later sibling
			// tile (also positioned) could paint over the modal despite its own
			// z-[2000] and swallow clicks meant for it. The green backdrop
			// cells are plain `position: static` divs, so per CSS painting
			// order this positioned-but-z-index-less tile already paints
			// above them with no z-index needed at all - a real regression
			// this caused, caught by CI.
			className={`relative h-full ${editing ? "cursor-grab active:cursor-grabbing" : ""} ${editing && isDragging ? "opacity-20" : ""}`}
		>
			<div inert={editing} className={`h-full ${editing ? "opacity-60" : ""}`}>
				{children}
			</div>
			{editing && (
				<>
					{/* Visually hidden until it receives keyboard focus (Tab) -
					pointer-events-none keeps a mouse from ever clicking it while
					hidden, so the only way to reach it is Tab, at which point
					:focus makes it visible. Always present for screen readers
					regardless of visual state (opacity, unlike display/clip-based
					hiding, doesn't remove it from the accessibility tree). */}
					<button
						type="button"
						{...attributes}
						{...listeners}
						className="pointer-events-none absolute left-1/2 top-2 z-30 -translate-x-1/2 cursor-grab rounded-lg bg-white p-1.5 text-gray-600 opacity-0 shadow-md ring-1 ring-gray-200 transition-opacity focus:pointer-events-auto focus:opacity-100 active:cursor-grabbing"
						aria-label={t("orgDashboard.dragToReorder", {
							widget: t(catalogEntry.titleKey),
						})}
					>
						<GripIcon />
					</button>
					<button
						type="button"
						onClick={onRemove}
						className="absolute right-2 top-2 z-20 rounded-lg bg-white/95 p-1.5 text-gray-500 shadow-sm ring-1 ring-gray-200 hover:bg-red-50 hover:text-red-600"
						aria-label={t("orgDashboard.removeWidget", {
							widget: t(catalogEntry.titleKey),
						})}
					>
						<TrashIcon />
					</button>
				</>
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
	dimensions,
}: {
	widgetKey: WidgetKey;
	dimensions: { width: number; height: number } | null;
}) {
	const { t } = useTranslation();
	const catalogEntry = WIDGET_CATALOG[widgetKey];
	return (
		<div
			style={
				dimensions
					? { width: dimensions.width, height: dimensions.height }
					: undefined
			}
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
	const isLargeViewport = useIsLargeViewport();

	// Bumped after a published opportunity is created so the Calendar and
	// Upcoming Opportunities widgets (which each own their own data) refetch.
	const [refreshKey, setRefreshKey] = useState(0);

	// Renders the default layout immediately rather than gating the whole
	// grid behind the GET .../dashboard/layout round trip - most visits get
	// exactly this layout back anyway (no customization saved yet), and a
	// returning organizer's customized layout swaps in a moment later instead
	// of blocking first paint on it.
	const [savedLayout, setSavedLayout] = useState<WidgetKey[]>(DEFAULT_LAYOUT);
	const [editing, setEditing] = useState(false);
	const [draftLayout, setDraftLayout] = useState<WidgetKey[] | null>(null);
	const [saving, setSaving] = useState(false);
	const [showAddWidgetModal, setShowAddWidgetModal] = useState(false);
	// Tracks which widget (if any) is mid-drag so <DragOverlay> can render a
	// floating clone of it - see EditableWidgetTile's comment on why the
	// dragged tile itself no longer carries a dnd-kit transform.
	const [activeDragKey, setActiveDragKey] = useState<WidgetKey | null>(null);
	const [activeDragDimensions, setActiveDragDimensions] = useState<{
		width: number;
		height: number;
	} | null>(null);

	useEffect(() => {
		api
			.getDashboardLayout(organizationId)
			.then((response) => {
				const sanitized = response.widgets
					.map((w) => sanitizeWidgetKey(w.widgetKey))
					.filter((k): k is WidgetKey => k !== null);
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
	const availableToAdd = WIDGET_KEYS.filter((key) => !layout.includes(key));

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
				widgets: draftLayout.map((widgetKey) => ({ widgetKey })),
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
		setDraftLayout((prev) => (prev ?? []).filter((k) => k !== key));
	}

	function handleAddWidget(key: WidgetKey) {
		setDraftLayout((prev) => [...(prev ?? []), key]);
	}

	const sensors = useSensors(
		// A movement threshold before a drag activates, now that the whole
		// tile (not just the grip button) carries a mousedown listener -
		// without it, a plain click on the remove button (which lives inside
		// that same tile) would be swallowed as a zero-distance drag instead
		// of firing its own onClick.
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
		setActiveDragDimensions(
			rect ? { width: rect.width, height: rect.height } : null,
		);
	}

	// Reorders live as the dragged tile crosses another one, instead of only
	// on drop - #771 follow-up review feedback ("the widgets should update
	// when I drag already"). Every other tile carries no dnd-kit transform
	// (see EditableWidgetTile), so this reorder just snaps the grid straight
	// to its new packed layout on each crossing rather than animating a
	// projected offset.
	function handleDragOver(event: DragOverEvent) {
		const { active, over } = event;
		if (!over || active.id === over.id) return;
		setDraftLayout((prev) => {
			const current = prev ?? [];
			const oldIndex = current.indexOf(active.id as WidgetKey);
			const newIndex = current.indexOf(over.id as WidgetKey);
			if (oldIndex === -1 || newIndex === -1) return current;
			return arrayMove(current, oldIndex, newIndex);
		});
	}

	// The layout is already correct by drop time (handleDragOver keeps it live
	// updated) - this just clears the drag-overlay UI state.
	function handleDragEnd() {
		setActiveDragKey(null);
		setActiveDragDimensions(null);
	}

	// dnd-kit fires this on Escape (and a few other abandon paths) instead of
	// onDragEnd - without it, an escaped drag would leave the source tile
	// permanently at opacity-20 (see EditableWidgetTile) with no overlay to
	// show for it.
	function handleDragCancel() {
		setActiveDragKey(null);
		setActiveDragDimensions(null);
	}

	function renderWidget(key: WidgetKey, size: WidgetSizeClass) {
		switch (key) {
			case "ToDo":
				return <ToDoWidget organizationId={organizationId} size={size} />;
			case "UpcomingOpportunities":
				return (
					<UpcomingOpportunitiesWidget
						organizationId={organizationId}
						refreshKey={refreshKey}
						size={size}
					/>
				);
			case "Calendar":
				return (
					<CalendarWidget
						organizationId={organizationId}
						refreshKey={refreshKey}
						size={size}
					/>
				);
			case "Settings":
				return <SettingsWidget org={org} size={size} />;
			case "CreateOpportunity":
				return (
					<CreateOpportunityWidget
						organizationId={organizationId}
						onCreated={handleOpportunityCreated}
						size={size}
					/>
				);
			case "QuickCheckIn":
				return (
					<QuickCheckInWidget organizationId={organizationId} size={size} />
				);
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

	// Shelf-packs the (drag-ordered) layout onto the 8-column grid every
	// render - cheap for the handful of widgets a dashboard has, and this is
	// what replaces manual sizing entirely: each widget's column/row span is
	// a pure function of how much room is left when its turn comes, not a
	// choice the organizer makes (#771 follow-up review feedback - "forget
	// about the sizes slider... widgets should take as much place as left").
	const { placed, totalRows } = packWidgets(layout);
	const packedByKey = new Map(placed.map((p) => [p.widgetKey, p]));

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
			className="grid grid-cols-1 gap-4 lg:grid-cols-8 lg:auto-rows-[minmax(64px,auto)]"
		>
			{/* Light green cell backdrop behind the whole grid while editing, so
			an organizer can see the underlying 8-column structure (and exactly
			how many rows the current layout needs, +1 spare) instead of it
			being invisible until something occupies it - #771 follow-up review
			feedback ("no visual grid rendering... maybe I can render very
			light green grids"). These are real grid items placed in this same
			grid (not a separately-positioned overlay), so their row tracks are
			guaranteed to line up with the real widgets' even if a widget's
			content grows taller than its nominal row estimate. `-m-1` lets each
			cell bleed slightly past its own track into the gap, so it visibly
			"expands a bit over" whatever widget sits on top of it. Gated on
			isLargeViewport since the grid itself collapses to a single stacked
			column below `lg`, where this wouldn't mean anything. */}
			{editing &&
				isLargeViewport &&
				Array.from({ length: (totalRows + 1) * 8 }, (_, i) => {
					const col = (i % 8) + 1;
					const row = Math.floor(i / 8) + 1;
					return (
						<div
							key={`grid-guide-${col}-${row}`}
							data-testid="dashboard-grid-guide-cell"
							aria-hidden="true"
							className="pointer-events-none -m-1 rounded-md bg-green-300/40"
							style={{ gridColumn: col, gridRow: row }}
						/>
					);
				})}
			{layout.map((widgetKey) => {
				const packed = packedByKey.get(widgetKey);
				if (!packed) return null;
				const sizeClass = isLargeViewport
					? classifyWidth(packed.colSpan)
					: "compact";
				return (
					<EditableWidgetTile
						key={widgetKey}
						widgetKey={widgetKey}
						gridStyle={
							isLargeViewport
								? {
										// Explicit start line (not just `span N`) is required
										// here: the green backdrop cells above claim every
										// single cell of the grid explicitly, which fully
										// saturates CSS Grid's auto-placement algorithm for this
										// row range. A widget tile placed with only a span (no
										// start) would have nowhere left to auto-place into and
										// would overflow into new auto-generated rows below the
										// entire backdrop - exactly the "widgets render below
										// the green grid instead of on top of it" bug from #762
										// follow-up feedback. Matching the same explicit
										// coordinates `packWidgets` gave the backdrop is what
										// makes the real tile land in - and paint over, per DOM
										// order - its intended cells instead.
										gridColumn: `${packed.col} / span ${packed.colSpan}`,
										gridRow: `${packed.row} / span ${packed.rowSpan}`,
									}
								: undefined
						}
						editing={editing}
						onRemove={() => handleRemoveWidget(widgetKey)}
					>
						{renderWidget(widgetKey, sizeClass)}
					</EditableWidgetTile>
				);
			})}
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
				onDragOver={handleDragOver}
				onDragEnd={handleDragEnd}
				onDragCancel={handleDragCancel}
			>
				<SortableContext items={layout} strategy={rectSortingStrategy}>
					{grid}
				</SortableContext>
				<DragOverlay>
					{activeDragKey && (
						<WidgetDragPreview
							widgetKey={activeDragKey}
							dimensions={activeDragDimensions}
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
