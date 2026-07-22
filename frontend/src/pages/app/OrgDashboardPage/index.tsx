import {
	useCallback,
	useEffect,
	useMemo,
	useRef,
	useState,
	type CSSProperties,
	type KeyboardEvent,
	type PointerEvent as ReactPointerEvent,
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
	GRID_MAX_ROWS,
	WIDGET_CATALOG,
	WIDGET_KEYS,
	classifyWidth,
	compactLayout,
	isValidPlacement,
	placeNewWidget,
	sanitizeWidgetKey,
	settlePlacement,
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

// A real pointer drag (#16) in progress on one widget's grip (move) or
// resize handle. `origRect` is that widget's position when the drag
// started; `currentRect` is the live, cell-snapped position/size under the
// pointer right now (authoritative - dragPreview state below just mirrors it
// for rendering). colPx/rowPx convert pixel deltas to grid cells, measured
// off the widget's own rendered tile at drag start rather than the grid
// container, so it stays correct at any viewport width. `moved` gates
// whether releasing the pointer counts as an actual drag (commit) or a
// plain click (fall through to the existing click-click-click flow via
// handleAdvance).
interface DragSession {
	key: WidgetKey;
	mode: "move" | "resize";
	startClientX: number;
	startClientY: number;
	colPx: number;
	rowPx: number;
	origRect: PlacedWidget;
	currentRect: PlacedWidget;
	moved: boolean;
}

// How far the pointer has to travel from its down-position before a
// grip/resize-handle press counts as a drag rather than a click - keeps a
// slightly-jittery tap/click from being misread as an accidental 1-cell move.
const DRAG_THRESHOLD_PX = 4;

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

function ResizeHandleIcon() {
	return (
		<svg
			className="h-3.5 w-3.5"
			viewBox="0 0 16 16"
			fill="none"
			stroke="currentColor"
			strokeWidth="1.5"
			strokeLinecap="round"
			aria-hidden="true"
		>
			<path d="M13 3 3 13M13 8 8 13" />
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
	isCornerFlowActive,
	onAdvance,
	onArrowKeyDown,
	onRemove,
	onGripPointerDown,
	onResizePointerDown,
	children,
}: {
	widgetKey: WidgetKey;
	gridStyle?: CSSProperties;
	editing: boolean;
	showPlacementControls: boolean;
	isPlacing: boolean;
	hasAnchor: boolean;
	placingDisabled: boolean;
	// Whether the click-click-click/keyboard corner flow specifically (not a
	// real pointer drag, #16) is what's active for this widget - `isPlacing`
	// alone can't tell the two apart (a drag sets it too, for the ring
	// highlight below), and `hasAnchor`/hasAnchor's "pick a corner" wording
	// would be actively wrong read out mid-drag if used for that.
	isCornerFlowActive: boolean;
	onAdvance: () => void;
	onArrowKeyDown: (event: KeyboardEvent<HTMLButtonElement>) => void;
	onRemove: () => void;
	onGripPointerDown: (event: ReactPointerEvent<HTMLElement>) => void;
	onResizePointerDown: (event: ReactPointerEvent<HTMLElement>) => void;
	children: ReactNode;
}) {
	const { t } = useTranslation();
	const catalogEntry = WIDGET_CATALOG[widgetKey];
	const title = t(catalogEntry.titleKey);
	const moveLabel = !isCornerFlowActive
		? t("orgDashboard.moveOrResize", { widget: title })
		: hasAnchor
			? t("orgDashboard.placementPickEndCorner", { widget: title })
			: t("orgDashboard.placementPickStartCorner", { widget: title });

	return (
		<div
			data-testid={`widget-tile-${widgetKey}`}
			style={gridStyle}
			// No z-index here on purpose: this div is `position: relative` (for
			// the absolutely-positioned remove button and the tile itself acting
			// as the drag-to-move surface) but must NOT also get a z-index,
			// because that would give it its own stacking context - any modal a
			// widget renders inside itself (e.g. CreateOpportunityWidget's
			// wizard) would then be scoped to THIS tile's stacking order instead
			// of the page's, so a later sibling tile (also positioned) could
			// paint over the modal despite its own z-[2000] and swallow clicks
			// meant for it.
			//
			// The whole tile is the primary press-and-drag-to-move target while
			// editing (not just a small grip icon) - dashboard-builder UIs that
			// make organizers hunt for a tiny handle before they can reposition
			// anything are exactly the friction this is meant to remove. That
			// means the tile opts INTO pointer events here (rather than passing
			// clicks through to the grid-guide backdrop beneath, as it used to) -
			// but only while NO placement is active at all anywhere on the
			// board (!isPlacing && !placingDisabled, i.e. activeKey === null).
			// Once ANY widget's placement is active (real drag or the
			// click-click-click flow), completing it very often means clicking
			// a backdrop cell that falls inside another widget's current
			// footprint - that's the whole point of #18 (an overlapping
			// placement displaces what's in the way instead of being
			// rejected), so every OTHER tile must let those clicks through
			// too, not just the one actually being placed. A real drag
			// already committed to via document-level pointermove/pointerup
			// listeners isn't affected by tiles losing pointer-events
			// mid-drag - only where a NEW press lands is.
			onPointerDown={
				editing && showPlacementControls && !placingDisabled && !isPlacing
					? onGripPointerDown
					: undefined
			}
			className={`relative h-full ${
				editing && showPlacementControls && !isPlacing && !placingDisabled
					? "cursor-grab touch-none active:cursor-grabbing"
					: editing
						? "pointer-events-none"
						: ""
			} ${editing && isPlacing ? "ring-2 ring-brand-500" : ""}`}
		>
			<div inert={editing} className={`h-full ${editing ? "opacity-60" : ""}`}>
				{children}
			</div>
			{editing && (
				<>
					{showPlacementControls && (
						// Still the entry point into the click-click-click
						// corner-to-corner flow (onClick) and its accessible
						// keyboard path (onKeyDown's arrow keys/Enter, #17) - the
						// whole tile above now also starts a real pointer drag
						// (#16) on its own, so this button's own onPointerDown
						// would otherwise fire a second, redundant drag-start for
						// the exact same press; stopPropagation keeps it to one.
						<button
							type="button"
							onClick={onAdvance}
							onKeyDown={onArrowKeyDown}
							onPointerDown={(e) => {
								e.stopPropagation();
								onGripPointerDown(e);
							}}
							disabled={placingDisabled}
							className={`pointer-events-auto absolute left-1/2 top-2 z-30 -translate-x-1/2 cursor-pointer touch-none rounded-lg bg-white p-1.5 text-gray-600 shadow-md ring-1 ring-gray-200 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-30 ${isPlacing ? "ring-2 ring-brand-500" : ""}`}
							aria-label={moveLabel}
						>
							<GripIcon />
						</button>
					)}
					{showPlacementControls && (
						// Pure mouse/touch drag-to-resize affordance (#16) - the
						// existing grip button + arrow keys already cover resizing
						// accessibly via the two-corner flow, so this one is taken out
						// of the tab order and hidden from assistive tech (#17) rather
						// than exposing a second, keyboard-inert control for the same
						// capability. stopPropagation is required here, not just an
						// optimization - without it, the tile's own move-drag handler
						// (above) would also see this same press and immediately
						// overwrite the resize session with a move session.
						<button
							type="button"
							tabIndex={-1}
							aria-hidden="true"
							onPointerDown={(e) => {
								e.stopPropagation();
								onResizePointerDown(e);
							}}
							disabled={placingDisabled}
							className="pointer-events-auto absolute bottom-2 right-2 z-20 cursor-nwse-resize touch-none rounded-lg bg-white/95 p-1 text-gray-500 shadow-sm ring-1 ring-gray-200 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-30"
						>
							<ResizeHandleIcon />
						</button>
					)}
					<button
						type="button"
						onPointerDown={(e) => e.stopPropagation()}
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

	// Real pointer drag (#16) - a second, independent way to reposition a
	// widget alongside the click-click-click/keyboard flow above. `dragActive`
	// only exists to (de)register the document-level pointermove/pointerup
	// listeners at the right time; the session's actual live data lives in
	// dragSessionRef (a ref, not state, since pointermove fires far more often
	// than a re-render is useful for) and dragPreview mirrors just enough of
	// it to render the live preview + backdrop tint.
	const [dragActive, setDragActive] = useState(false);
	const [dragPreview, setDragPreview] = useState<PlacedWidget | null>(null);
	const dragSessionRef = useRef<DragSession | null>(null);
	// Set to the widget key a drag just committed via a real pointer move, so
	// the `click` event a browser still fires right after pointerup on THAT
	// widget's grip doesn't also run handleAdvance and start an unwanted
	// click-click-click placement. Keyed rather than a plain flag so a stray
	// click landing on a *different* widget's grip right after (pointerup can
	// land anywhere) doesn't have its own click wrongly swallowed too.
	const suppressNextAdvanceRef = useRef<WidgetKey | null>(null);

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
					`/app/${organizationId}/dashboard/opportunities?highlight=${createdDraftId}`,
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

	// Bounds/min-size violations still hard-reject (there's nowhere to push a
	// widget that doesn't fit on the grid at all). An overlap with another
	// widget no longer does (#18) - it displaces whatever's in the way
	// instead, then compacts the result so that displacement doesn't leave a
	// gap of its own further up.
	function commitPlacement(rect: PlacedWidget) {
		if (!isValidPlacement(rect)) {
			dispatchToast("error", t("orgDashboard.placementInvalid"));
			cancelPlacing();
			return;
		}
		setDraftLayout((prev) => {
			const others = (prev ?? []).filter((w) => w.widgetKey !== rect.widgetKey);
			return settlePlacement(rect, others);
		});
		cancelPlacing();
	}

	// Drives the Move/Resize button - a mouse click, or a keyboard Enter/
	// Space on the focused button (both fire a native `click` event), always
	// advance this same state machine: not placing yet -> start placing;
	// placing with no corner locked -> lock the current cursor as the first
	// corner; placing with a corner already locked -> commit using the
	// cursor as the second corner. Skipped once for the synthetic click that
	// follows a real pointer drag (#16) - that drag already committed via
	// startDrag's own pointerup handler.
	function handleAdvance(key: WidgetKey) {
		if (suppressNextAdvanceRef.current === key) {
			suppressNextAdvanceRef.current = null;
			return;
		}
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
					row: Math.min(GRID_MAX_ROWS, cursor.row + 1),
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

	// Starts a real pointer drag (#16) on a widget's tile (move, from
	// anywhere on it) or resize handle. Measures cell size off the widget's
	// OWN rendered tile rather than the grid container, so it stays correct
	// regardless of viewport width. A plain click/tap (pointer released
	// before moving past DRAG_THRESHOLD_PX) is left alone here - a press on
	// the grip button specifically still falls through to its own onClick
	// (handleAdvance) exactly as before; the tile itself has no click
	// handler to fall through to, so a plain click anywhere else on it is
	// simply a no-op, same as before the whole tile became draggable.
	function startDrag(
		event: ReactPointerEvent<HTMLElement>,
		key: WidgetKey,
		mode: "move" | "resize",
	) {
		if (event.pointerType === "mouse" && event.button !== 0) return;
		const widget = (draftLayout ?? []).find((w) => w.widgetKey === key);
		const tile = event.currentTarget.closest<HTMLElement>(
			`[data-testid="widget-tile-${key}"]`,
		);
		if (!widget || !tile) return;
		const box = tile.getBoundingClientRect();
		// A tile mid-layout (just resized, just re-rendered) can momentarily
		// measure as 0x0 in a real browser - dividing by that produces
		// Infinity/NaN deltas for the rest of the drag. Bail out rather than
		// starting a session on a bad measurement; the user's next press
		// gets a fresh, presumably-settled measurement.
		if (box.width <= 0 || box.height <= 0) return;
		dragSessionRef.current = {
			key,
			mode,
			startClientX: event.clientX,
			startClientY: event.clientY,
			colPx: box.width / widget.width,
			rowPx: box.height / widget.height,
			origRect: widget,
			currentRect: widget,
			moved: false,
		};
		setDragPreview(widget);
		setDragActive(true);
	}

	useEffect(() => {
		if (!dragActive) return;

		function handlePointerMove(event: globalThis.PointerEvent) {
			const session = dragSessionRef.current;
			if (!session) return;
			const deltaX = event.clientX - session.startClientX;
			const deltaY = event.clientY - session.startClientY;
			if (
				Math.abs(deltaX) > DRAG_THRESHOLD_PX ||
				Math.abs(deltaY) > DRAG_THRESHOLD_PX
			) {
				session.moved = true;
			}
			const deltaCol = Math.round(deltaX / session.colPx);
			const deltaRow = Math.round(deltaY / session.rowPx);
			const nextRect: PlacedWidget =
				session.mode === "move"
					? {
							...session.origRect,
							x: Math.max(1, session.origRect.x + deltaCol),
							y: Math.max(1, session.origRect.y + deltaRow),
						}
					: {
							...session.origRect,
							width: Math.max(1, session.origRect.width + deltaCol),
							height: Math.max(1, session.origRect.height + deltaRow),
						};
			session.currentRect = nextRect;
			setDragPreview(nextRect);
		}

		function endDrag() {
			const session = dragSessionRef.current;
			dragSessionRef.current = null;
			setDragActive(false);
			setDragPreview(null);
			if (session?.moved) {
				suppressNextAdvanceRef.current = session.key;
				commitPlacement(session.currentRect);
			}
		}

		document.addEventListener("pointermove", handlePointerMove);
		document.addEventListener("pointerup", endDrag);
		document.addEventListener("pointercancel", endDrag);
		return () => {
			document.removeEventListener("pointermove", handlePointerMove);
			document.removeEventListener("pointerup", endDrag);
			document.removeEventListener("pointercancel", endDrag);
		};
		// commitPlacement/cancelPlacing close over draftLayout - re-running
		// this setup on every draftLayout change would drop mid-drag listeners.
		// dragActive is the only thing that should ever (re)register them.
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [dragActive]);

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
			cancelPlacing();
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
		// #14: closes whatever gap removing this widget just left instead of
		// leaving the rest of the layout sitting where it was.
		setDraftLayout((prev) =>
			compactLayout((prev ?? []).filter((w) => w.widgetKey !== key)),
		);
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
			// #14: placeNewWidget only knows the tallest existing widget's
			// bottom edge, not gaps elsewhere on the grid - compacting after
			// lets the new widget slide up into one of those if it fits.
			return compactLayout([...current, placeNewWidget(key, current)]);
		});
	}

	function renderWidget(
		key: WidgetKey,
		size: WidgetSizeClass,
		heightRows: number,
	) {
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
						heightRows={heightRows}
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
				return (
					<SettingsIconWidget organizationId={organizationId} size={size} />
				);
		}
	}

	// An empty layout is ambiguous only in the API response (see the
	// useEffect above) - once resolved into `layout`, zero widgets always
	// means "genuinely nothing here right now" (either a deliberately
	// emptied saved layout, or a draft mid-edit), so it always gets the same
	// empty state rather than silently falling back to the default set.
	const isEmpty = layout.length === 0;

	// The widget currently being interactively positioned, however it got
	// there - the click-click-click/keyboard corner flow (placingKey) or a
	// real pointer drag (dragPreview, #16). previewRect is that widget's
	// live candidate rect either way; previewValid only reflects the hard
	// bounds/min-size constraints (#18: overlap is no longer one of them).
	const activeKey = dragPreview?.widgetKey ?? placingKey;
	const previewRect = useMemo(() => {
		if (dragPreview) return dragPreview;
		if (!placingKey || !cursor) return null;
		return normalizeRect(anchor ?? cursor, cursor, placingKey);
	}, [dragPreview, placingKey, anchor, cursor]);
	const previewValid = previewRect ? isValidPlacement(previewRect) : false;

	const contentRows = layout.length
		? Math.max(1, ...layout.map((w) => w.y + w.height - 1))
		: 1;
	const previewBottom = previewRect
		? previewRect.y + previewRect.height - 1
		: 0;
	// Clamped, not just summed - `Array.from({ length })` below throws a
	// RangeError (taking down the whole page, not just this component) for
	// a non-finite or absurdly large length, and Math.min/max propagate NaN
	// rather than ignoring it, so a plain Math.min(ceiling, ...) wouldn't
	// actually catch that case. contentRows/previewBottom are normally
	// well-behaved, but this is the one spot a stray NaN/Infinity anywhere
	// upstream would otherwise become unrecoverable rather than just a
	// visually-wrong backdrop.
	const rawGuideRows =
		Math.max(contentRows, cursor?.row ?? 0, previewBottom) + 4;
	const guideRows = Number.isFinite(rawGuideRows)
		? Math.min(GRID_MAX_ROWS + 4, rawGuideRows)
		: GRID_MAX_ROWS + 4;

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
				const isPlacingThis = activeKey === widget.widgetKey;
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
						isCornerFlowActive={placingKey === widget.widgetKey}
						placingDisabled={activeKey !== null && !isPlacingThis}
						onAdvance={() => handleAdvance(widget.widgetKey)}
						onArrowKeyDown={(e) => handleArrowKeyDown(e, widget.widgetKey)}
						onRemove={() => handleRemoveWidget(widget.widgetKey)}
						onGripPointerDown={(e) => startDrag(e, widget.widgetKey, "move")}
						onResizePointerDown={(e) =>
							startDrag(e, widget.widgetKey, "resize")
						}
					>
						{renderWidget(widget.widgetKey, sizeClass, rect.height)}
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
