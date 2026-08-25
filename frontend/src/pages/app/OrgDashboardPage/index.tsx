import {
	useCallback,
	useEffect,
	useMemo,
	useState,
	type CSSProperties,
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
	groupIntoRowBands,
	placeNewWidget,
	sanitizeWidgetKey,
	sortByPosition,
	type PlacedWidget,
	type WidgetKey,
	type WidgetSizeClass,
} from "./widgetCatalog";

type BandGridStyle = CSSProperties & { "--dashboard-grid-columns": number };

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

	const [refreshKey, setRefreshKey] = useState(0);

	const [savedLayout, setSavedLayout] =
		useState<PlacedWidget[]>(DEFAULT_LAYOUT);
	const [editing, setEditing] = useState(false);
	const [draftLayout, setDraftLayout] = useState<PlacedWidget[] | null>(null);
	const [saving, setSaving] = useState(false);
	const [showAddWidgetModal, setShowAddWidgetModal] = useState(false);

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

	const mobileLayout = isLargeViewport ? layout : sortByPosition(layout);

	const rowBands = !editing && isLargeViewport ? groupIntoRowBands(layout) : [];
	const needsRowBanding = rowBands.some((band) => band.columns < GRID_COLUMNS);

	const handleOpportunityCreated = useCallback(
		(createdDraftId?: string) => {
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

		setShowAddWidgetModal(false);
	}

	function startEditing() {
		if (layoutLoadFailed || !isOrganizer) return;
		setDraftLayout(savedLayout);
		setEditing(true);
	}

	function handleStartEditingAndAddWidget() {
		if (!editing) startEditing();
		setShowAddWidgetModal(true);
	}

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

							disabled: saving,
						},
					]
				: [],
		[hasWidgetsToAdd, saving, t],
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

		setDraftLayout((prev) =>
			compactLayout((prev ?? []).filter((w) => w.widgetKey !== key)),
		);
		if (wasPlacing) {
			placement.cancelPlacing();

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

	const isEmpty = layout.length === 0;

	const { activeKey, previewRect, previewValid, placingKey, anchor, cursor } =
		placement;

	const contentRows = layout.length
		? Math.max(1, ...layout.map((w) => w.y + w.height - 1))
		: 1;
	const previewBottom = previewRect
		? previewRect.y + previewRect.height - 1
		: 0;

	const guidePadding = activeKey !== null ? 4 : 1;

	const rawGuideRows =
		Math.max(contentRows, cursor?.row ?? 0, previewBottom) + guidePadding;
	const guideRows = Number.isFinite(rawGuideRows)
		? Math.min(GRID_MAX_ROWS + 4, rawGuideRows)
		: GRID_MAX_ROWS + 4;

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

	function renderTile(widget: PlacedWidget, rowOffset = 0) {
		const isPlacingThis = activeKey === widget.widgetKey;
		const rect = isPlacingThis && previewRect ? previewRect : widget;
		const sizeClass = isLargeViewport ? classifyWidth(rect.width) : "compact";
		return (
			<EditableWidgetTile
				key={widget.widgetKey}
				widgetKey={widget.widgetKey}
				gridStyle={
					isLargeViewport
						? {
								gridColumn: `${rect.x} / span ${rect.width}`,
								gridRow: `${rect.y - rowOffset} / span ${rect.height}`,
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
	) : needsRowBanding ? (
		<div data-testid="dashboard-widget-grid" className="flex flex-col gap-4">
			{rowBands.map((band) => {
				const bandStyle: BandGridStyle = {
					gridTemplateColumns: `repeat(${band.columns}, minmax(0, 1fr))`,
					maxWidth: `${(band.columns / GRID_COLUMNS) * 100}%`,
					"--dashboard-grid-columns": band.columns,
				};
				return (
					<div
						key={band.startRow}
						className="dashboard-widget-grid grid gap-4"
						style={bandStyle}
					>
						{band.widgets.map((widget) =>
							renderTile(widget, band.startRow - 1),
						)}
					</div>
				);
			})}
			{isOrganizer && !layoutLoadFailed && (
				<div className="dashboard-widget-grid grid">
					<button
						type="button"
						data-testid="dashboard-customize-hint"
						onClick={startEditing}
						className="flex h-full items-center justify-center gap-1.5 rounded-md border border-dashed border-gray-200 text-sm text-gray-500 transition-colors hover:border-brand-300 hover:text-brand-600"
					>
						<PlusIcon />
						{t("orgDashboard.customizeHint")}
					</button>
				</div>
			)}
		</div>
	) : (
		<div
			data-testid="dashboard-widget-grid"

			className={`dashboard-widget-grid grid grid-cols-1 gap-4 lg:grid-cols-8 ${editing ? "dashboard-widget-grid--editing" : ""}`}

			role="presentation"
			onClick={editing && isLargeViewport ? handleGuideCellClick : undefined}
			onPointerOver={
				editing && isLargeViewport ? handleGuideCellPointerOver : undefined
			}
		>
			{guideCells}
			{mobileLayout.map((widget) => renderTile(widget))}

			{!editing && isLargeViewport && isOrganizer && !layoutLoadFailed && (
				<button
					type="button"
					data-testid="dashboard-customize-hint"
					onClick={startEditing}
					style={{
						gridColumn: `1 / span ${GRID_COLUMNS}`,
						gridRow: contentRows + 1,
					}}
					className="flex items-center justify-center gap-1.5 rounded-md border border-dashed border-gray-200 text-sm text-gray-500 transition-colors hover:border-brand-300 hover:text-brand-600"
				>
					<PlusIcon />
					{t("orgDashboard.customizeHint")}
				</button>
			)}
		</div>
	);

	const placingTitle = placingKey ? t(WIDGET_CATALOG[placingKey].titleKey) : "";

	return (
		<>
			{editing && isLargeViewport && placingKey && cursor && (
				<div className="sticky top-24 z-30 mb-3 flex items-center justify-between gap-3 rounded-lg bg-brand-50 px-4 py-2 text-sm text-brand-800 shadow-md">
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
