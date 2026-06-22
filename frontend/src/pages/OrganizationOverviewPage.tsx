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
}

type Tab = "calendar" | "engagements" | "settings";
type SettingsTab = "general" | "members";

const VALID_TABS: Tab[] = ["calendar", "engagements", "settings"];

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

	// ── Settings tab ────────────────────────────────────────────────────────
	const [settingsTab, setSettingsTab] = useState<SettingsTab>("general");
	const [saving, setSaving] = useState(false);
	const [settingsError, setSettingsError] = useState<string | null>(null);
	const [successMessage, setSuccessMessage] = useState<string | null>(null);

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
		{ key: "settings", label: t("orgOverview.tabSettings") },
	];

	return (
		<>
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
									<div className="flex justify-between gap-3">
										<Link
											to={`/volunteer-opportunities/${selectedEvent.opportunityId}`}
											className="text-sm text-brand-700 hover:underline"
											onClick={() => setSelectedEvent(null)}
										>
											{t("orgOverview.eventNavigate")}
										</Link>
										<div className="flex gap-2">
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
				<div className="mx-auto max-w-2xl">
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
				<div className="mx-auto max-w-2xl">
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
					{!orgLoading && org && (
						<>
							<p className="mb-6 text-sm text-gray-500">
								{t("orgSettings.createdOn", {
									date: new Date(org.createdOn).toLocaleDateString(locale, {
										day: "2-digit",
										month: "long",
										year: "numeric",
									}),
								})}
							</p>

							<div className="mb-6 flex gap-4 border-b border-gray-200">
								{(["general", "members"] as SettingsTab[]).map((tab) => (
									<button
										key={tab}
										type="button"
										onClick={() => setSettingsTab(tab)}
										className={`pb-2 text-sm font-medium transition-colors ${
											settingsTab === tab
												? "border-b-2 border-brand-700 text-brand-700"
												: "text-gray-500 hover:text-gray-700"
										}`}
									>
										{tab === "general"
											? t("orgSettings.tabGeneral")
											: t("orgSettings.tabMembers", {
													count: org.members.length,
												})}
									</button>
								))}
							</div>

							{settingsError && (
								<div className="mb-4 rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
									{settingsError}
								</div>
							)}
							{successMessage && (
								<div className="mb-4 rounded-md bg-green-50 px-4 py-3 text-sm text-green-700">
									{successMessage}
								</div>
							)}

							{settingsTab === "general" && (
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

									<div className="flex justify-end">
										<button
											type="submit"
											disabled={saving}
											className="rounded-md bg-brand-700 px-5 py-2 text-sm font-medium text-white hover:bg-brand-800 disabled:opacity-50"
										>
											{saving ? t("orgSettings.saving") : t("orgSettings.save")}
										</button>
									</div>
								</form>
							)}

							{settingsTab === "members" && (
								<>
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
						</>
					)}
				</div>
			)}
		</>
	);
}
