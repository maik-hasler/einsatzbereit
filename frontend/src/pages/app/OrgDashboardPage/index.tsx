import {
	useCallback,
	useEffect,
	useMemo,
	useState,
	type MouseEvent as ReactMouseEvent,
	type PointerEvent as ReactPointerEvent,
} from "react";
import { useNavigate, useOutletContext } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrgAppContext } from "../../../layouts/OrgAppLayout";
import { useApiClient } from "../../../hooks/useApiClient";
import { usePageTitle } from "../../../hooks/usePageTitle";
import { useEditModeQuickActions } from "../../../hooks/useEditModeQuickActions";
import { dispatchToast } from "../../../lib/toastBus";
import { getApiErrorMessage } from "../../../lib/apiError";
import { PlusIcon } from "../../../components/QuickActionIcons";
import EmptyState from "../../../components/EmptyState";
import ErrorBanner from "../../../components/ErrorBanner";
import AddWidgetModal from "./AddWidgetModal";
import CalendarWidget from "./CalendarWidget";
import UpcomingOpportunitiesWidget from "./UpcomingOpportunitiesWidget";
import ToDoWidget from "./ToDoWidget";
import VolunteerStatsWidget from "./VolunteerStatsWidget";
import SettingsWidget from "./SettingsWidget";
import CreateOpportunityWidget from "./CreateOpportunityWidget";
import QuickCheckInWidget from "./QuickCheckInWidget";
import SettingsIconWidget from "./SettingsIconWidget";
import EditableWidgetTile from "./EditableWidgetTile";
import { cellInRect, useWidgetPlacement } from "./useWidgetPlacement";
import {
	DEFAULT_LAYOUT,
	GRID_COLUMNS,
	GRID_MAX_ROWS,
	WIDGET_CATALOG,
	WIDGET_KEYS,
	classifyWidth,
	compactLayout,
	placeNewWidget,
	sanitizeWidgetKey,
	type PlacedWidget,
	type WidgetKey,
	type WidgetSizeClass,
} from "./widgetCatalog";

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

// Pure action tools with no read-only value for a viewer who can't perform
// the action they trigger (creating an opportunity, running check-in) -
// filtered out of a plain Member's rendered layout entirely, unlike the
// other widgets (ToDo, Calendar, Settings, ...) which stay useful read-only.
const ORGANIZER_ONLY_WIDGET_KEYS: readonly WidgetKey[] = [
	"CreateOpportunity",
	"QuickCheckIn",
];

export default function OrgDashboardPage() {
	const { org, isOrganizer } = useOutletContext<OrgAppContext>();
	const { t } = useTranslation();
	const navigate = useNavigate();
	const api = useApiClient();
	const organizationId = org.id;
	const isLargeViewport = useIsLargeViewport();
	usePageTitle(`${t("orgOverview.tabDashboard")} - ${org.name}`);

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
	// Distinguishes "no custom layout exists" (the optimistic DEFAULT_LAYOUT
	// render above is genuinely accurate) from "the fetch to confirm that
	// failed" (#1234: it used to collapse both into the same silent
	// DEFAULT_LAYOUT fallback - indistinguishable from a real empty
	// customization, so a returning organizer hitting a transient backend
	// outage could edit and save over their actual saved layout without ever
	// being told theirs failed to load). Blocks entering edit mode (see
	// startEditing below) until a load actually succeeds.
	const [layoutLoadFailed, setLayoutLoadFailed] = useState(false);
	const [retryingLayoutLoad, setRetryingLayoutLoad] = useState(false);

	const placement = useWidgetPlacement({ draftLayout, setDraftLayout });

	const loadLayout = useCallback(() => {
		return api
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
				setLayoutLoadFailed(false);
			})
			.catch(() => setLayoutLoadFailed(true));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	useEffect(() => {
		void loadLayout();
	}, [loadLayout]);

	function retryLoadLayout() {
		setRetryingLayoutLoad(true);
		void loadLayout().finally(() => setRetryingLayoutLoad(false));
	}

	const rawLayout = editing ? (draftLayout ?? []) : savedLayout;
	const layout = isOrganizer
		? rawLayout
		: compactLayout(
				rawLayout.filter(
					(w) => !ORGANIZER_ONLY_WIDGET_KEYS.includes(w.widgetKey),
				),
			);
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
			placement.cancelPlacing();
		} catch (e) {
			dispatchToast("error", getApiErrorMessage(e, t("error.serverError")));
		} finally {
			setSaving(false);
		}
	}

	function handleCancel() {
		placement.cancelPlacing();
		setEditing(false);
		setDraftLayout(null);
	}

	function startEditing() {
		// Blocked while the true saved layout is unconfirmed (see
		// layoutLoadFailed above), or for a plain Member (read-only tier) -
		// the "Edit" quick action is already disabled for both, but the
		// empty-state CTA below calls this directly too.
		if (layoutLoadFailed || !isOrganizer) return;
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
		editDisabled: layoutLoadFailed || !isOrganizer,
		editDisabledTitle: !isOrganizer
			? t("orgDashboard.layoutEditDisabledNotOrganizerHint")
			: t("orgDashboard.layoutLoadError"),
		onEdit: startEditing,
		onSave: () => void handleSave(),
		onCancel: handleCancel,
		extraEditingActions,
	});

	function handleRemoveWidget(key: WidgetKey) {
		const wasPlacing = placement.placingKey === key;
		// #14: closes whatever gap removing this widget just left instead of
		// leaving the rest of the layout sitting where it was.
		setDraftLayout((prev) =>
			compactLayout((prev ?? []).filter((w) => w.widgetKey !== key)),
		);
		if (wasPlacing) {
			placement.cancelPlacing();
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

	function renderWidget(key: WidgetKey, size: WidgetSizeClass) {
		switch (key) {
			case "ToDo":
				return (
					<ToDoWidget
						organizationId={organizationId}
						refreshKey={refreshKey}
						size={size}
					/>
				);
			case "VolunteerStats":
				return (
					<VolunteerStatsWidget
						organizationId={organizationId}
						refreshKey={refreshKey}
						size={size}
					/>
				);
			case "UpcomingOpportunities":
				return (
					<UpcomingOpportunitiesWidget
						organizationId={organizationId}
						refreshKey={refreshKey}
						size={size}
						isOrganizer={isOrganizer}
						onOpportunityCreated={handleOpportunityCreated}
					/>
				);
			case "Calendar":
				return (
					<CalendarWidget
						organizationId={organizationId}
						refreshKey={refreshKey}
						size={size}
						isOrganizer={isOrganizer}
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
						onOpportunityCreated={handleOpportunityCreated}
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

	const { activeKey, previewRect, previewValid, placingKey, anchor, cursor } =
		placement;

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

	// #1402: up to 832 cells at GRID_MAX_ROWS, so handlers live once on the
	// grid container (see handleGuideCellClick/handleGuideCellPointerOver
	// below) reading data-col/data-row off the event target, rather than a
	// closure per cell. Memoized so an unrelated re-render (e.g. `saving`
	// toggling) doesn't recompute all 832 cells for no reason.
	const guideCells = useMemo(() => {
		if (!editing || !isLargeViewport) return null;
		return Array.from({ length: guideRows * GRID_COLUMNS }, (_, i) => {
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
					data-col={col}
					data-row={row}
					aria-hidden="true"
					className={`-m-1 rounded-md ${tint} ${placingKey ? "cursor-pointer" : "pointer-events-none"}`}
					style={{ gridColumn: col, gridRow: row }}
				/>
			);
		});
	}, [
		editing,
		isLargeViewport,
		guideRows,
		previewRect,
		previewValid,
		placingKey,
	]);

	// Delegated handlers for the grid-guide backdrop cells (see guideCells
	// above) - a single pair of listeners on the container instead of one
	// onClick/onPointerEnter per cell. Cells carry no handlers of their own;
	// pointer-events-none (see the className above) already keeps events from
	// reaching them at all while nothing is being placed, so both of these
	// are effectively no-ops outside an active placement even though they're
	// always wired up.
	function guideCellFromEvent(
		target: EventTarget | null,
	): { col: number; row: number } | null {
		const cellEl = (target as HTMLElement | null)?.closest<HTMLElement>(
			'[data-testid="dashboard-grid-guide-cell"]',
		);
		if (!cellEl) return null;
		return { col: Number(cellEl.dataset.col), row: Number(cellEl.dataset.row) };
	}

	function handleGuideCellClick(event: ReactMouseEvent<HTMLDivElement>) {
		const cell = guideCellFromEvent(event.target);
		if (cell) placement.handleCellClick(cell);
	}

	function handleGuideCellPointerOver(
		event: ReactPointerEvent<HTMLDivElement>,
	) {
		const cell = guideCellFromEvent(event.target);
		if (cell) placement.handleCellHover(cell);
	}

	const grid = isEmpty ? (
		<div data-testid="dashboard-empty-state">
			<EmptyState
				title={t("orgDashboard.emptyStateTitle")}
				message={t("orgDashboard.emptyStateMessage")}
				action={
					isOrganizer
						? {
								label: t("orgDashboard.addWidgetHeading"),
								onClick: handleStartEditingAndAddWidget,
							}
						: undefined
				}
			/>
		</div>
	) : (
		<div
			data-testid="dashboard-widget-grid"
			// Uniform (not minmax(64px, auto)) row height: CSS Grid auto-rows
			// apply to the whole row band across every column, not just the cell
			// whose content demanded the extra height - a minmax row would let a
			// single tall widget stretch its entire row, including the backdrop
			// guide cells and any other widget sharing that row. A widget whose
			// content exceeds its allotted rows scrolls internally instead (see
			// WidgetCard).
			//
			// The row height itself (see .dashboard-widget-grid in global.css)
			// tracks the actual rendered column width via a container query,
			// rather than a flat pixel constant - width already scales with the
			// viewport (grid-cols-8's 1fr tracks), and matching row height to it
			// keeps a widget's on-screen proportions (short-and-wide vs.
			// tall-and-narrow) consistent across screen sizes instead of warping
			// while its stored cell width/height stays the same.
			className="dashboard-widget-grid grid grid-cols-1 gap-4 lg:grid-cols-8"
			// role="presentation": this delegated onClick/onPointerOver only ever
			// acts on a bubbled event that actually originated on one of the
			// aria-hidden guide cells above (see guideCellFromEvent) - the
			// container's own "clickability" isn't a perceivable action in its
			// own right, and doesn't affect its real interactive descendants
			// (the widget tiles' own buttons keep their normal roles/focus).
			// Satisfies jsx-a11y/click-events-have-key-events and
			// jsx-a11y/no-static-element-interactions, which both exempt
			// presentation/hidden elements - the keyboard-accessible equivalent
			// of this same placement flow is the per-widget "Move or resize"
			// button + arrow keys (see useWidgetPlacement's handleArrowKeyDown).
			// Must be a literal string, not a conditional - both rules read it
			// via getLiteralPropValue and don't resolve a ternary.
			role="presentation"
			onClick={editing && isLargeViewport ? handleGuideCellClick : undefined}
			onPointerOver={
				editing && isLargeViewport ? handleGuideCellPointerOver : undefined
			}
		>
			{/* Light green cell backdrop behind the whole grid while editing, so
			an organizer can see the underlying 8-column structure. These cells
			double as the corner-to-corner placement surface: while a widget is
			being placed, they become clickable (see handleGuideCellClick above)
			and are tinted blue/red to preview whether the current selection is a
			valid placement. Gated on isLargeViewport since the grid itself
			collapses to a single stacked column below `lg`, where this wouldn't
			mean anything. */}
			{guideCells}
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
						onAdvance={() => placement.handleAdvance(widget.widgetKey)}
						onArrowKeyDown={(e) =>
							placement.handleArrowKeyDown(e, widget.widgetKey)
						}
						onRemove={() => handleRemoveWidget(widget.widgetKey)}
						onGripPointerDown={(e) =>
							placement.startDrag(e, widget.widgetKey, "move")
						}
						onResizePointerDown={(e) =>
							placement.startDrag(e, widget.widgetKey, "resize")
						}
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
						onClick={placement.cancelPlacing}
						className="shrink-0 rounded-lg px-2 py-1 text-xs font-semibold text-brand-800 hover:bg-brand-100"
					>
						{t("common.cancel")}
					</button>
				</div>
			)}

			{layoutLoadFailed && (
				<div className="mb-3 flex items-center gap-3">
					<ErrorBanner
						id="dashboard-layout-load-error"
						className="flex-1"
						message={t("orgDashboard.layoutLoadError")}
					/>
					{/* aria-describedby ties this to the error text above - its own
					accessible name ("Retry") says nothing about what it's retrying,
					and a screen-reader user tabbing to it after the banner's one-time
					aria-live announcement has already passed would otherwise hear
					just "Retry, button" with no context. */}
					<button
						type="button"
						onClick={retryLoadLayout}
						disabled={retryingLayoutLoad}
						aria-describedby="dashboard-layout-load-error"
						data-testid="dashboard-layout-retry"
						className="shrink-0 rounded-card bg-red-50 px-3 py-3 text-sm font-semibold text-red-700 hover:bg-red-100 disabled:opacity-50"
					>
						{retryingLayoutLoad ? t("common.retrying") : t("common.retry")}
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
