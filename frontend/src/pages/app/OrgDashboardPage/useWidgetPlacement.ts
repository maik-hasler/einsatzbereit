import {
	useEffect,
	useMemo,
	useRef,
	useState,
	type Dispatch,
	type KeyboardEvent,
	type PointerEvent as ReactPointerEvent,
	type SetStateAction,
} from "react";
import { useTranslation } from "react-i18next";
import { dispatchToast } from "../../../lib/toastBus";
import {
	GRID_COLUMNS,
	GRID_MAX_ROWS,
	isValidPlacement,
	settlePlacement,
	type PlacedWidget,
	type WidgetKey,
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

export function cellInRect(
	col: number,
	row: number,
	rect: PlacedWidget,
): boolean {
	return (
		col >= rect.x &&
		col < rect.x + rect.width &&
		row >= rect.y &&
		row < rect.y + rect.height
	);
}

interface UseWidgetPlacementOptions {
	draftLayout: PlacedWidget[] | null;
	setDraftLayout: Dispatch<SetStateAction<PlacedWidget[] | null>>;
}

// Owns widget repositioning/resizing across all three input modalities the
// dashboard's edit mode supports: a real pointer drag on a widget's tile or
// resize handle (#16, mouse/touch), the click-click-click corner-to-corner
// flow (mouse/touch tap on a grid-guide cell), and its keyboard equivalent
// (arrow keys + Enter/Space on the Move/Resize button, #17). All three
// funnel into the same `placingKey`/`anchor`/`cursor`/commitPlacement state
// machine below so OrgDashboardPage itself only has to wire up input events
// to the handlers this hook returns.
export function useWidgetPlacement({
	draftLayout,
	setDraftLayout,
}: UseWidgetPlacementOptions) {
	const { t } = useTranslation();

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

	return {
		placingKey,
		anchor,
		cursor,
		activeKey,
		previewRect,
		previewValid,
		cancelPlacing,
		handleAdvance,
		handleCellClick,
		handleCellHover,
		handleArrowKeyDown,
		startDrag,
	};
}
