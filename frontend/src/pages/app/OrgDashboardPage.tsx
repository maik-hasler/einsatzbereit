import { useEffect, useState } from "react";
import { Link, useNavigate, useOutletContext } from "react-router";
import { useTranslation } from "react-i18next";
import { Calendar, dateFnsLocalizer } from "react-big-calendar";
import type { View } from "react-big-calendar";
import { format, parse, startOfWeek, getDay } from "date-fns";
import { enUS, de } from "date-fns/locale";
import "react-big-calendar/lib/css/react-big-calendar.css";
import type { OrganizationCalendarEventDto } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import CreateVolunteerOpportunityModal from "../../components/CreateVolunteerOpportunityModal";
import type { OrgAppContext } from "../../layouts/OrgAppLayout";

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
	maxParticipants: number;
}

function CalEventChip({ event }: { event: object }) {
	const e = event as CalEvent;
	return (
		<span className="flex items-center justify-between gap-1 overflow-hidden">
			<span className="truncate">{e.title}</span>
			{e.maxParticipants > 0 && (
				<span className="shrink-0 text-xs opacity-80">
					{e.bookedCount}/{e.maxParticipants}
				</span>
			)}
		</span>
	);
}

export default function OrgDashboardPage() {
	const { org } = useOutletContext<OrgAppContext>();
	const { t } = useTranslation();
	const api = useApiClient();
	const navigate = useNavigate();
	const organizationId = org.id;

	const [showCreateModal, setShowCreateModal] = useState(false);

	const [calData, setCalData] = useState<OrganizationCalendarEventDto[]>([]);
	const [calLoading, setCalLoading] = useState(true);
	const [calError, setCalError] = useState<string | null>(null);
	const [calView, setCalView] = useState<View>("month");
	const [calDate, setCalDate] = useState(new Date());
	const [selectedEvent, setSelectedEvent] = useState<CalEvent | null>(null);
	const [pickerColor, setPickerColor] = useState(DEFAULT_EVENT_COLOR);
	const [savingColor, setSavingColor] = useState(false);
	const [colorSaveError, setColorSaveError] = useState<string | null>(null);

	useEffect(() => {
		if (!selectedEvent) return;
		const handleKey = (e: KeyboardEvent) => {
			if (e.key === "Escape") setSelectedEvent(null);
		};
		document.addEventListener("keydown", handleKey);
		return () => document.removeEventListener("keydown", handleKey);
	}, [selectedEvent]);

	function loadCalendarEvents() {
		setCalLoading(true);
		setCalError(null);
		api
			.getOrganizationCalendarEvents(organizationId)
			.then(setCalData)
			.catch((e: unknown) =>
				setCalError(e instanceof Error ? e.message : String(e)),
			)
			.finally(() => setCalLoading(false));
	}

	useEffect(() => {
		loadCalendarEvents();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	const calEvents: CalEvent[] = calData.flatMap((opp) =>
		opp.timeSlots.map((slot) => ({
			id: slot.timeSlotId,
			title: opp.title,
			start: new Date(slot.startDateTime),
			end: new Date(slot.endDateTime),
			opportunityId: opp.opportunityId,
			color: opp.color,
			bookedCount: slot.bookedCount,
			maxParticipants: slot.maxParticipants,
		})),
	);

	function handleSelectEvent(event: CalEvent) {
		setSelectedEvent(event);
		setPickerColor(event.color ?? DEFAULT_EVENT_COLOR);
		setColorSaveError(null);
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
				prev.map((opp) =>
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

	function handleOpportunityCreated(createdDraftId?: string) {
		loadCalendarEvents();
		// Drafts live on the Opportunities tab now. When one is saved from here,
		// take the organizer there with the new draft highlighted so it is never
		// lost (issue #708). A published opportunity just refreshes the calendar.
		if (createdDraftId) {
			navigate(
				`/app/${organizationId}/opportunities?highlight=${createdDraftId}`,
			);
		}
	}

	return (
		<div>
			<div className="mb-6 flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
				<h1 className="w-full min-w-0 break-words text-2xl font-bold text-gray-900 sm:w-auto">
					{org.name}
				</h1>
				<button
					type="button"
					onClick={() => setShowCreateModal(true)}
					data-testid="create-opportunity-btn"
					className="inline-flex w-full shrink-0 items-center justify-center gap-1.5 rounded-xl bg-brand-700 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-brand-800 focus:outline-none sm:w-auto"
				>
					{t("orgOverview.createOpportunity")}
				</button>
			</div>

			{calLoading && (
				<div className="flex items-center justify-center py-16">
					<span className="text-gray-500">
						{t("orgOverview.calendarLoading")}
					</span>
				</div>
			)}
			{calError && (
				<p className="text-red-600">
					{t("orgOverview.calendarError", { message: calError })}
				</p>
			)}
			{!calLoading && !calError && (
				<div className="rbc-container">
					<Calendar
						localizer={localizer}
						events={calEvents}
						view={calView}
						onView={(v: View) => setCalView(v)}
						date={calDate}
						onNavigate={(d: Date) => setCalDate(d)}
						views={["month", "week", "work_week", "day"]}
						style={{ height: 600 }}
						components={{ event: CalEventChip }}
						eventPropGetter={(event: object) => {
							const e = event as CalEvent;
							const bg = e.color ?? DEFAULT_EVENT_COLOR;
							return {
								style: {
									backgroundColor: bg,
									borderColor: bg,
									color: "#ffffff",
								},
							};
						}}
						onSelectEvent={(event: object) =>
							handleSelectEvent(event as CalEvent)
						}
						messages={{
							today: t("orgOverview.calendarToday"),
							previous: t("orgOverview.calendarBack"),
							next: t("orgOverview.calendarNext"),
							month: t("orgOverview.calendarMonth"),
							week: t("orgOverview.calendarWeek"),
							work_week: t("orgOverview.calendarWorkWeek"),
							day: t("orgOverview.calendarDay"),
							noEventsInRange: t("orgOverview.calendarNoEvents"),
						}}
					/>
				</div>
			)}

			{selectedEvent && (
				<div className="fixed inset-0 z-[2000] flex items-center justify-center">
					<button
						type="button"
						aria-hidden="true"
						tabIndex={-1}
						className="absolute inset-0 bg-black/50"
						onClick={() => setSelectedEvent(null)}
					/>
					<div
						role="dialog"
						aria-modal="true"
						aria-labelledby="color-dialog-title"
						className="relative z-10 w-80 rounded-xl bg-white p-6 shadow-xl"
					>
						<h2
							id="color-dialog-title"
							className="mb-4 text-lg font-semibold text-gray-900"
						>
							{selectedEvent.title}
						</h2>
						<div className="space-y-4">
							{selectedEvent.maxParticipants > 0 && (
								<p className="text-sm text-gray-600">
									{t("orgOverview.eventFillState", {
										booked: selectedEvent.bookedCount,
										max: selectedEvent.maxParticipants,
									})}
								</p>
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
										onChange={(e) => setPickerColor(e.target.value)}
										className="h-9 w-16 cursor-pointer rounded border border-gray-300"
									/>
									<span className="text-sm text-gray-500">{pickerColor}</span>
								</div>
							</div>
							{colorSaveError && (
								<p className="text-sm text-red-600">{colorSaveError}</p>
							)}
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
										to={`/app/${organizationId}/opportunities/${selectedEvent.opportunityId}/engagements`}
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
									<button
										type="button"
										disabled={savingColor}
										onClick={handleColorSave}
										className="rounded-md bg-brand-700 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-800 disabled:opacity-50"
									>
										{savingColor
											? t("orgOverview.eventColorSaving")
											: t("orgOverview.eventColorSave")}
									</button>
								</div>
							</div>
						</div>
					</div>
				</div>
			)}

			{showCreateModal && (
				<CreateVolunteerOpportunityModal
					organizationId={organizationId}
					onClose={() => setShowCreateModal(false)}
					onSuccess={handleOpportunityCreated}
				/>
			)}
		</div>
	);
}
