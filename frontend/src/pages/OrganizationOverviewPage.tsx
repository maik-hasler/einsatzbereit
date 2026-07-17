import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import { Calendar, dateFnsLocalizer } from "react-big-calendar";
import type { View } from "react-big-calendar";
import { format, parse, startOfWeek, getDay } from "date-fns";
import { enUS, de } from "date-fns/locale";
import "react-big-calendar/lib/css/react-big-calendar.css";
import type {
	OrganizationCalendarEventDto,
	OrganizationDetailsResponse,
	OrgInvitationDto,
	PublicOpportunitySummaryDto,
	VolunteerOpportunitySummary,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { inputClass, labelClass } from "../lib/formClasses";
import { getApiErrorMessage } from "../lib/apiError";
import EmptyState from "../components/EmptyState";
import ConfirmDialog from "../components/ConfirmDialog";
import CreateVolunteerOpportunityModal from "../components/CreateVolunteerOpportunityModal";
import OrganizationProfileView from "../components/OrganizationProfileView";

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
	const { organizationId: routeOrgId } = useParams<{
		organizationId: string;
	}>();
	const [searchParams, setSearchParams] = useSearchParams();
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const auth = useAuth();
	const navigate = useNavigate();
	const locale = i18n.language === "de" ? "de-DE" : "en-GB";
	const currentUserId = auth.user?.profile?.sub;

	const rawTab = searchParams.get("tab");
	const activeTab: Tab = isTab(rawTab) ? rawTab : "calendar";

	function switchTab(tab: Tab) {
		setSearchParams(tab === "calendar" ? {} : { tab }, { replace: true });
	}

	// ── Org details (loaded immediately for header + settings) ──────────────
	const [org, setOrg] = useState<OrganizationDetailsResponse | null>(null);
	const [orgLoading, setOrgLoading] = useState(true);

	// routeOrgId may be a slug or a GUID (GetOrganizationDetails resolves both);
	// every other endpoint below is GUID-only, so they wait for the real id.
	const organizationId = org?.id;

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
	usePageToolbar([
		{ label: t("breadcrumb.home"), href: "/" },
		{
			label: org?.name ?? t("orgDashboard.title"),
			href:
				(org?.slug ?? organizationId ?? routeOrgId)
					? `/organizations/${org?.slug ?? organizationId ?? routeOrgId}`
					: undefined,
		},
		{ label: t("orgDashboard.title") },
	]);

	useEffect(() => {
		if (!routeOrgId) return;
		setOrgLoading(true);
		api
			.getOrganizationDetails(routeOrgId)
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
	}, [routeOrgId]);

	// ── Create opportunity ───────────────────────────────────────────────────
	const [showCreateModal, setShowCreateModal] = useState(false);

	function handleOpportunityCreated() {
		loadCalendarEvents();
		loadDrafts();
		setEngInitialized(false);
	}

	// ── Drafts (unpublished opportunities, not shown in public listings) ────
	const [drafts, setDrafts] = useState<VolunteerOpportunitySummary[]>([]);

	function loadDrafts() {
		if (!organizationId) return;
		api
			.getOrganizationOpportunityDrafts(organizationId)
			.then(setDrafts)
			.catch(() => {});
	}

	useEffect(() => {
		loadDrafts();
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

	function loadCalendarEvents() {
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
	const [showLeaveConfirm, setShowLeaveConfirm] = useState(false);
	const [leaving, setLeaving] = useState(false);
	const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
	const [deleting, setDeleting] = useState(false);
	const isSoleMember = org?.members.length === 1;

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

	// ── Invitations ──────────────────────────────────────────────────────────
	const [invitations, setInvitations] = useState<OrgInvitationDto[]>([]);

	useEffect(() => {
		if (!organizationId) return;
		api
			.getOrgInvitations(organizationId)
			.then(setInvitations)
			.catch(() => {});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	async function handleInviteMember(userId: string) {
		if (!organizationId) return;
		try {
			const response = await api.createInvitation(organizationId, {
				inviteeId: userId,
			});
			const invited = memberCandidates.find((c) => c.userId === userId);
			setInvitations((prev) => [
				...prev,
				{
					id: response.invitationId,
					inviteeId: userId,
					inviteeName:
						invited?.firstName && invited?.lastName
							? `${invited.firstName} ${invited.lastName}`
							: (invited?.username ?? ""),
					status: "Pending",
					createdOn: new Date(),
				},
			]);
			setMemberCandidates((prev) => prev.filter((c) => c.userId !== userId));
			setMemberSearch("");
			setSettingsError(null);
			setSuccessMessage(t("orgSettings.inviteSent"));
		} catch {
			setSuccessMessage(null);
			setSettingsError(t("orgSettings.inviteError"));
		}
	}

	async function handleDismissInvitation(invitationId: string) {
		if (!organizationId) return;
		try {
			await api.dismissInvitation(organizationId, invitationId);
			setInvitations((prev) => prev.filter((i) => i.id !== invitationId));
		} catch {
			setSettingsError(t("orgSettings.dismissError"));
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

	async function handleLeaveOrganization() {
		if (!organizationId || !currentUserId) return;
		setLeaving(true);
		try {
			await api.removeMember(organizationId, currentUserId);
			navigate("/");
		} catch (err) {
			setShowLeaveConfirm(false);
			setSettingsError(
				getApiErrorMessage(err, t("orgSettings.leaveOrganizationError")),
			);
		} finally {
			setLeaving(false);
		}
	}

	async function handleDeleteOrganization() {
		if (!organizationId) return;
		setDeleting(true);
		try {
			await api.deleteOrganization(organizationId);
			navigate("/");
		} catch (err) {
			setShowDeleteConfirm(false);
			setSettingsError(
				getApiErrorMessage(err, t("orgSettings.deleteOrganizationError")),
			);
		} finally {
			setDeleting(false);
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
		<div>
			<div className="mb-6 flex items-center justify-between gap-3">
				<h1 className="text-2xl font-bold text-gray-900">
					{org?.name ?? t("orgDashboard.title")}
				</h1>
				{organizationId && (
					<button
						type="button"
						onClick={() => setShowCreateModal(true)}
						data-testid="create-opportunity-btn"
						className="inline-flex shrink-0 items-center gap-1.5 rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-brand-700 focus:outline-none"
					>
						{t("orgOverview.createOpportunity")}
					</button>
				)}
			</div>

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

			<div
				className={
					activeTab !== "calendar"
						? "mx-auto max-w-2xl lg:min-h-[600px]"
						: "lg:min-h-[600px]"
				}
			>
				{/* ── Calendar tab ──────────────────────────────────────────────────── */}
				{activeTab === "calendar" && (
					<div>
						{drafts.length > 0 && (
							<section className="mb-8" data-testid="drafts-section">
								<h2 className="text-lg font-semibold text-gray-900">
									{t("orgDashboard.draftsTitle")}
								</h2>
								<p className="mt-1 text-sm text-gray-500">
									{t("orgDashboard.draftsDesc")}
								</p>
								<ul className="mt-4 space-y-3">
									{drafts.map((draft) => (
										<li
											key={draft.id}
											className="relative rounded-2xl border border-gray-100 bg-white p-4 shadow-sm transition hover:border-brand-200 hover:shadow-md"
										>
											<Link
												to={`/volunteer-opportunities/${draft.id}`}
												className="absolute inset-0"
												aria-label={
													draft.title || t("orgDashboard.unnamedDraft")
												}
											/>
											<div className="flex items-center justify-between gap-3">
												<div className="min-w-0">
													<p className="truncate text-sm font-semibold text-gray-900">
														{draft.title || t("orgDashboard.unnamedDraft")}
													</p>
													{draft.description && (
														<p className="mt-0.5 line-clamp-1 text-xs text-gray-500">
															{draft.description}
														</p>
													)}
												</div>
												<span className="shrink-0 rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-800">
													{t("opportunities.draftBadge")}
												</span>
											</div>
										</li>
									))}
								</ul>
							</section>
						)}

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
								<span className="text-gray-500">
									{t("orgSettings.loading")}
								</span>
							</div>
						)}
						{!orgLoading && !org && (
							<div className="py-8 text-center text-red-600">
								{t("orgSettings.notFound")}
							</div>
						)}
						{!orgLoading && org && !editing && (
							<OrganizationProfileView
								name={org.name}
								logoUrl={logoUrl}
								description={org.description}
								contactEmail={org.contactEmail}
								contactPhone={org.contactPhone}
								website={org.website}
								address={org.address}
								subtitle={
									<p className="text-xs text-gray-400">
										{t("orgSettings.createdOn", {
											date: new Date(org.createdOn).toLocaleDateString(locale, {
												day: "2-digit",
												month: "long",
												year: "numeric",
											}),
										})}
									</p>
								}
								actions={
									<button
										type="button"
										onClick={() => setEditing(true)}
										className="shrink-0 rounded-md border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
									>
										{t("orgSettings.edit")}
									</button>
								}
								beforeContent={
									<>
										{successMessage && (
											<div className="mb-4 rounded-md bg-green-50 px-4 py-3 text-sm text-green-700">
												{successMessage}
											</div>
										)}
										{settingsError && (
											<div className="mb-4 rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
												{settingsError}
											</div>
										)}
									</>
								}
							>
								<div className="mt-8 rounded-2xl border border-red-100 bg-red-50 px-4 py-4">
									<h2 className="text-sm font-semibold text-red-800">
										{t("orgSettings.dangerZone")}
									</h2>
									<p className="mt-1 text-xs text-red-700">
										{t("orgSettings.deleteOrganizationHint")}
									</p>
									<button
										type="button"
										onClick={() => setShowDeleteConfirm(true)}
										disabled={!isSoleMember}
										className="mt-3 rounded-md border border-red-300 bg-white px-3 py-1.5 text-sm font-medium text-red-700 hover:bg-red-100 disabled:cursor-not-allowed disabled:border-gray-200 disabled:text-gray-400 disabled:hover:bg-white"
									>
										{t("orgSettings.deleteOrganization")}
									</button>
								</div>
							</OrganizationProfileView>
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
													<p className="mt-1 text-xs text-red-600">
														{logoError}
													</p>
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
												<label
													htmlFor="org-house-number"
													className={labelClass}
												>
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
								<span className="text-gray-500">
									{t("orgSettings.loading")}
								</span>
							</div>
						)}
						{!orgLoading && org && (
							<>
								{successMessage && (
									<div className="mb-4 rounded-md bg-green-50 px-4 py-3 text-sm text-green-700">
										{successMessage}
									</div>
								)}
								{settingsError && (
									<div className="mb-4 rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
										{settingsError}
									</div>
								)}

								{/* Invite member search */}
								<div className="mb-6">
									<label
										htmlFor="member-search"
										className="block text-sm font-medium text-gray-700"
									>
										{t("orgSettings.inviteLabel")}
									</label>
									<input
										id="member-search"
										type="search"
										value={memberSearch}
										onChange={(e) => handleMemberSearchChange(e.target.value)}
										placeholder={t("orgSettings.invitePlaceholder")}
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
														onClick={() => handleInviteMember(candidate.userId)}
														className="ml-3 shrink-0 rounded-md bg-brand-700 px-2.5 py-1 text-xs font-medium text-white hover:bg-brand-800"
													>
														{t("orgSettings.invite")}
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

								{invitations.some((i) => i.status === "Pending") && (
									<div className="mb-6">
										<h2 className="mb-2 text-sm font-medium text-gray-700">
											{t("orgSettings.pendingInvitations")}
										</h2>
										<ul className="divide-y divide-gray-100 rounded-md border border-gray-200 bg-white shadow-sm">
											{invitations
												.filter((i) => i.status === "Pending")
												.map((invitation) => (
													<li
														key={invitation.id}
														className="flex items-center justify-between px-3 py-2"
													>
														<div className="min-w-0">
															<p className="truncate text-sm font-medium text-gray-900">
																{invitation.inviteeName}
															</p>
															<p className="truncate text-xs text-gray-500">
																{t("orgSettings.invitationSentOn", {
																	date: new Date(
																		invitation.createdOn,
																	).toLocaleDateString(locale, {
																		day: "2-digit",
																		month: "long",
																		year: "numeric",
																	}),
																})}
															</p>
														</div>
													</li>
												))}
										</ul>
									</div>
								)}

								{invitations.some((i) => i.status === "Declined") && (
									<div className="mb-6">
										<h2 className="mb-2 text-sm font-medium text-gray-700">
											{t("orgSettings.declinedInvitations")}
										</h2>
										<ul className="divide-y divide-gray-100 rounded-md border border-gray-200 bg-white shadow-sm">
											{invitations
												.filter((i) => i.status === "Declined")
												.map((invitation) => (
													<li
														key={invitation.id}
														className="flex items-center justify-between px-3 py-2"
													>
														<div className="min-w-0">
															<p className="truncate text-sm font-medium text-gray-900">
																{invitation.inviteeName}
															</p>
														</div>
														<button
															type="button"
															onClick={() =>
																handleDismissInvitation(invitation.id)
															}
															className="ml-3 shrink-0 text-xs text-red-700 hover:text-red-800"
														>
															{t("orgSettings.dismissInvitation")}
														</button>
													</li>
												))}
										</ul>
									</div>
								)}

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
													<p className="text-xs text-gray-500">
														{member.email}
													</p>
													{member.isOrganisator && (
														<span className="mt-0.5 inline-block rounded-full bg-brand-50 px-2 py-0.5 text-xs text-brand-700">
															{t("orgSettings.organisator")}
														</span>
													)}
												</div>
												{member.userId === currentUserId ? (
													<button
														type="button"
														onClick={() => setShowLeaveConfirm(true)}
														disabled={isSoleMember}
														title={
															isSoleMember
																? t(
																		"orgSettings.leaveOrganizationLastMemberHint",
																	)
																: undefined
														}
														className="text-xs text-red-700 hover:text-red-800 disabled:cursor-not-allowed disabled:text-gray-400 disabled:hover:text-gray-400"
													>
														{t("orgSettings.leaveOrganization")}
													</button>
												) : (
													<button
														type="button"
														onClick={() => handleRemoveMember(member.userId)}
														className="text-xs text-red-700 hover:text-red-800"
													>
														{t("orgSettings.removeMember")}
													</button>
												)}
											</li>
										))}
									</ul>
								)}
								{isSoleMember && (
									<p className="mt-3 text-xs text-gray-500">
										{t("orgSettings.leaveOrganizationLastMemberHint")}
									</p>
								)}
							</>
						)}
					</div>
				)}
			</div>

			{showLeaveConfirm && org && (
				<ConfirmDialog
					title={t("confirmDialog.leaveOrganization.title")}
					message={t("confirmDialog.leaveOrganization.message", {
						name: org.name,
					})}
					confirmLabel={t("confirmDialog.leaveOrganization.confirm")}
					onConfirm={handleLeaveOrganization}
					onClose={() => setShowLeaveConfirm(false)}
					loading={leaving}
				/>
			)}

			{showDeleteConfirm && org && (
				<ConfirmDialog
					title={t("confirmDialog.deleteOrganization.title")}
					message={t("confirmDialog.deleteOrganization.message", {
						name: org.name,
					})}
					confirmLabel={t("confirmDialog.deleteOrganization.confirm")}
					onConfirm={handleDeleteOrganization}
					onClose={() => setShowDeleteConfirm(false)}
					loading={deleting}
				/>
			)}

			{showCreateModal && organizationId && (
				<CreateVolunteerOpportunityModal
					organizationId={organizationId}
					onClose={() => setShowCreateModal(false)}
					onSuccess={handleOpportunityCreated}
				/>
			)}
		</div>
	);
}
