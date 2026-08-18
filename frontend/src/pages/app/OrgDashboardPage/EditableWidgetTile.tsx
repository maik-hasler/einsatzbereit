import {
	type CSSProperties,
	type KeyboardEvent,
	type PointerEvent as ReactPointerEvent,
	type ReactNode,
} from "react";
import { useTranslation } from "react-i18next";
import {
	GripDotsIcon,
	ResizeHandleIcon,
	TrashIcon,
} from "../../../components/icons";
import ErrorBoundary from "../../../components/ErrorBoundary";
import ErrorBanner from "../../../components/ErrorBanner";
import { WIDGET_CATALOG, type WidgetKey } from "./widgetCatalog";

export default function EditableWidgetTile({
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
			className={`relative ${editing ? "h-full" : "h-full lg:h-auto"} ${
				editing && showPlacementControls && !isPlacing && !placingDisabled
					? "cursor-grab touch-none active:cursor-grabbing"
					: editing
						? "pointer-events-none"
						: ""
			} ${editing && isPlacing ? "ring-2 ring-brand-500" : ""}`}
		>
			<div
				inert={editing}
				className={`h-full ${editing ? "opacity-60" : ""} ${
					// Reserves room for the grip button below (absolutely positioned,
					// centered at the tile's top edge) instead of letting it sit on
					// top of WidgetCard's own title - on a narrow tile the two used to
					// overlap directly (#2045, PR #2038 F12).
					editing && showPlacementControls ? "pt-10" : ""
				}`}
			>
				{/* A crash inside a single widget shouldn't blank the whole dashboard
				(#1243) - the default full-page ErrorBoundary fallback would break
				this tile's grid layout, so a small inline one is used instead. It
				keeps the same landmark-region-plus-heading shape WidgetCard renders
				in the non-error case (see WidgetCard.tsx) so a screen-reader user
				navigating by region/heading still finds this widget - by name - and
				isn't left with just an anonymous error paragraph. */}
				<ErrorBoundary
					fallback={
						<section
							aria-labelledby={`widget-error-title-${widgetKey}`}
							className="flex h-full flex-col rounded-card border border-gray-100 bg-white p-5 shadow-resting"
						>
							<h2
								id={`widget-error-title-${widgetKey}`}
								className="mb-4 text-base font-semibold text-gray-900"
							>
								{title}
							</h2>
							<div className="flex flex-1 items-center justify-center">
								<ErrorBanner message={t("orgDashboard.widgetError")} />
							</div>
						</section>
					}
				>
					{children}
				</ErrorBoundary>
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
						//
						// Mouse hit-testing only (#830 follow-up), not pointer-
						// events-none outright: while the click-click-click corner
						// flow is active for this widget with no anchor picked yet,
						// its tile has collapsed to a 1x1 preview box at its own
						// current top-left cell (see previewRect/normalizeRect
						// (cursor, cursor, ...) below), putting this centered
						// button almost exactly on top of the very backdrop cell
						// the next click needs to land on - so it stops claiming
						// mouse clicks for that one narrow window. Gated on
						// isCornerFlowActive specifically, not the broader
						// isPlacing (which a fresh press's own onPointerDown/
						// startDrag call above already flips true, transiently,
						// before the browser has even dispatched this SAME click's
						// "click" event) - isCornerFlowActive only reflects
						// placingKey, set by onAdvance below, which only runs once
						// the click event actually fires - so a fresh press can
						// never self-disable its own click via this class.
						// Keyboard Enter/Space activation is untouched regardless
						// (pointer-events doesn't gate that), so the accessible
						// path this button exists for - not requiring pointer
						// precision on a tiny backdrop cell - still works exactly
						// the same via a focused Enter press.
						<button
							type="button"
							onClick={onAdvance}
							onKeyDown={onArrowKeyDown}
							onPointerDown={(e) => {
								e.stopPropagation();
								onGripPointerDown(e);
							}}
							disabled={placingDisabled}
							className={`absolute top-2 left-1/2 z-30 -translate-x-1/2 cursor-pointer touch-none rounded-lg bg-white p-1.5 text-gray-600 shadow-md ring-1 ring-gray-200 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-30 ${isCornerFlowActive && !hasAnchor ? "pointer-events-none" : "pointer-events-auto"} ${isPlacing ? "ring-2 ring-brand-500" : ""}`}
							aria-label={moveLabel}
						>
							<GripDotsIcon />
						</button>
					)}
					{showPlacementControls && !isPlacing && (
						// Pure mouse/touch drag-to-resize affordance (#16) - the
						// existing grip button + arrow keys already cover resizing
						// accessibly via the two-corner flow, so this one is taken out
						// of the tab order and hidden from assistive tech (#17) rather
						// than exposing a second, keyboard-inert control for the same
						// capability. stopPropagation is required here, not just an
						// optimization - without it, the tile's own move-drag handler
						// (above) would also see this same press and immediately
						// overwrite the resize session with a move session.
						//
						// Hidden entirely while THIS widget is being placed (#830
						// follow-up): starting a placement collapses the tile to a
						// 1x1 preview box at its own current top-left cell (see
						// previewRect/normalizeRect(cursor, cursor, ...) below) until
						// a first corner is picked - with the tile that small, this
						// handle would otherwise sit almost exactly on top of the
						// very backdrop cell the corner-to-corner flow needs the
						// next click to land on. Resizing mid-placement isn't a
						// meaningful action anyway.
						//
						// A dedicated right-edge (width-only) and bottom-edge
						// (height-only) handle pair briefly existed alongside this
						// one (#830) but got reverted (#783 review) - on top of the
						// existing grip/corner-resize/remove trio, two more
						// permanently-visible controls left too little bare tile
						// surface to grab-and-drag on the smaller widget sizes,
						// which the organizer's feedback described as "you added
						// more buttons, I can't move anything else - it's just not
						// working" rather than as an improvement.
						<button
							type="button"
							data-testid="widget-resize-handle-corner"
							tabIndex={-1}
							aria-hidden="true"
							onPointerDown={(e) => {
								e.stopPropagation();
								onResizePointerDown(e);
							}}
							disabled={placingDisabled}
							className="pointer-events-auto absolute right-2 bottom-2 z-20 cursor-nwse-resize touch-none rounded-lg bg-white/95 p-1 text-gray-500 shadow-sm ring-1 ring-gray-200 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-30"
						>
							<ResizeHandleIcon />
						</button>
					)}
					<button
						type="button"
						onPointerDown={(e) => e.stopPropagation()}
						onClick={onRemove}
						disabled={placingDisabled}
						className="pointer-events-auto absolute top-2 right-2 z-20 rounded-lg bg-white/95 p-1.5 text-gray-500 shadow-sm ring-1 ring-gray-200 hover:bg-red-50 hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-30"
						aria-label={t("orgDashboard.removeWidget", { widget: title })}
					>
						<TrashIcon />
					</button>
				</>
			)}
		</div>
	);
}
