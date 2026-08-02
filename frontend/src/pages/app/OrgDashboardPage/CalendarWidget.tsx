import { memo, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { Calendar, dateFnsLocalizer } from "react-big-calendar";
import type { View } from "react-big-calendar";
import { format, parse, startOfWeek, getDay } from "date-fns";
import { enUS, de } from "date-fns/locale";
// react-big-calendar's own stylesheet is imported eagerly from main.tsx
// (before global.css), not here - see the comment there for why: this
// widget only exists on the lazy-loaded OrgDashboardPage chunk, and an
// import here would tie the library's default (unstyled, low-contrast)
// CSS to that chunk's load order relative to global.css's brand overrides
// for the same react-big-calendar classes, rather than a fixed, predictable
// order.
import type { OrganizationCalendarEventDto } from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import Modal from "../../../components/Modal";
import Spinner from "../../../components/Spinner";
import Button from "../../../components/Button";
import ErrorBanner from "../../../components/ErrorBanner";
import WidgetCard from "./WidgetCard";
import { useSharedOrgFetch } from "../../../hooks/useSharedOrgFetch";
import { visibleCalendarRange } from "../../../lib/calendarRange";
import { formatDateTime } from "../../../lib/format";
import type { WidgetSizeClass } from "./widgetCatalog";

// The *default* view a fresh mount opens on - a narrow tile can't usefully
// show a month grid, so it starts on the agenda list instead; all three
// stay switchable via the view buttons regardless of size (#771 follow-up
// review feedback - adaptive layouts per size).
function defaultViewForSize(size: WidgetSizeClass): View {
	if (size === "compact") return "agenda";
	if (size === "medium") return "week";
	return "month";
}

const rbcLocales = {
	"en-US": enUS,
	de,
};
const localizer = dateFnsLocalizer({
	format,
	parse,
	startOfWeek,
	getDay,
	locales: rbcLocales,
});

const DEFAULT_EVENT_COLOR = "#226947";

interface CalEvent {
	id: string;
	title: string;
	start: Date;
	end: Date;
	opportunityId: string;
	color: string | undefined;
	bookedCount: number;
	maxParticipants: number | null;
}

// react-big-calendar renders this inside a plain, non-focusable <div>/<td>
// (Month/Week/Day: EventCell.js's `.rbc-event`; Agenda: a bare
// `rbc-agenda-event-cell` <td>) - neither ever gets a tabIndex or role from
// the library itself, so a mouse click was previously the only way to reach
// the event dialog. Rendering a real <button> as the chip's own root element
// fixes every view at once (components.event is the one seam RBC threads
// through both EventCell and Agenda) without needing to fight the library's
// internal onKeyPress/eventWrapper plumbing, which - confirmed by reading
// node_modules/react-big-calendar/lib/{EventCell,Agenda,Calendar}.js -
// forwards onKeyPressEvent as a *separate*, unwired prop rather than
// invoking onSelectEvent on Enter/Space by default.
function CalEventChip({
	event,
	onActivate,
}: {
	event: object;
	onActivate: (event: object) => void;
}) {
	const { t, i18n } = useTranslation();
	const e = event as CalEvent;
	const timeRange = `${formatDateTime(e.start.toISOString(), i18n.language)} - ${formatDateTime(e.end.toISOString(), i18n.language)}`;
	const capacityLabel =
		e.maxParticipants === null
			? t("orgOverview.eventChipUnlimited", { booked: e.bookedCount })
			: e.maxParticipants > 0
				? `${e.bookedCount}/${e.maxParticipants}`
				: "";
	const ariaLabel = [e.title, timeRange, capacityLabel]
		.filter(Boolean)
		.join(", ");

	return (
		<button
			type="button"
			aria-label={ariaLabel}
			style={{ color: "inherit" }}
			className="flex h-full w-full items-center justify-between gap-1 overflow-hidden border-0 bg-transparent p-0 text-left"
			onClick={(evt) => {
				// The outer .rbc-event/rbc-agenda-event-cell that RBC wraps this
				// button in already has its own onClick wired to the same
				// onSelectEvent - without stopping propagation, a click here would
				// bubble up and invoke it a second time.
				evt.stopPropagation();
				onActivate(event);
			}}
		>
			<span className="truncate">{e.title}</span>
			{capacityLabel && (
				<span className="shrink-0 text-xs opacity-80">{capacityLabel}</span>
			)}
		</button>
	);
}

function calendarEventPropGetter(event: object) {
	const e = event as CalEvent;
	const bg = e.color ?? DEFAULT_EVENT_COLOR;
	return {
		style: {
			backgroundColor: bg,
			borderColor: bg,
			color: "#ffffff",
		},
	};
}

// The color picker's onChange fires continuously while the user drags across
// the native picker UI; debouncing avoids re-rendering the whole widget (and
// its Calendar subtree) on every pointer movement (#1397).
const COLOR_INPUT_DEBOUNCE_MS = 100;

interface Props {
	organizationId: string;
	refreshKey: number;
	size: WidgetSizeClass;
}

// The dashboard grid's row height isn't a flat pixel constant (it tracks
// the rendered column width, see .dashboard-widget-grid in global.css, so a
// widget's on-screen shape stays consistent across screen sizes) - mirroring
// that in a second, JS-side constant here would just be a new way for the
// two to drift apart again. Filling 100% of the widget's own card area (see
// WidgetCard) instead means the calendar always matches whatever height the
// organizer's placement actually rendered at, on any screen, with no
// separate row-to-pixel formula to keep in sync. MIN_HEIGHT is just a floor
// so a very short placement doesn't squash the calendar unusably small -
// WidgetCard's own overflow-y-auto handles the rest.
const CALENDAR_MIN_HEIGHT_PX = 400;

function CalendarWidget({ organizationId, refreshKey, size }: Props) {
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const calendarContainerRef = useRef<HTMLDivElement | null>(null);

	// Month/Week views wrap each week's strip of overlaid event chips in a
	// container with role="row" (node_modules/react-big-calendar/lib/
	// DateContentRow.js), but that container's only children are the
	// heading cells (Month only) and the event chips themselves - never a
	// cell/gridcell/columnheader, because it isn't a second row of the day
	// grid at all: it's a decorative overlay sitting on top of the real day
	// cells (BackgroundCells' .rbc-row-bg), which already carry their own
	// correct roles. axe-core's aria-required-children rule correctly flags
	// role="row" with no such children; each event chip already exposes its
	// own accessible <button> (CalEventChip below), so the fix is to drop
	// the incorrect role rather than fabricate fake gridcells. Unlike
	// .rbc-event (see calendarComponents below), RBC doesn't thread a
	// components seam through DateContentRow for this wrapper, so a
	// MutationObserver is the only way to correct it without patching the
	// library itself.
	useEffect(() => {
		const container = calendarContainerRef.current;
		if (!container) return;

		function stripInvalidRowRole() {
			container
				?.querySelectorAll('.rbc-row-content[role="row"]')
				.forEach((el) => el.setAttribute("role", "presentation"));
		}

		stripInvalidRowRole();
		const observer = new MutationObserver(stripInvalidRowRole);
		observer.observe(container, { childList: true, subtree: true });
		return () => observer.disconnect();
	}, []);

	// Lazy initializer - only the INITIAL view depends on size; once mounted,
	// the organizer's own view-button clicks take over and this doesn't
	// re-run just because a drag elsewhere changed this widget's span.
	const [calView, setCalView] = useState<View>(() => defaultViewForSize(size));
	const [calDate, setCalDate] = useState(new Date());

	// Bound to whatever's actually on screen (#1389) rather than fetching
	// every calendar event the org has ever created - an org with years of
	// weekly recurring shifts would otherwise pull thousands of slots on
	// every dashboard load. Recomputed whenever the visible window changes
	// (view switch or navigation), which naturally triggers a refetch since
	// it's part of useSharedOrgFetch's key.
	const { from: rangeFrom, to: rangeTo } = useMemo(
		() => visibleCalendarRange(calDate, calView),
		[calDate, calView],
	);
	const [calData, setCalData, calError] = useSharedOrgFetch<
		OrganizationCalendarEventDto[]
	>(
		`calendarEvents:${organizationId}:${refreshKey}:${rangeFrom.toISOString()}:${rangeTo.toISOString()}`,
		() => api.getOrganizationCalendarEvents(organizationId, rangeFrom, rangeTo),
	);
	const calLoading = calData === null && !calError;
	const [selectedEvent, setSelectedEvent] = useState<CalEvent | null>(null);
	const [pickerColor, setPickerColor] = useState(DEFAULT_EVENT_COLOR);
	const [savingColor, setSavingColor] = useState(false);
	const [colorSaveError, setColorSaveError] = useState<string | null>(null);
	const colorDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

	useEffect(() => {
		return () => {
			if (colorDebounceRef.current) clearTimeout(colorDebounceRef.current);
		};
	}, []);

	const calEvents: CalEvent[] = useMemo(
		() =>
			(calData ?? []).flatMap((opp) =>
				opp.timeSlots.map((slot) => ({
					id: slot.timeSlotId,
					title: opp.title,
					start: new Date(slot.startDateTime),
					end: new Date(slot.endDateTime),
					opportunityId: opp.opportunityId,
					color: opp.color,
					bookedCount: slot.bookedCount,
					maxParticipants: slot.maxParticipants ?? null,
				})),
			),
		[calData],
	);

	const calendarMessages = useMemo(
		() => ({
			today: t("orgOverview.calendarToday"),
			previous: t("orgOverview.calendarBack"),
			next: t("orgOverview.calendarNext"),
			month: t("orgOverview.calendarMonth"),
			week: t("orgOverview.calendarWeek"),
			work_week: t("orgOverview.calendarWorkWeek"),
			day: t("orgOverview.calendarDay"),
			agenda: t("orgOverview.calendarAgenda"),
			noEventsInRange: t("orgOverview.calendarNoEvents"),
		}),
		[t],
	);

	const handleSelectEvent = useCallback((event: object) => {
		const e = event as CalEvent;
		if (colorDebounceRef.current) clearTimeout(colorDebounceRef.current);
		setSelectedEvent(e);
		setPickerColor(e.color ?? DEFAULT_EVENT_COLOR);
		setColorSaveError(null);
	}, []);

	// Only recomputed when handleSelectEvent's identity changes (never, since
	// it has an empty dep array) - keeps the same stable-object-reference
	// property the previous module-scope `calendarComponents` constant had
	// (see #1397), while still being able to close over a per-instance
	// handler now that CalEventChip needs one for keyboard activation.
	const calendarComponents = useMemo(
		() => ({
			event: (props: { event: object }) => (
				<CalEventChip {...props} onActivate={handleSelectEvent} />
			),
		}),
		[handleSelectEvent],
	);

	function handleColorPickerChange(value: string) {
		if (colorDebounceRef.current) clearTimeout(colorDebounceRef.current);
		colorDebounceRef.current = setTimeout(() => {
			setPickerColor(value);
		}, COLOR_INPUT_DEBOUNCE_MS);
	}

	async function handleColorSave() {
		if (!selectedEvent) return;
		setSavingColor(true);
		setColorSaveError(null);
		try {
			await api.setOpportunityColor(selectedEvent.opportunityId, {
				color: pickerColor || undefined,
			});
			setCalData((prev) =>
				(prev ?? []).map((opp) =>
					opp.opportunityId === selectedEvent.opportunityId
						? { ...opp, color: pickerColor || undefined }
						: opp,
				),
			);
			setSelectedEvent(null);
		} catch {
			setColorSaveError(t("orgOverview.colorSaveError"));
		} finally {
			setSavingColor(false);
		}
	}

	return (
		<WidgetCard
			titleId="widget-calendar-title"
			title={t("orgDashboard.calendarWidgetTitle")}
		>
			{/* display:contents - a purely structural wrapper (matches
			MiniCalendar.tsx's own use of the same pattern) so the
			MutationObserver above has a stable node to attach to regardless of
			calLoading/calError, without affecting layout: the ref used to sit
			directly on the calendar's own div below, which doesn't exist yet on
			first mount while calLoading is still true - the effect's one-time
			[] run found a null ref and never got a second chance to attach. */}
			<div ref={calendarContainerRef} className="contents">
				{calLoading && (
					<div className="flex items-center justify-center py-16">
						<Spinner label={t("orgOverview.calendarLoading")} />
					</div>
				)}
				{calError && (
					<ErrorBanner
						message={t("orgOverview.calendarError", { message: calError })}
					/>
				)}
				{!calLoading && !calError && (
					<div className="rbc-container h-full">
						<Calendar
							localizer={localizer}
							culture={i18n.language === "de" ? "de" : "en-US"}
							events={calEvents}
							view={calView}
							onView={(v: View) => setCalView(v)}
							date={calDate}
							onNavigate={(d: Date) => setCalDate(d)}
							views={["month", "week", "work_week", "day", "agenda"]}
							style={{ height: "100%", minHeight: CALENDAR_MIN_HEIGHT_PX }}
							components={calendarComponents}
							eventPropGetter={calendarEventPropGetter}
							onSelectEvent={handleSelectEvent}
							messages={calendarMessages}
						/>
					</div>
				)}
			</div>

			{selectedEvent && (
				<Modal
					onClose={() => setSelectedEvent(null)}
					labelledBy="color-dialog-title"
					maxWidth="max-w-xs"
					className="rounded-card bg-white p-6 shadow-modal"
				>
					<h3
						id="color-dialog-title"
						className="mb-4 text-lg font-semibold text-gray-900"
					>
						{selectedEvent.title}
					</h3>
					<div className="space-y-4">
						{selectedEvent.maxParticipants === null ? (
							<p className="text-sm text-gray-600">
								{t("orgOverview.eventFillStateUnlimited", {
									booked: selectedEvent.bookedCount,
								})}
							</p>
						) : (
							selectedEvent.maxParticipants > 0 && (
								<p className="text-sm text-gray-600">
									{t("orgOverview.eventFillState", {
										booked: selectedEvent.bookedCount,
										max: selectedEvent.maxParticipants,
									})}
								</p>
							)
						)}
						<div>
							<label
								htmlFor="event-color-picker"
								className="block text-sm font-medium text-gray-700"
							>
								{t("orgOverview.eventColorLabel")}
							</label>
							<div className="mt-1 flex items-center gap-3">
								<input
									id="event-color-picker"
									type="color"
									value={pickerColor}
									onChange={(e) => handleColorPickerChange(e.target.value)}
									className="h-9 w-16 cursor-pointer rounded border border-gray-300"
								/>
								<span className="text-sm text-gray-500">{pickerColor}</span>
							</div>
						</div>
						{colorSaveError && <ErrorBanner message={colorSaveError} />}
						<div className="flex flex-col gap-2">
							<div className="flex gap-4">
								<Link
									to={`/volunteer-opportunities/${selectedEvent.opportunityId}`}
									className="text-sm text-brand-700 hover:underline"
									onClick={() => setSelectedEvent(null)}
								>
									{t("orgOverview.eventNavigate")}
								</Link>
								<Link
									to={`/app/${organizationId}/dashboard/opportunities/${selectedEvent.opportunityId}/engagements`}
									className="text-sm text-brand-700 hover:underline"
									onClick={() => setSelectedEvent(null)}
								>
									{t("orgOverview.eventManageApplications")}
								</Link>
							</div>
							<div className="flex justify-end gap-2">
								<button
									type="button"
									onClick={() => setSelectedEvent(null)}
									className="rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
								>
									{t("createOpportunity.cancel")}
								</button>
								<Button
									type="button"
									disabled={savingColor}
									onClick={handleColorSave}
									size="sm"
								>
									{savingColor
										? t("orgOverview.eventColorSaving")
										: t("orgOverview.eventColorSave")}
								</Button>
							</div>
						</div>
					</div>
				</Modal>
			)}
		</WidgetCard>
	);
}

export default memo(CalendarWidget);
