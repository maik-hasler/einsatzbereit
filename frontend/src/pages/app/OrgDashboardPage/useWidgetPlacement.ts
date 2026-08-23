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

interface Cell {
	col: number;
	row: number;
}

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

export function useWidgetPlacement({
	draftLayout,
	setDraftLayout,
}: UseWidgetPlacementOptions) {
	const { t } = useTranslation();

	const [placingKey, setPlacingKey] = useState<WidgetKey | null>(null);
	const [anchor, setAnchor] = useState<Cell | null>(null);
	const [cursor, setCursor] = useState<Cell | null>(null);

	const [dragActive, setDragActive] = useState(false);
	const [dragPreview, setDragPreview] = useState<PlacedWidget | null>(null);
	const dragSessionRef = useRef<DragSession | null>(null);

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

		let rafId: number | null = null;
		let pendingRect: PlacedWidget | null = null;

		function flushPendingRect() {
			rafId = null;
			if (pendingRect) setDragPreview(pendingRect);
		}

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
			pendingRect = nextRect;
			rafId ??= requestAnimationFrame(flushPendingRect);
		}

		function endDrag() {
			if (rafId !== null) {
				cancelAnimationFrame(rafId);
				rafId = null;
			}
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
			if (rafId !== null) cancelAnimationFrame(rafId);
			document.removeEventListener("pointermove", handlePointerMove);
			document.removeEventListener("pointerup", endDrag);
			document.removeEventListener("pointercancel", endDrag);
		};

		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [dragActive]);

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
