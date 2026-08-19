import { memo, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { Calendar, dateFnsLocalizer } from "react-big-calendar";
import type { DateHeaderProps, View } from "react-big-calendar";
import { format, parse, startOfWeek, getDay } from "date-fns";
import { enGB, de } from "date-fns/locale";
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
import Skeleton from "../../../components/Skeleton";
import Button from "../../../components/Button";
import ErrorBanner from "../../../components/ErrorBanner";
import WidgetCard from "./WidgetCard";
import { useSharedOrgFetch } from "../../../hooks/useSharedOrgFetch";
import { visibleCalendarRange } from "../../../lib/calendarRange";
import {
	formatDate,
	formatDateTimeRange,
	formatFullDate,
	pickLocalizedText,
	resolveDateLocale,
} from "../../../lib/format";
import { brandColor } from "../../../lib/brandColor";
import {
	readableTextColor,
	meetsTextContrastFloor,
} from "../../../lib/colorContrast";
import { getApiErrorMessage } from "../../../lib/apiError";
import { labelClass } from "../../../lib/formClasses";
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

// Keyed by resolveDateLocale's output (lib/format.ts's single source of
// truth for i18n.language -> Intl/date-fns locale, #1267) rather than a
// second, independently-drifting en-US/de ternary here (#1731).
const rbcLocales = {
	"en-GB": enGB,
	"de-DE": de,
};
const localizer = dateFnsLocalizer({
	format,
	parse,
	startOfWeek,
	getDay,
	locales: rbcLocales,
});

interface CalEvent {
	id: string;
	title: string;
	/** `title`'s actual language, when it may differ from the active UI
	 * language - e.g. a German-only opportunity title (einsatzbereit#2057). */
	titleLang: string;
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
	const timeRange = formatDateTimeRange(
		e.start.toISOString(),
		e.end.toISOString(),
		i18n.language,
	);
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
			<span lang={e.titleLang} className="truncate">
				{e.title}
			</span>
			{capacityLabel && (
				<span className="shrink-0 text-xs">{capacityLabel}</span>
			)}
		</button>
	);
}

// react-big-calendar's default DateHeader (lib/DateHeader.js) renders each
// month-view day cell as a bare `<button>{label}</button>` with only the
// numeric day ("27") and no aria-label - identical text repeats up to five
// times a month, leaving a screen-reader user with no way to tell which "27"
// they're on (einsatzbereit#1924/#1925 - filed twice for the same gap).
// Mirrors MiniCalendar.tsx's own aria-label recipe (full weekday/day/month/
// year via the shared formatFullDate helper, plus "in the past" for a past
// date) rather than inventing a second phrasing for the same concept, and
// mirrors the library default's span-vs-button split (span when RBC reports
// no drilldown view to navigate to, otherwise the same rbc-button-link
// button) so a day cell never exposes a control with no real action.
function CalDateHeader({
	label,
	date,
	drilldownView,
	onDrillDown,
}: DateHeaderProps) {
	const { t, i18n } = useTranslation();
	const todayMidnight = new Date();
	todayMidnight.setHours(0, 0, 0, 0);
	const isPast = date.getTime() < todayMidnight.getTime();
	const isToday = date.getTime() === todayMidnight.getTime();
	const fullDate = formatFullDate(date, i18n.language);
	const ariaLabel = isPast
		? `${fullDate}, ${t("opportunities.dayInPast")}`
		: fullDate;

	if (!drilldownView) {
		return <span aria-label={ariaLabel}>{label}</span>;
	}
	return (
		<button
			type="button"
			className="rbc-button-link"
			aria-label={ariaLabel}
			aria-current={isToday ? "date" : undefined}
			onClick={onDrillDown}
		>
			{label}
		</button>
	);
}

function calendarEventPropGetter(event: object) {
	const e = event as CalEvent;
	const bg = e.color ?? brandColor("700");
	return {
		style: {
			backgroundColor: bg,
			borderColor: bg,
			color: readableTextColor(bg),
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
	isOrganizer: boolean;
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

function CalendarWidget({
	organizationId,
	refreshKey,
	size,
	isOrganizer,
}: Props) {
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const calendarContainerRef = useRef<HTMLDivElement | null>(null);

	// Month view (node_modules/react-big-calendar/lib/Month.js and
	// DateContentRow.js) marks up each week as an ARIA table row/cell
	// structure - .rbc-month-view[role=table] > .rbc-month-row[role=rowgroup]
	// > .rbc-row-content[role=row] > (a role-less .rbc-row containing the
	// real .rbc-date-cell[role=cell] date-number buttons, *and* a sibling
	// overlay of event-chip rows) - but it's incomplete/self-contradictory:
	// .rbc-row-content[role=row]'s own direct children are never
	// cell/gridcell (axe-core's aria-required-children rule correctly flags
	// this), and no combination of patching individual roles resolves it
	// cleanly - verified empirically by replaying this exact DOM structure
	// through axe-core directly (bypassing the Docker/Aspire-dependent
	// Playwright suite, which isn't runnable in this sandbox): every element
	// with a table-structural role requires ALL of its focusable
	// descendants to be reachable through a valid cell/gridcell, but the
	// event-chip buttons genuinely have no cell wrapper at all - they're a
	// decorative overlay, not a second row of the same table - so nothing
	// short of removing the roles entirely satisfies every check at once.
	// Each date-number and event-chip button is already its own accessible
	// <button> (CalEventChip below handles the event chips) with no
	// dependency on an ancestor's ARIA role, so the table/row/cell/
	// columnheader roles were purely decorative scaffolding to begin with -
	// removing them (confirmed via the same axe-core replay to leave zero
	// violations) loses no actual assistive-tech affordance. RBC doesn't
	// expose a components seam for any of this scaffolding, so a
	// MutationObserver is the only way to correct it without patching the
	// library itself. Scoped to Month-view-only class names/attributes
	// (`.rbc-date-cell`/`renderHeader` don't exist in Week/Day/Agenda's
	// DOM - confirmed via node_modules/react-big-calendar/lib/TimeGrid.js),
	// so this can't affect any other view.
	useEffect(() => {
		const container = calendarContainerRef.current;
		if (!container) return;

		function stripMonthTableRoles() {
			container
				?.querySelectorAll(
					'.rbc-month-view[role], .rbc-month-row[role], .rbc-row-content[role], .rbc-date-cell[role], .rbc-row.rbc-month-header[role], [role="columnheader"]',
				)
				.forEach((el) => {
					el.removeAttribute("role");
					el.removeAttribute("aria-sort");
				});
		}

		stripMonthTableRoles();
		const observer = new MutationObserver(stripMonthTableRoles);
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

	// The fetch key above changes on every view switch and every step forward
	// or back through the calendar, and useSharedOrgFetch clears its data on a
	// key change - so a plain `calData === null` loading flag tore the whole
	// <Calendar> out of the tree and replaced it with a skeleton on every
	// single navigation. That unmounted the toolbar button the organizer had
	// just clicked (dropping keyboard focus to <body>, so "next month, next
	// month" was impossible without a mouse) and made stepping through months
	// flash the widget empty each time. Keeping the last loaded range on screen
	// while the next one is in flight - stale-while-revalidate, marked with
	// aria-busy - keeps the calendar mounted, so focus stays where it was and
	// only the events swap. The skeleton is now what it says it is: a
	// first-load placeholder.
	const lastLoadedRef = useRef<OrganizationCalendarEventDto[] | null>(null);
	useEffect(() => {
		if (calData !== null) lastLoadedRef.current = calData;
	}, [calData]);
	const displayedCalData = calData ?? lastLoadedRef.current;
	const calLoading = displayedCalData === null && !calError;
	const calRefreshing = calData === null && displayedCalData !== null;

	const [selectedEvent, setSelectedEvent] = useState<CalEvent | null>(null);
	const [pickerColor, setPickerColor] = useState<string>(() =>
		brandColor("700"),
	);
	const pickerColorContrastOk = meetsTextContrastFloor(pickerColor);
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
			(displayedCalData ?? []).flatMap((opp) => {
				const title = pickLocalizedText(
					opp.titleDe,
					opp.titleEn,
					i18n.language,
				);
				return opp.timeSlots.map((slot) => ({
					id: slot.timeSlotId,
					title: title.text,
					titleLang: title.lang,
					start: new Date(slot.startDateTime),
					end: new Date(slot.endDateTime),
					opportunityId: opp.opportunityId,
					color: opp.color,
					bookedCount: slot.bookedCount,
					maxParticipants: slot.maxParticipants ?? null,
				}));
			}),
		[displayedCalData, i18n.language],
	);

	// #983/#2045: opening on the current month (or week) whenever the
	// organizer happens to have no events scheduled in it wastes hundreds of
	// px on an empty grid and, since `calDate` also starts on today, defaults
	// to showing exactly nothing - including week view, which #2045 reported
	// scrolled to a completely empty night-hours grid because the org's only
	// opportunity fell outside that week. Agenda has no such "which
	// week/month" problem - it just lists whatever's actually upcoming - so
	// any grid view that opens empty falls back to it. The one-shot flag is
	// consumed on the *first* load to complete, regardless of which view
	// that happens to be (medium/compact widgets default to week/agenda, not
	// month) - gating it behind a specific view instead would leave the flag
	// unconsumed for those sizes and keep re-firing on every later view
	// change, silently overriding a view the organizer switched to
	// deliberately.
	const [hasCheckedInitialView, setHasCheckedInitialView] = useState(false);
	useEffect(() => {
		if (hasCheckedInitialView || calLoading) return;
		setHasCheckedInitialView(true);
		if (calView !== "agenda" && calEvents.length === 0) setCalView("agenda");
	}, [calLoading, calView, calEvents, hasCheckedInitialView]);

	// react-big-calendar defaults an unset scrollToTime to midnight for week/
	// day views (confirmed in node_modules/react-big-calendar/lib/{Week,Day}.js)
	// - opening scrolled to a wall of empty night hours instead of the day's
	// actual business hours is exactly #2045's reported bug. Falls back to
	// the earliest event actually on screen when there is one, so a shift
	// starting before 8am is still visible without an extra scroll.
	const scrollToTime = useMemo(() => {
		const eventHours = calEvents.map((e) => e.start.getHours());
		const startHour = eventHours.length > 0 ? Math.min(8, ...eventHours) : 8;
		const scrollDate = new Date(calDate);
		scrollDate.setHours(startHour, 0, 0, 0);
		return scrollDate;
	}, [calEvents, calDate]);

	const calendarMessages = useMemo(
		() => ({
			today: t("orgOverview.calendarToday"),
			previous: t("orgOverview.calendarBack"),
			next: t("orgOverview.calendarNext"),
			month: t("orgOverview.calendarMonth"),
			week: t("orgOverview.calendarWeek"),
			day: t("orgOverview.calendarDay"),
			agenda: t("orgOverview.calendarAgenda"),
			noEventsInRange: t("orgOverview.calendarNoEvents"),
			date: t("orgOverview.calendarDate"),
			time: t("orgOverview.calendarTime"),
			event: t("orgOverview.calendarEvent"),
			allDay: t("orgOverview.calendarAllDay"),
			yesterday: t("orgOverview.calendarYesterday"),
			tomorrow: t("orgOverview.calendarTomorrow"),
			showMore: (total: number) =>
				t("orgOverview.calendarShowMore", { count: total }),
		}),
		[t],
	);

	// react-big-calendar's own dayRangeHeaderFormat/agendaHeaderFormat defaults
	// (see localizers/date-fns.js) render the week/agenda toolbar's date-range
	// label via date-fns' locale-default 'P' token - raw, ambiguous DD/MM/YYYY
	// for en-GB, never routed through the site's shared formatDate/formatDateTime
	// helpers that every other date on the site (including this same widget's
	// own event chips, CalEventChip above) goes through (#1959). Both format
	// keys receive a {start, end} range and are keyed off i18n.language (not
	// the `culture` arg RBC would otherwise pass in), so this has to be a
	// per-render formatter rather than a module-scope constant like
	// `localizer` above.
	const calendarFormats = useMemo(() => {
		const formatHeaderRange = ({ start, end }: { start: Date; end: Date }) =>
			`${formatDate(start.toISOString(), i18n.language)} - ${formatDate(end.toISOString(), i18n.language)}`;
		return {
			dayRangeHeaderFormat: formatHeaderRange,
			agendaHeaderFormat: formatHeaderRange,
		};
	}, [i18n.language]);

	const handleSelectEvent = useCallback((event: object) => {
		const e = event as CalEvent;
		if (colorDebounceRef.current) clearTimeout(colorDebounceRef.current);
		setSelectedEvent(e);
		setPickerColor(e.color ?? brandColor("700"));
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
			month: { dateHeader: CalDateHeader },
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
		} catch (err) {
			setColorSaveError(
				getApiErrorMessage(err, t("orgOverview.colorSaveError")),
			);
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
					<div role="status" className="space-y-3">
						<span className="sr-only">{t("orgOverview.calendarLoading")}</span>
						<div
							aria-hidden="true"
							className="flex items-center justify-between gap-2"
						>
							<Skeleton className="h-6 w-32" />
							<Skeleton className="h-6 w-24" />
						</div>
						{/* #2045: matches the shape of whatever view is actually about
						to render instead of always the same short list - agenda really
						is a handful of rows, but week/month resolve to a real day/week
						grid, and a short-list skeleton for those swapped for a much
						taller grid the instant data arrived, jumping the page under the
						organizer. */}
						{calView === "agenda" ? (
							<div aria-hidden="true" className="space-y-2">
								{Array.from({ length: 3 }).map((_, i) => (
									<Skeleton key={i} className="h-10 w-full" />
								))}
							</div>
						) : (
							<div
								aria-hidden="true"
								className={`grid gap-1 ${calView === "month" ? "grid-cols-7" : "grid-cols-1"}`}
							>
								{Array.from({
									length: calView === "month" ? 35 : calView === "week" ? 7 : 1,
								}).map((_, i) => (
									<Skeleton key={i} className="h-16 w-full" />
								))}
							</div>
						)}
					</div>
				)}
				{calError && (
					<ErrorBanner
						message={t("orgOverview.calendarError", { message: calError })}
					/>
				)}
				{!calLoading && !calError && (
					// aria-busy while the next range is being fetched, so a screen
					// reader isn't told the events on screen are the ones it just
					// navigated to before they actually are.
					<div
						aria-busy={calRefreshing || undefined}
						className={`rbc-container h-full transition-opacity ${calRefreshing ? "opacity-60" : ""}`}
					>
						<Calendar
							localizer={localizer}
							culture={resolveDateLocale(i18n.language)}
							events={calEvents}
							view={calView}
							onView={(v: View) => setCalView(v)}
							date={calDate}
							onNavigate={(d: Date) => setCalDate(d)}
							views={["month", "week", "day", "agenda"]}
							scrollToTime={scrollToTime}
							style={{ height: "100%", minHeight: CALENDAR_MIN_HEIGHT_PX }}
							components={calendarComponents}
							eventPropGetter={calendarEventPropGetter}
							onSelectEvent={handleSelectEvent}
							messages={calendarMessages}
							formats={calendarFormats}
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
						lang={selectedEvent.titleLang}
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
						{isOrganizer && (
							<div>
								<label htmlFor="event-color-picker" className={labelClass}>
									{t("orgOverview.eventColorLabel")}
								</label>
								<div className="mt-1 flex items-center gap-3">
									<input
										id="event-color-picker"
										type="color"
										value={pickerColor}
										onChange={(e) => handleColorPickerChange(e.target.value)}
										aria-invalid={pickerColorContrastOk ? undefined : true}
										aria-describedby={
											pickerColorContrastOk
												? undefined
												: "event-color-contrast-warning"
										}
										className="h-9 w-16 cursor-pointer rounded border border-gray-300"
									/>
									<span className="text-sm text-gray-500">{pickerColor}</span>
								</div>
								{/* Mirrors the backend's MinimumTextContrastRatio gate
								(EventColorContrast.cs) client-side so the organizer sees
								why a color is unreadable before the round trip to the
								API rejects it with the same message (einsatzbereit#1726). */}
								{!pickerColorContrastOk && (
									<p
										id="event-color-contrast-warning"
										className="mt-1 text-sm text-red-700"
									>
										{t("orgOverview.eventColorContrastWarning")}
									</p>
								)}
							</div>
						)}
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
								{isOrganizer && (
									<Button
										type="button"
										disabled={savingColor || !pickerColorContrastOk}
										onClick={handleColorSave}
										size="sm"
									>
										{savingColor
											? t("orgOverview.eventColorSaving")
											: t("orgOverview.eventColorSave")}
									</Button>
								)}
							</div>
						</div>
					</div>
				</Modal>
			)}
		</WidgetCard>
	);
}

export default memo(CalendarWidget);
