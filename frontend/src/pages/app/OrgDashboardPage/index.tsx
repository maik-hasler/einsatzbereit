import {
	useCallback,
	useEffect,
	useMemo,
	useState,
	type CSSProperties,
	type KeyboardEvent,
	type ReactNode,
} from "react";
import { useNavigate, useOutletContext } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrgAppContext } from "../../../layouts/OrgAppLayout";
import { useApiClient } from "../../../hooks/useApiClient";
import { useEditModeQuickActions } from "../../../hooks/useEditModeQuickActions";
import { dispatchToast } from "../../../lib/toastBus";
import { getApiErrorMessage } from "../../../lib/apiError";
import { PlusIcon, TrashIcon } from "../../../components/QuickActionIcons";
import EmptyState from "../../../components/EmptyState";
import AddWidgetModal from "./AddWidgetModal";
import CalendarWidget from "./CalendarWidget";
import UpcomingOpportunitiesWidget from "./UpcomingOpportunitiesWidget";
import ToDoWidget from "./ToDoWidget";
import SettingsWidget from "./SettingsWidget";
import CreateOpportunityWidget from "./CreateOpportunityWidget";
import QuickCheckInWidget from "./QuickCheckInWidget";
import SettingsIconWidget from "./SettingsIconWidget";
import {
	DEFAULT_LAYOUT,
	GRID_COLUMNS,
	WIDGET_CATALOG,
	WIDGET_KEYS,
	classifyWidth,
	isValidPlacement,
	placeNewWidget,
	sanitizeWidgetKey,
	type PlacedWidget,
	type WidgetKey,
	type WidgetSizeClass,
} from "./widgetCatalog";

// A single grid cell, 1-based - the unit both the corner-to-corner mouse/
// touch flow and the keyboard flow move a cursor around in before locking
// it as one of a placement's two corners (see startPlacing/handleAdvance
// below).
interface Cell {
	col: number;
	row: number;
}

// Arrow-key movement has no natural upper bound the way GRID_COLUMNS bounds
// columns - the grid grows downward as far as an organizer wants. This is
// just a sane ceiling so a held-down arrow key can't runaway-render an
// unbounded number of backdrop cells.
const MAX_GRID_ROWS = 100;

function normalizeRect(a: Cell, b: Cell, widgetKey: WidgetKey): PlacedWidget {
	return {
		widgetKey,
		x: Math.min(a.col, b.col),
		y: Math.min(a.row, b.row),
		width: Math.abs(a.col - b.col) + 1,
		height: Math.abs(a.row - b.row) + 1,
	};
}

function cellInRect(col: number, row: number, rect: PlacedWidget): boolean {
	return (
		col >= rect.x &&
		col < rect.x + rect.width &&
		row >= rect.y &&
		row < rect.y + rect.height
	);
}

// Matches Tailwind's default `lg` breakpoint, which is also where the
// widget grid switches from a single stacked column to the real 8-column
// grid (see the grid container's className below) - corner-to-corner
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
	showPlacementControls,
	isPlacing,
	hasAnchor,
	placingDisabled,
	onAdvance,
	onArrowKeyDown,
	onRemove,
	children,
}: {
	widgetKey: WidgetKey;
	gridStyle?: CSSProperties;
	editing: boolean;
	showPlacementControls: boolean;
	isPlacing: boolean;
	hasAnchor: boolean;
	placingDisabled: boolean;
	onAdvance: () => void;
	onArrowKeyDown: (event: KeyboardEvent<HTMLButtonElement>) => void;
	onRemove: () => void;
	children: ReactNode;
}) {
	const { t } = useTranslation();
	const catalogEntry = WIDGET_CATALOG[widgetKey];
	const title = t(catalogEntry.titleKey);
	const moveLabel = !isPlacing
		? t("orgDashboard.moveOrResize", { widget: title })
		: hasAnchor
			? t("orgDashboard.placementPickEndCorner", { widget: title })
			: t("orgDashboard.placementPickStartCorner", { widget: title });

	return (
		<div
			data-testid={`widget-tile-${widgetKey}`}
			style={gridStyle}
			// No z-index here on purpose: this div is `position: relative` (for
			// the absolutely-positioned move/remove buttons) but must NOT also
			// get a z-index, because that would give it its own stacking
			// context - any modal a widget renders inside itself (e.g.
			// CreateOpportunityWidget's wizard) would then be scoped to THIS
			// tile's stacking order instead of the page's, so a later sibling
			// tile (also positioned) could paint over the modal despite its own
			// z-[2000] and swallow clicks meant for it. That same
			// position:relative is exactly why this tile paints ABOVE the
			// green backdrop cells beneath it regardless of DOM order (a
			// positioned element always paints above an in-flow, non-
			// positioned sibling within the same stacking context) - which
			// would otherwise block clicks meant for a backdrop cell that
			// happens to fall under this widget's own current footprint
			// (e.g. clicking its existing top-left corner to start a
			// resize from there). pointer-events-none on this wrapper while
			// editing lets those clicks pass through to the grid beneath;
			// the two buttons below opt back in with pointer-events-auto,
			// which CSS honors even under a pointer-events-none ancestor.
			className={`relative h-full ${editing ? "pointer-events-none" : ""} ${editing && isPlacing ? "ring-2 ring-brand-500" : ""}`}
		>
			<div inert={editing} className={`h-full ${editing ? "opacity-60" : ""}`}>
				{children}
			</div>
			{editing && (
				<>
					{showPlacementControls && (
						// Unlike the old drag-to-reorder grip (hidden until keyboard
						// focus, since mouse users dragged the whole tile directly),
						// this button IS the mouse/touch entry point into placement -
						// clicking it is how they start placing, before drawing the
						// box on the grid guide cells (see handleCellClick) - so it
						// must stay visible and clickable the same way the Remove
						// button below always does, not just on focus.
						<button
							type="button"
							onClick={onAdvance}
							onKeyDown={onArrowKeyDown}
							disabled={placingDisabled}
							className={`pointer-events-auto absolute left-1/2 top-2 z-30 -translate-x-1/2 cursor-pointer rounded-lg bg-white p-1.5 text-gray-600 shadow-md ring-1 ring-gray-200 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-30 ${isPlacing ? "ring-2 ring-brand-500" : ""}`}
							aria-label={moveLabel}
						>
							<GripIcon />
						</button>
					)}
					<button
						type="button"
						onClick={onRemove}
						disabled={placingDisabled}
						className="pointer-events-auto absolute right-2 top-2 z-20 rounded-lg bg-white/95 p-1.5 text-gray-500 shadow-sm ring-1 ring-gray-200 hover:bg-red-50 hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-30"
						aria-label={t("orgDashboard.removeWidget", { widget: title })}
					>
						<TrashIcon />
					</button>
				</>
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
	const isLargeViewport = useIsLargeViewport();

	// Bumped after a published opportunity is created so the Calendar and
	// Upcoming Opportunities widgets (which each own their own data) refetch.
	const [refreshKey, setRefreshKey] = useState(0);

	// Renders the default layout immediately rather than gating the whole
	// grid behind the GET .../dashboard/layout round trip - most visits get
	// exactly this layout back anyway (no customization saved yet), and a
	// returning organizer's customized layout swaps in a moment later instead
	// of blocking first paint on it.
	const [savedLayout, setSavedLayout] =
		useState<PlacedWidget[]>(DEFAULT_LAYOUT);
	const [editing, setEditing] = useState(false);
	const [draftLayout, setDraftLayout] = useState<PlacedWidget[] | null>(null);
	const [saving, setSaving] = useState(false);
	const [showAddWidgetModal, setShowAddWidgetModal] = useState(false);

	// Corner-to-corner placement state (#782): at most one widget is being
	// placed at a time. `cursor` is the grid cell the pointer is hovering (or
	// the keyboard cursor last moved to); `anchor` is the first corner once
	// locked in (by a click/tap or an Enter/Space press) - once both corners
	// are known the placement commits. Mouse/touch and keyboard both drive
	// this same state, just through different inputs (see handleAdvance vs
	// handleCellClick vs handleArrowKeyDown).
	const [placingKey, setPlacingKey] = useState<WidgetKey | null>(null);
	const [anchor, setAnchor] = useState<Cell | null>(null);
	const [cursor, setCursor] = useState<Cell | null>(null);

	useEffect(() => {
		api
			.getDashboardLayout(organizationId)
			.then((response) => {
				const sanitized = response.widgets
					.map((w) => {
						const key = sanitizeWidgetKey(w.widgetKey);
						return key
							? {
									widgetKey: key,
									x: w.x,
									y: w.y,
									width: w.width,
									height: w.height,
								}
							: null;
					})
					.filter((w): w is PlacedWidget => w !== null);
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

	// Memoized so CreateOpportunityWidget's React.memo (see that component)
	// actually skips re-rendering while a placement is in progress - a fresh
	// function reference on every OrgDashboardPage render (which happens on
	// every cursor move while placing a widget) would defeat it otherwise.
	const handleOpportunityCreated = useCallback(
		(createdDraftId?: string) => {
			// Drafts live on the Opportunities tab now. When one is saved from
			// here, take the organizer there with the new draft highlighted so
			// it is never lost (issue #708). A published opportunity just
			// refreshes the widgets.
			if (createdDraftId) {
				navigate(
					`/app/${organizationId}/opportunities?highlight=${createdDraftId}`,
				);
				return;
			}
			setRefreshKey((k) => k + 1);
		},
		[navigate, organizationId],
	);

	function cancelPlacing() {
		setPlacingKey(null);
		setAnchor(null);
		setCursor(null);
	}

	function startPlacing(key: WidgetKey) {
		const current = (draftLayout ?? []).find((w) => w.widgetKey === key);
		if (!current) return;
		setPlacingKey(key);
		setAnchor(null);
		setCursor({ col: current.x, row: current.y });
	}

	function commitPlacement(rect: PlacedWidget) {
		if (!isValidPlacement(rect, draftLayout ?? [])) {
			dispatchToast("error", t("orgDashboard.placementOverlaps"));
			cancelPlacing();
			return;
		}
		setDraftLayout((prev) =>
			(prev ?? []).map((w) => (w.widgetKey === rect.widgetKey ? rect : w)),
		);
		cancelPlacing();
	}

	// Drives the Move/Resize button - a mouse click, or a keyboard Enter/
	// Space on the focused button (both fire a native `click` event), always
	// advance this same state machine: not placing yet -> start placing;
	// placing with no corner locked -> lock the current cursor as the first
	// corner; placing with a corner already locked -> commit using the
	// cursor as the second corner.
	function handleAdvance(key: WidgetKey) {
		if (placingKey !== key) {
			startPlacing(key);
			return;
		}
		if (!cursor) return;
		if (!anchor) {
			setAnchor(cursor);
			return;
		}
		commitPlacement(normalizeRect(anchor, cursor, key));
	}

	// A click/tap directly on a grid guide cell is the mouse/touch equivalent
	// of the keyboard's arrow-keys-then-Enter: it moves the cursor there AND
	// immediately locks/commits, since there's no separate "hover" step on
	// touch. Mouse users additionally get a live preview via onPointerEnter
	// (handleCellHover) before they click.
	function handleCellClick(cell: Cell) {
		if (!placingKey) return;
		setCursor(cell);
		if (!anchor) {
			setAnchor(cell);
			return;
		}
		commitPlacement(normalizeRect(anchor, cell, placingKey));
	}

	function handleCellHover(cell: Cell) {
		if (!placingKey) return;
		setCursor(cell);
	}

	function handleArrowKeyDown(
		event: KeyboardEvent<HTMLButtonElement>,
		key: WidgetKey,
	) {
		if (placingKey !== key || !cursor) return;
		switch (event.key) {
			case "ArrowUp":
				event.preventDefault();
				setCursor({ col: cursor.col, row: Math.max(1, cursor.row - 1) });
				break;
			case "ArrowDown":
				event.preventDefault();
				setCursor({
					col: cursor.col,
					row: Math.min(MAX_GRID_ROWS, cursor.row + 1),
				});
				break;
			case "ArrowLeft":
				event.preventDefault();
				setCursor({ col: Math.max(1, cursor.col - 1), row: cursor.row });
				break;
			case "ArrowRight":
				event.preventDefault();
				setCursor({
					col: Math.min(GRID_COLUMNS, cursor.col + 1),
					row: cursor.row,
				});
				break;
			case "Escape":
				event.preventDefault();
				cancelPlacing();
				break;
		}
	}

	async function handleSave() {
		if (!draftLayout) return;
		setSaving(true);
		try {
			await api.saveDashboardLayout(organizationId, {
				widgets: draftLayout.map((w) => ({
					widgetKey: w.widgetKey,
					x: w.x,
					y: w.y,
					width: w.width,
					height: w.height,
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
		cancelPlacing();
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
		const wasPlacing = placingKey === key;
		setDraftLayout((prev) => (prev ?? []).filter((w) => w.widgetKey !== key));
		if (wasPlacing) {
			cancelPlacing();
			// Removing the widget currently being placed unmounts the very
			// button that had focus (its whole tile disappears), dropping
			// focus to <body> with nothing to restore it - move it somewhere
			// stable and still meaningful instead. The widget just removed is
			// always addable again, so "Add Widget" is guaranteed to exist by
			// the next frame.
			requestAnimationFrame(() => {
				document
					.querySelector<HTMLButtonElement>(
						'[data-testid="quick-action-add-widget"]',
					)
					?.focus();
			});
		}
	}

	function handleAddWidget(key: WidgetKey) {
		setDraftLayout((prev) => {
			const current = prev ?? [];
			return [...current, placeNewWidget(key, current)];
		});
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
					<QuickCheckInWidget
						organizationId={organizationId}
						refreshKey={refreshKey}
						size={size}
					/>
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

	const previewRect = useMemo(() => {
		if (!placingKey || !cursor) return null;
		return normalizeRect(anchor ?? cursor, cursor, placingKey);
	}, [placingKey, anchor, cursor]);
	const previewValid = previewRect
		? isValidPlacement(previewRect, draftLayout ?? [])
		: false;

	const contentRows = layout.length
		? Math.max(1, ...layout.map((w) => w.y + w.height - 1))
		: 1;
	const guideRows = Math.max(contentRows, cursor?.row ?? 0) + 4;

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
			an organizer can see the underlying 8-column structure. These cells
			double as the corner-to-corner placement surface: while a widget is
			being placed, they become clickable (see handleCellClick) and are
			tinted blue/red to preview whether the current selection is a valid
			placement. Gated on isLargeViewport since the grid itself collapses
			to a single stacked column below `lg`, where this wouldn't mean
			anything. */}
			{editing &&
				isLargeViewport &&
				Array.from({ length: guideRows * GRID_COLUMNS }, (_, i) => {
					const col = (i % GRID_COLUMNS) + 1;
					const row = Math.floor(i / GRID_COLUMNS) + 1;
					const inPreview = previewRect && cellInRect(col, row, previewRect);
					const tint = inPreview
						? previewValid
							? "bg-brand-300/50"
							: "bg-red-400/50"
						: "bg-green-300/40";
					return (
						<div
							key={`grid-guide-${col}-${row}`}
							data-testid="dashboard-grid-guide-cell"
							aria-hidden="true"
							onClick={
								placingKey ? () => handleCellClick({ col, row }) : undefined
							}
							onPointerEnter={
								placingKey ? () => handleCellHover({ col, row }) : undefined
							}
							className={`-m-1 rounded-md ${tint} ${placingKey ? "cursor-pointer" : "pointer-events-none"}`}
							style={{ gridColumn: col, gridRow: row }}
						/>
					);
				})}
			{layout.map((widget) => {
				const isPlacingThis = placingKey === widget.widgetKey;
				const rect = isPlacingThis && previewRect ? previewRect : widget;
				const sizeClass = isLargeViewport
					? classifyWidth(rect.width)
					: "compact";
				return (
					<EditableWidgetTile
						key={widget.widgetKey}
						widgetKey={widget.widgetKey}
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
										// entire backdrop.
										gridColumn: `${rect.x} / span ${rect.width}`,
										gridRow: `${rect.y} / span ${rect.height}`,
									}
								: undefined
						}
						editing={editing}
						showPlacementControls={isLargeViewport}
						isPlacing={isPlacingThis}
						hasAnchor={isPlacingThis && anchor !== null}
						placingDisabled={placingKey !== null && !isPlacingThis}
						onAdvance={() => handleAdvance(widget.widgetKey)}
						onArrowKeyDown={(e) => handleArrowKeyDown(e, widget.widgetKey)}
						onRemove={() => handleRemoveWidget(widget.widgetKey)}
					>
						{renderWidget(widget.widgetKey, sizeClass)}
					</EditableWidgetTile>
				);
			})}
		</div>
	);

	const placingTitle = placingKey ? t(WIDGET_CATALOG[placingKey].titleKey) : "";

	return (
		<>
			{editing && isLargeViewport && placingKey && cursor && (
				<div className="mb-3 flex items-center justify-between gap-3 rounded-lg bg-brand-50 px-4 py-2 text-sm text-brand-800">
					{/* role="status" makes this a live region, so screen readers
					announce it not just when placing starts but on every
					subsequent update - including the column/row text changing as
					arrow keys move the cursor, which is otherwise silent to
					assistive tech (the tinted preview cells are a purely visual
					cue). */}
					<p role="status" data-testid="dashboard-placement-status">
						{anchor
							? t("orgDashboard.placingBannerPickEnd", {
									widget: placingTitle,
									column: cursor.col,
									row: cursor.row,
								})
							: t("orgDashboard.placingBannerPickStart", {
									widget: placingTitle,
									column: cursor.col,
									row: cursor.row,
								})}
					</p>
					{/* Escape cancels for keyboard users via the Move/Resize
					button's own onKeyDown - mouse/touch users have no equivalent
					gesture (every grid-cell click locks/commits a corner
					immediately), so this is their only way to back out. */}
					<button
						type="button"
						onClick={cancelPlacing}
						className="shrink-0 rounded-lg px-2 py-1 text-xs font-semibold text-brand-800 hover:bg-brand-100"
					>
						{t("common.cancel")}
					</button>
				</div>
			)}

			{grid}

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
