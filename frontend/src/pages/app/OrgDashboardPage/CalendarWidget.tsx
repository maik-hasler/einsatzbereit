import { memo, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { Calendar, dateFnsLocalizer } from "react-big-calendar";
import type { DateHeaderProps, View } from "react-big-calendar";
import { format, parse, startOfWeek, getDay } from "date-fns";
import { enGB, de } from "date-fns/locale";

import type { OrganizationCalendarEventDto } from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import { useScrollFade } from "../../../hooks/useScrollFade";
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

function defaultViewForSize(size: WidgetSizeClass): View {
	if (size === "compact") return "agenda";
	if (size === "medium") return "week";
	return "month";
}

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

	titleLang: string;
	start: Date;
	end: Date;
	opportunityId: string;
	color: string | undefined;
	bookedCount: number;
	maxParticipants: number | null;
}

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

const COLOR_INPUT_DEBOUNCE_MS = 100;

interface Props {
	organizationId: string;
	refreshKey: number;
	size: WidgetSizeClass;
	isOrganizer: boolean;
}

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
	const [agendaViewEl, setAgendaViewEl] = useState<HTMLDivElement | null>(null);

	useEffect(() => {
		const container = calendarContainerRef.current;
		if (!container) return;

		function syncRbcDom() {
			container
				?.querySelectorAll(
					'.rbc-month-view[role], .rbc-month-row[role], .rbc-row-content[role], .rbc-date-cell[role], .rbc-row.rbc-month-header[role], [role="columnheader"]',
				)
				.forEach((el) => {
					el.removeAttribute("role");
					el.removeAttribute("aria-sort");
				});

			const agendaView =
				container?.querySelector<HTMLDivElement>(".rbc-agenda-view") ?? null;
			setAgendaViewEl((prev) => (prev === agendaView ? prev : agendaView));
		}

		syncRbcDom();
		const observer = new MutationObserver(syncRbcDom);
		observer.observe(container, { childList: true, subtree: true });
		return () => observer.disconnect();
	}, []);

	const agendaViewRef = useMemo(
		() => ({ current: agendaViewEl }),
		[agendaViewEl],
	);
	const {
		canScrollStart: canScrollAgendaLeft,
		canScrollEnd: canScrollAgendaRight,
	} = useScrollFade(agendaViewRef, "x");

	const [calView, setCalView] = useState<View>(() => defaultViewForSize(size));
	const [calDate, setCalDate] = useState(new Date());

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

	const [hasCheckedInitialView, setHasCheckedInitialView] = useState(false);
	useEffect(() => {
		if (hasCheckedInitialView || calLoading) return;
		setHasCheckedInitialView(true);
		if (calView !== "agenda" && calEvents.length === 0) setCalView("agenda");
	}, [calLoading, calView, calEvents, hasCheckedInitialView]);

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

			{agendaViewEl &&
				createPortal(
					<>
						<div
							aria-hidden="true"
							data-testid="calendar-agenda-fade-left"
							className={`pointer-events-none absolute inset-y-0 left-0 w-8 bg-gradient-to-r from-white to-transparent transition-opacity duration-200 ${
								canScrollAgendaLeft ? "opacity-100" : "opacity-0"
							}`}
						/>
						<div
							aria-hidden="true"
							data-testid="calendar-agenda-fade-right"
							className={`pointer-events-none absolute inset-y-0 right-0 w-8 bg-gradient-to-l from-white to-transparent transition-opacity duration-200 ${
								canScrollAgendaRight ? "opacity-100" : "opacity-0"
							}`}
						/>
					</>,
					agendaViewEl,
				)}

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
