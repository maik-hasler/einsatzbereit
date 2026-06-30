import { useEffect, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { Calendar, dateFnsLocalizer } from "react-big-calendar";
import type { View } from "react-big-calendar";
import { format, parse, startOfWeek, getDay } from "date-fns";
import { enUS, de } from "date-fns/locale";
import "react-big-calendar/lib/css/react-big-calendar.css";
import type {
	OrganizationCalendarEventDto,
	OrganizationDetailsResponse,
	PublicOpportunitySummaryDto,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import EmptyState from "../components/EmptyState";

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

type Tab = "calendar" | "engagements" | "members" | "settings";

const VALID_TABS: Tab[] = ["calendar", "engagements", "members", "settings"];

function isTab(v: string | null): v is Tab {
	return VALID_TABS.includes(v as Tab);
}

const inputClass =
	"mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-700 focus:outline-none";
const labelClass = "block text-xs text-gray-600";

function Field({
	label,
	id,
	children,
}: {
	label: string;
	id?: string;
	children: React.ReactNode;
}) {
	return (
		<div>
			<label htmlFor={id} className="block text-sm font-medium text-gray-700">
				{label}
			</label>
			{children}
		</div>
	);
}

const MAX_LOGO_BYTES = 2 * 1024 * 1024;
const LOGO_TYPES = ["image/jpeg", "image/png", "image/webp"];

export default function OrganizationOverviewPage() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const [searchParams, setSearchParams] = useSearchParams();
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

	const rawTab = searchParams.get("tab");
	const activeTab: Tab = isTab(rawTab) ? rawTab : "calendar";

	function switchTab(tab: Tab) {
		setSearchParams(tab === "calendar" ? {} : { tab }, { replace: true });
	}

	// ── Org details (loaded immediately for header + settings) ──────────────
	const [org, setOrg] = useState<OrganizationDetailsResponse | null>(null);
	const [orgLoading, setOrgLoading] = useState(true);

	const [form, setForm] = useState({
		name: "",
		description: "",
		contactEmail: "",
		contactPhone: "",
		website: "",
		street: "",
		houseNumber: "",
		zipCode: "",
		city: "",
	});
	const [logoUrl, setLogoUrl] = useState<string | null>(null);
	const [uploadingLogo, setUploadingLogo] = useState(false);
	const [logoError, setLogoError] = useState<string | null>(null);
	const logoInputRef = useRef<HTMLInputElement>(null);

	usePageTitle(org?.name ?? t("orgDashboard.title"));

	useEffect(() => {
		if (!organizationId) return;
		setOrgLoading(true);
		api
			.getOrganizationDetails(organizationId)
			.then((data) => {
				setOrg(data);
				setLogoUrl(data.logoUrl ?? null);
				setForm({
					name: data.name,
					description: data.description ?? "",
					contactEmail: data.contactEmail ?? "",
					contactPhone: data.contactPhone ?? "",
					website: data.website ?? "",
					street: data.address?.street ?? "",
					houseNumber: data.address?.houseNumber ?? "",
					zipCode: data.address?.zipCode ?? "",
					city: data.address?.city ?? "",
				});
			})
			.catch(() => {})
			.finally(() => setOrgLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	// ── Calendar tab ────────────────────────────────────────────────────────
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

	useEffect(() => {
		if (!organizationId) return;
		setCalLoading(true);
		setCalError(null);
		api
			.getOrganizationCalendarEvents(organizationId)
			.then(setCalData)
			.catch((e: unknown) =>
				setCalError(e instanceof Error ? e.message : String(e)),
			)
			.finally(() => setCalLoading(false));
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

	// ── Engagements tab ─────────────────────────────────────────────────────
	const [engOpps, setEngOpps] = useState<PublicOpportunitySummaryDto[]>([]);
	const [engLoading, setEngLoading] = useState(false);
	const [engError, setEngError] = useState<string | null>(null);
	const [engInitialized, setEngInitialized] = useState(false);

	useEffect(() => {
		if (activeTab !== "engagements" || engInitialized || !organizationId)
			return;
		setEngInitialized(true);
		setEngLoading(true);
		api
			.getPublicOrganizationProfile(organizationId)
			.then((profile) => setEngOpps(profile.openOpportunities))
			.catch((e: unknown) =>
				setEngError(e instanceof Error ? e.message : String(e)),
			)
			.finally(() => setEngLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [activeTab, engInitialized, organizationId]);

	// ── Settings / Members tabs ─────────────────────────────────────────────
	const [editing, setEditing] = useState(false);
	const [saving, setSaving] = useState(false);
	const [settingsError, setSettingsError] = useState<string | null>(null);
	const [successMessage, setSuccessMessage] = useState<string | null>(null);

	// ── Member search ────────────────────────────────────────────────────────
	const [memberSearch, setMemberSearch] = useState("");
	const [memberCandidates, setMemberCandidates] = useState<
		import("../client/api-client").MemberCandidateDto[]
	>([]);
	const [memberSearchLoading, setMemberSearchLoading] = useState(false);
	const memberSearchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

	function handleMemberSearchChange(value: string) {
		setMemberSearch(value);
		if (memberSearchTimer.current) clearTimeout(memberSearchTimer.current);
		if (value.length < 2) {
			setMemberCandidates([]);
			return;
		}
		memberSearchTimer.current = setTimeout(() => {
			if (!organizationId) return;
			setMemberSearchLoading(true);
			api
				.searchMemberCandidates(organizationId, value)
				.then(setMemberCandidates)
				.catch(() => setMemberCandidates([]))
				.finally(() => setMemberSearchLoading(false));
		}, 300);
	}

	async function handleAddMember(userId: string) {
		if (!organizationId) return;
		try {
			await api.addMember(organizationId, { userId });
			const added = memberCandidates.find((c) => c.userId === userId);
			if (added) {
				setOrg((prev) =>
					prev
						? {
								...prev,
								members: [
									...prev.members,
									{
										userId: added.userId,
										username: added.username,
										firstName: added.firstName,
										lastName: added.lastName,
										email: added.email,
										isOrganisator: false,
									},
								],
							}
						: prev,
				);
				setMemberCandidates((prev) => prev.filter((c) => c.userId !== userId));
			}
		} catch {
			setSettingsError(t("orgSettings.addMemberError"));
		}
	}

	function handleCancelEdit() {
		if (!org) return;
		setForm({
			name: org.name,
			description: org.description ?? "",
			contactEmail: org.contactEmail ?? "",
			contactPhone: org.contactPhone ?? "",
			website: org.website ?? "",
			street: org.address?.street ?? "",
			houseNumber: org.address?.houseNumber ?? "",
			zipCode: org.address?.zipCode ?? "",
			city: org.address?.city ?? "",
		});
		setLogoError(null);
		setSettingsError(null);
		setEditing(false);
	}

	const hasAddress =
		form.street || form.houseNumber || form.zipCode || form.city;

	async function handleLogoChange(e: React.ChangeEvent<HTMLInputElement>) {
		const file = e.target.files?.[0];
		if (!file || !organizationId) return;
		if (!LOGO_TYPES.includes(file.type)) {
			setLogoError(t("orgSettings.logoHint"));
			return;
		}
		if (file.size > MAX_LOGO_BYTES) {
			setLogoError(t("orgSettings.logoHint"));
			return;
		}
		setUploadingLogo(true);
		setLogoError(null);
		try {
			await api.uploadOrganizationLogo(organizationId, {
				data: file,
				fileName: file.name,
			});
			setLogoUrl(URL.createObjectURL(file));
		} catch {
			setLogoError(t("orgSettings.logoUploadError"));
		} finally {
			setUploadingLogo(false);
			if (logoInputRef.current) logoInputRef.current.value = "";
		}
	}

	async function handleSave(e: React.FormEvent) {
		e.preventDefault();
		if (!organizationId) return;
		setSaving(true);
		setSettingsError(null);
		setSuccessMessage(null);
		try {
			await api.updateOrganization(organizationId, {
				name: form.name,
				description: form.description || undefined,
				contactEmail: form.contactEmail || undefined,
				contactPhone: form.contactPhone || undefined,
				website: form.website || undefined,
				address: hasAddress
					? {
							street: form.street,
							houseNumber: form.houseNumber,
							zipCode: form.zipCode,
							city: form.city,
						}
					: undefined,
			});
			setEditing(false);
			setSuccessMessage(t("orgSettings.savedSuccess"));
			setOrg((prev) =>
				prev
					? {
							...prev,
							name: form.name,
							description: form.description || undefined,
							contactEmail: form.contactEmail || undefined,
							contactPhone: form.contactPhone || undefined,
							website: form.website || undefined,
							address: hasAddress
								? {
										street: form.street,
										houseNumber: form.houseNumber,
										zipCode: form.zipCode,
										city: form.city,
									}
								: undefined,
						}
					: prev,
			);
		} catch {
			setSettingsError(t("orgSettings.saveError"));
		} finally {
			setSaving(false);
		}
	}

	async function handleRemoveMember(userId: string) {
		if (!organizationId) return;
		try {
			await api.removeMember(organizationId, userId);
			setOrg((prev) =>
				prev
					? {
							...prev,
							members: prev.members.filter((m) => m.userId !== userId),
						}
					: prev,
			);
		} catch {
			setSettingsError(t("orgSettings.removeMemberError"));
		}
	}

	// ── Render ───────────────────────────────────────────────────────────────

	const tabs: { key: Tab; label: string }[] = [
		{ key: "calendar", label: t("orgOverview.tabCalendar") },
		{ key: "engagements", label: t("orgOverview.tabEngagements") },
		{ key: "members", label: t("orgOverview.tabMembers") },
		{ key: "settings", label: t("orgOverview.tabSettings") },
	];

	return (
		<div className={activeTab !== "calendar" ? "max-w-2xl" : ""}>
			<h1 className="mb-6 text-2xl font-bold text-gray-900">
				{org?.name ?? t("orgDashboard.title")}
			</h1>

			{/* Tab bar */}
			<div className="mb-6 border-b border-gray-200">
				<nav className="-mb-px flex gap-6" aria-label={t("orgDashboard.title")}>
					{tabs.map(({ key, label }) => (
						<button
							key={key}
							type="button"
							onClick={() => switchTab(key)}
							className={`pb-3 text-sm font-medium transition-colors border-b-2 ${
								activeTab === key
									? "border-brand-700 text-brand-700"
									: "border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300"
							}`}
							aria-current={activeTab === key ? "page" : undefined}
						>
							{label}
						</button>
					))}
				</nav>
			</div>

			{/* ── Calendar tab ──────────────────────────────────────────────────── */}
			{activeTab === "calendar" && (
				<div>
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

					{/* Color picker modal */}
					{selectedEvent && (
						<div className="fixed inset-0 z-50 flex items-center justify-center">
							<button
								type="button"
								aria-hidden="true"
								tabIndex={-1}
								className="absolute inset-0 bg-black/40"
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
									className="mb-4 text-base font-semibold text-gray-900"
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
											<span className="text-sm text-gray-500">
												{pickerColor}
											</span>
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
												to={`/volunteer-opportunities/${selectedEvent.opportunityId}/engagements`}
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
				</div>
			)}

			{/* ── Engagements tab ───────────────────────────────────────────────── */}
			{activeTab === "engagements" && (
				<div>
					{engLoading && (
						<div className="flex items-center justify-center py-16">
							<span className="text-gray-500">
								{t("orgEngagements.loading")}
							</span>
						</div>
					)}
					{engError && (
						<p className="text-red-600">
							{t("orgEngagements.error", { message: engError })}
						</p>
					)}
					{!engLoading && !engError && engOpps.length === 0 && (
						<EmptyState
							title={t("orgEngagements.noOpportunities")}
							message={t("orgEngagements.noOpportunitiesHint")}
						/>
					)}
					{!engLoading && !engError && engOpps.length > 0 && (
						<ul className="space-y-3">
							{engOpps.map((opp) => (
								<li
									key={opp.id}
									className="rounded-xl border border-gray-100 bg-white p-4 shadow-sm transition-shadow hover:shadow-md"
								>
									<p className="text-sm font-semibold text-gray-900">
										{opp.title}
									</p>
									{opp.description && (
										<p className="mt-1 line-clamp-2 text-sm text-gray-500">
											{opp.description}
										</p>
									)}
									<Link
										to={`/volunteer-opportunities/${opp.id}/engagements`}
										className="mt-2 inline-flex items-center gap-1 text-sm font-medium text-brand-700 hover:text-brand-800 hover:underline"
									>
										{t("orgEngagements.manageEngagements")}
										<svg
											className="h-3.5 w-3.5"
											fill="none"
											viewBox="0 0 24 24"
											strokeWidth="2"
											stroke="currentColor"
											aria-hidden="true"
										>
											<path
												strokeLinecap="round"
												strokeLinejoin="round"
												d="M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3"
											/>
										</svg>
									</Link>
								</li>
							))}
						</ul>
					)}
				</div>
			)}

			{/* ── Settings tab ──────────────────────────────────────────────────── */}
			{activeTab === "settings" && (
				<div>
					{orgLoading && (
						<div className="flex items-center justify-center py-16">
							<span className="text-gray-500">{t("orgSettings.loading")}</span>
						</div>
					)}
					{!orgLoading && !org && (
						<div className="py-8 text-center text-red-600">
							{t("orgSettings.notFound")}
						</div>
					)}
					{!orgLoading && org && !editing && (
						<>
							{/* View mode - mirrors the public org profile */}
							<div className="mb-6 flex items-start justify-between gap-4">
								<div className="flex items-center gap-4">
									{logoUrl ? (
										<img
											src={logoUrl}
											alt=""
											className="h-16 w-16 rounded-lg object-contain ring-1 ring-gray-200"
										/>
									) : (
										<span className="flex h-16 w-16 items-center justify-center rounded-lg bg-brand-100 text-2xl font-semibold text-brand-700">
											{org.name.charAt(0).toUpperCase()}
										</span>
									)}
									<div>
										<p className="text-base font-semibold text-gray-900">
											{org.name}
										</p>
										<p className="text-xs text-gray-400">
											{t("orgSettings.createdOn", {
												date: new Date(org.createdOn).toLocaleDateString(
													locale,
													{
														day: "2-digit",
														month: "long",
														year: "numeric",
													},
												),
											})}
										</p>
									</div>
								</div>
								<button
									type="button"
									onClick={() => setEditing(true)}
									className="shrink-0 rounded-md border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
								>
									{t("orgSettings.edit")}
								</button>
							</div>

							{successMessage && (
								<div className="mb-4 rounded-md bg-green-50 px-4 py-3 text-sm text-green-700">
									{successMessage}
								</div>
							)}

							{org.description && (
								<p className="mb-5 leading-relaxed text-gray-600">
									{org.description}
								</p>
							)}

							{(org.contactEmail ||
								org.contactPhone ||
								org.website ||
								org.address) && (
								<div className="space-y-2.5 rounded-2xl border border-gray-100 bg-gray-50 px-4 py-4 text-sm text-gray-700">
									{org.contactEmail && (
										<div className="flex items-center gap-3">
											<svg
												className="h-4 w-4 shrink-0 text-gray-400"
												fill="none"
												viewBox="0 0 24 24"
												strokeWidth="1.5"
												stroke="currentColor"
												aria-hidden="true"
											>
												<path
													strokeLinecap="round"
													strokeLinejoin="round"
													d="M21.75 6.75v10.5a2.25 2.25 0 0 1-2.25 2.25h-15a2.25 2.25 0 0 1-2.25-2.25V6.75m19.5 0A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25m19.5 0v.243a2.25 2.25 0 0 1-1.07 1.916l-7.5 4.615a2.25 2.25 0 0 1-2.36 0L3.32 8.91a2.25 2.25 0 0 1-1.07-1.916V6.75"
												/>
											</svg>
											<a
												href={`mailto:${org.contactEmail}`}
												className="text-brand-700 hover:underline"
											>
												{org.contactEmail}
											</a>
										</div>
									)}
									{org.contactPhone && (
										<div className="flex items-center gap-3">
											<svg
												className="h-4 w-4 shrink-0 text-gray-400"
												fill="none"
												viewBox="0 0 24 24"
												strokeWidth="1.5"
												stroke="currentColor"
												aria-hidden="true"
											>
												<path
													strokeLinecap="round"
													strokeLinejoin="round"
													d="M2.25 6.75c0 8.284 6.716 15 15 15h2.25a2.25 2.25 0 0 0 2.25-2.25v-1.372c0-.516-.351-.966-.852-1.091l-4.423-1.106c-.44-.11-.902.055-1.173.417l-.97 1.293c-.282.376-.769.542-1.21.38a12.035 12.035 0 0 1-7.143-7.143c-.162-.441.004-.928.38-1.21l1.293-.97c.363-.271.527-.734.417-1.173L6.963 3.102a1.125 1.125 0 0 0-1.091-.852H4.5A2.25 2.25 0 0 0 2.25 4.5v2.25Z"
												/>
											</svg>
											<a
												href={`tel:${org.contactPhone}`}
												className="text-brand-700 hover:underline"
											>
												{org.contactPhone}
											</a>
										</div>
									)}
									{org.website && (
										<div className="flex items-center gap-3">
											<svg
												className="h-4 w-4 shrink-0 text-gray-400"
												fill="none"
												viewBox="0 0 24 24"
												strokeWidth="1.5"
												stroke="currentColor"
												aria-hidden="true"
											>
												<path
													strokeLinecap="round"
													strokeLinejoin="round"
													d="M12 21a9.004 9.004 0 0 0 8.716-6.747M12 21a9.004 9.004 0 0 1-8.716-6.747M12 21c2.485 0 4.5-4.03 4.5-9S14.485 3 12 3m0 18c-2.485 0-4.5-4.03-4.5-9S9.515 3 12 3m0 0a8.997 8.997 0 0 1 7.843 4.582M12 3a8.997 8.997 0 0 0-7.843 4.582m15.686 0A11.953 11.953 0 0 1 12 10.5c-2.998 0-5.74-1.1-7.843-2.918m15.686 0A8.959 8.959 0 0 1 21 12c0 .778-.099 1.533-.284 2.253m0 0A17.919 17.919 0 0 1 12 16.5c-3.162 0-6.133-.815-8.716-2.247m0 0A9.015 9.015 0 0 1 3 12c0-1.605.42-3.113 1.157-4.418"
												/>
											</svg>
											<a
												href={org.website}
												target="_blank"
												rel="noopener noreferrer"
												className="text-brand-700 hover:underline"
											>
												{org.website}
											</a>
										</div>
									)}
									{org.address && (
										<div className="flex items-center gap-3">
											<svg
												className="h-4 w-4 shrink-0 text-gray-400"
												fill="none"
												viewBox="0 0 24 24"
												strokeWidth="1.5"
												stroke="currentColor"
												aria-hidden="true"
											>
												<path
													strokeLinecap="round"
													strokeLinejoin="round"
													d="M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
												/>
												<path
													strokeLinecap="round"
													strokeLinejoin="round"
													d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1 1 15 0Z"
												/>
											</svg>
											<span>
												{org.address.street} {org.address.houseNumber},{" "}
												{org.address.zipCode} {org.address.city}
											</span>
										</div>
									)}
								</div>
							)}
						</>
					)}
					{!orgLoading && org && editing && (
						<>
							{settingsError && (
								<div className="mb-4 rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
									{settingsError}
								</div>
							)}

							<form onSubmit={handleSave} className="space-y-5">
								<div>
									<p className="mb-1 block text-sm font-medium text-gray-700">
										{t("orgSettings.fieldLogo")}
									</p>
									<div className="flex items-center gap-4">
										{logoUrl ? (
											<img
												src={logoUrl}
												alt=""
												className="h-16 w-16 rounded-lg object-contain ring-1 ring-gray-200"
											/>
										) : (
											<span className="flex h-16 w-16 items-center justify-center rounded-lg bg-brand-100 text-2xl font-semibold text-brand-700">
												{org.name.charAt(0).toUpperCase()}
											</span>
										)}
										<div>
											<label
												htmlFor="logo-upload"
												className={`cursor-pointer rounded-md border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 ${uploadingLogo ? "opacity-50 pointer-events-none" : ""}`}
											>
												{uploadingLogo
													? t("orgSettings.logoUploading")
													: t("orgSettings.logoUpload")}
											</label>
											<input
												ref={logoInputRef}
												id="logo-upload"
												type="file"
												accept="image/jpeg,image/png,image/webp"
												className="sr-only"
												onChange={handleLogoChange}
												disabled={uploadingLogo}
											/>
											<p className="mt-1 text-xs text-gray-500">
												{t("orgSettings.logoHint")}
											</p>
											{logoError && (
												<p className="mt-1 text-xs text-red-600">{logoError}</p>
											)}
										</div>
									</div>
								</div>

								<Field label={t("orgSettings.fieldName")} id="org-name">
									<input
										id="org-name"
										required
										value={form.name}
										onChange={(e) =>
											setForm((f) => ({ ...f, name: e.target.value }))
										}
										className={inputClass}
									/>
								</Field>

								<Field
									label={t("orgSettings.fieldDescription")}
									id="org-description"
								>
									<textarea
										id="org-description"
										rows={3}
										value={form.description}
										onChange={(e) =>
											setForm((f) => ({
												...f,
												description: e.target.value,
											}))
										}
										className={inputClass}
									/>
								</Field>

								<Field
									label={t("orgSettings.fieldContactEmail")}
									id="org-contact-email"
								>
									<input
										id="org-contact-email"
										type="email"
										value={form.contactEmail}
										onChange={(e) =>
											setForm((f) => ({
												...f,
												contactEmail: e.target.value,
											}))
										}
										className={inputClass}
									/>
								</Field>

								<Field label={t("orgSettings.fieldPhone")} id="org-phone">
									<input
										id="org-phone"
										type="tel"
										value={form.contactPhone}
										onChange={(e) =>
											setForm((f) => ({
												...f,
												contactPhone: e.target.value,
											}))
										}
										className={inputClass}
									/>
								</Field>

								<Field label={t("orgSettings.fieldWebsite")} id="org-website">
									<input
										id="org-website"
										type="url"
										value={form.website}
										onChange={(e) =>
											setForm((f) => ({
												...f,
												website: e.target.value,
											}))
										}
										placeholder="https://"
										className={inputClass}
									/>
								</Field>

								<fieldset className="rounded-md border border-gray-200 p-4">
									<legend className="px-1 text-sm font-medium text-gray-700">
										{t("orgSettings.fieldAddress")}
									</legend>
									<div className="mt-3 grid grid-cols-3 gap-3">
										<div className="col-span-2">
											<label htmlFor="org-street" className={labelClass}>
												{t("orgSettings.fieldStreet")}
											</label>
											<input
												id="org-street"
												value={form.street}
												onChange={(e) =>
													setForm((f) => ({
														...f,
														street: e.target.value,
													}))
												}
												className={inputClass}
											/>
										</div>
										<div>
											<label htmlFor="org-house-number" className={labelClass}>
												{t("orgSettings.fieldHouseNumber")}
											</label>
											<input
												id="org-house-number"
												value={form.houseNumber}
												onChange={(e) =>
													setForm((f) => ({
														...f,
														houseNumber: e.target.value,
													}))
												}
												className={inputClass}
											/>
										</div>
										<div>
											<label htmlFor="org-zip" className={labelClass}>
												{t("orgSettings.fieldZip")}
											</label>
											<input
												id="org-zip"
												maxLength={5}
												value={form.zipCode}
												onChange={(e) =>
													setForm((f) => ({
														...f,
														zipCode: e.target.value,
													}))
												}
												className={inputClass}
											/>
										</div>
										<div className="col-span-2">
											<label htmlFor="org-city" className={labelClass}>
												{t("orgSettings.fieldCity")}
											</label>
											<input
												id="org-city"
												value={form.city}
												onChange={(e) =>
													setForm((f) => ({
														...f,
														city: e.target.value,
													}))
												}
												className={inputClass}
											/>
										</div>
									</div>
								</fieldset>

								<div className="flex justify-end gap-3">
									<button
										type="button"
										onClick={handleCancelEdit}
										className="rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
									>
										{t("orgSettings.cancel")}
									</button>
									<button
										type="submit"
										disabled={saving}
										className="rounded-md bg-brand-700 px-5 py-2 text-sm font-medium text-white hover:bg-brand-800 disabled:opacity-50"
									>
										{saving ? t("orgSettings.saving") : t("orgSettings.save")}
									</button>
								</div>
							</form>
						</>
					)}
				</div>
			)}

			{/* ── Members tab ───────────────────────────────────────────────────── */}
			{activeTab === "members" && (
				<div>
					{orgLoading && (
						<div className="flex items-center justify-center py-16">
							<span className="text-gray-500">{t("orgSettings.loading")}</span>
						</div>
					)}
					{!orgLoading && org && (
						<>
							{settingsError && (
								<div className="mb-4 rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
									{settingsError}
								</div>
							)}

							{/* Add member search */}
							<div className="mb-6">
								<label
									htmlFor="member-search"
									className="block text-sm font-medium text-gray-700"
								>
									{t("orgSettings.addMemberLabel")}
								</label>
								<input
									id="member-search"
									type="search"
									value={memberSearch}
									onChange={(e) => handleMemberSearchChange(e.target.value)}
									placeholder={t("orgSettings.addMemberPlaceholder")}
									className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-700 focus:outline-none"
								/>
								{memberSearchLoading && (
									<p className="mt-1 text-xs text-gray-500">
										{t("orgSettings.searching")}
									</p>
								)}
								{memberCandidates.length > 0 && (
									<ul className="mt-1 divide-y divide-gray-100 rounded-md border border-gray-200 bg-white shadow-sm">
										{memberCandidates.map((candidate) => (
											<li
												key={candidate.userId}
												className="flex items-center justify-between px-3 py-2"
											>
												<div className="min-w-0">
													<p className="truncate text-sm font-medium text-gray-900">
														{candidate.firstName && candidate.lastName
															? `${candidate.firstName} ${candidate.lastName}`
															: candidate.username}
													</p>
													<p className="truncate text-xs text-gray-500">
														{candidate.email}
													</p>
												</div>
												<button
													type="button"
													onClick={() => handleAddMember(candidate.userId)}
													className="ml-3 shrink-0 rounded-md bg-brand-700 px-2.5 py-1 text-xs font-medium text-white hover:bg-brand-800"
												>
													{t("orgSettings.addMember")}
												</button>
											</li>
										))}
									</ul>
								)}
								{memberSearch.length >= 2 &&
									!memberSearchLoading &&
									memberCandidates.length === 0 && (
										<p className="mt-1 text-xs text-gray-500">
											{t("orgSettings.noSearchResults")}
										</p>
									)}
							</div>

							{org.members.length === 0 ? (
								<EmptyState
									title={t("orgSettings.noMembers")}
									message={t("orgSettings.noMembersHint")}
								/>
							) : (
								<ul className="divide-y divide-gray-100">
									{org.members.map((member) => (
										<li
											key={member.userId}
											className="flex items-center justify-between py-3"
										>
											<div>
												<p className="text-sm font-medium text-gray-900">
													{member.firstName && member.lastName
														? `${member.firstName} ${member.lastName}`
														: member.username}
												</p>
												<p className="text-xs text-gray-500">{member.email}</p>
												{member.isOrganisator && (
													<span className="mt-0.5 inline-block rounded-full bg-brand-50 px-2 py-0.5 text-xs text-brand-700">
														{t("orgSettings.organisator")}
													</span>
												)}
											</div>
											<button
												type="button"
												onClick={() => handleRemoveMember(member.userId)}
												className="text-xs text-red-700 hover:text-red-800"
											>
												{t("orgSettings.removeMember")}
											</button>
										</li>
									))}
								</ul>
							)}
						</>
					)}
				</div>
			)}
		</div>
	);
}
