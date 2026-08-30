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
			// `h-full` in both modes: the tile fills the grid rows the layout
			// gave it. It used to drop to `lg:h-auto` outside edit mode, which
			// is what made a saved height silently revert on save (#2322 F2).
			className={`relative h-full ${
				editing && showPlacementControls && !isPlacing && !placingDisabled
					? "cursor-grab touch-none active:cursor-grabbing"
					: editing
						? "pointer-events-none"
						: ""
			} ${
				editing
					? isPlacing
						? "ring-2 ring-brand-500"
						: "ring-1 ring-brand-600"
					: ""
			}`}
		>
			{/*
				No top padding in edit mode. The grip button below used to be
				reserved 40px of `pt-10` here so it would not sit on top of
				WidgetCard's own title (#2045, PR #2038 F12) - but the tile's grid
				height does not grow to pay for that, so the card lost 40px off its
				bottom instead: the "create opportunity" button was sliced through
				its own label, and an `inert` tile cannot be scrolled to reach what
				was cut (#2322 F4). The grip now straddles the tile's top edge
				instead, which clears the title just as well and costs the card
				nothing.
			*/}
			<div inert={editing} className="h-full">
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
						<button
							type="button"
							onClick={onAdvance}
							onKeyDown={onArrowKeyDown}
							onPointerDown={(e) => {
								e.stopPropagation();
								onGripPointerDown(e);
							}}
							disabled={placingDisabled}
							// Centred ON the tile's top edge, so its lower half falls
							// inside the card's own padding (above the title) and its
							// upper half into the grid gap - see the note above the
							// content wrapper for why it no longer reserves room.
							className={`absolute top-0 left-1/2 z-30 -translate-x-1/2 -translate-y-1/2 cursor-pointer touch-none rounded-lg bg-white p-1.5 text-gray-600 shadow-md ring-1 ring-gray-200 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-30 ${isCornerFlowActive && !hasAnchor ? "pointer-events-none" : "pointer-events-auto"} ${isPlacing ? "ring-2 ring-brand-500" : ""}`}
							aria-label={moveLabel}
							title={moveLabel}
						>
							<GripDotsIcon />
						</button>
					)}
					{showPlacementControls && !isPlacing && (
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
