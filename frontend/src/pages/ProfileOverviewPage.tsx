import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
	EngagementSummary,
	MyInvitationDto,
	MyProfileResponse,
	StreakSummary,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import { getApiErrorMessage } from "../lib/apiError";
import { ENGAGEMENT_STATUS_COLORS } from "../lib/engagementStatus";
import BadgeGrid from "../components/BadgeGrid";
import CheckInModal from "../components/CheckInModal";
import ConfirmDialog from "../components/ConfirmDialog";
import CreateOrganizationModal from "../components/CreateOrganizationModal";
import EmptyState from "../components/EmptyState";
import ShareAchievementsModal from "../components/ShareAchievementsModal";
import SubmitFeedbackModal from "../components/SubmitFeedbackModal";

const MAX_AVATAR_BYTES = 2 * 1024 * 1024;
const AVATAR_TYPES = ["image/jpeg", "image/png", "image/webp"];

type Tab = "profile" | "engagements" | "achievements" | "invitations";
type ContactPref = "Email" | "Phone" | "";

const VALID_TABS: Tab[] = [
	"profile",
	"engagements",
	"achievements",
	"invitations",
];

function isTab(value: string | null): value is Tab {
	return VALID_TABS.includes(value as Tab);
}

const inputClass =
	"mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-700 focus:outline-none";

const textareaClass =
	"mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-700 focus:outline-none resize-y";

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

function ChipInput({
	inputRef,
	inputId,
	chips,
	inputValue,
	placeholder,
	onInputChange,
	onAdd,
	onRemove,
	removeLabel,
}: {
	inputRef: React.RefObject<HTMLInputElement | null>;
	inputId: string;
	chips: string[];
	inputValue: string;
	placeholder: string;
	onInputChange: (v: string) => void;
	onAdd: (v: string) => void;
	onRemove: (v: string) => void;
	removeLabel: string;
}) {
	function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
		if (e.key === "Enter") {
			e.preventDefault();
			onAdd(inputValue);
		}
	}

	return (
		<div className="mt-1">
			{chips.length > 0 && (
				<div className="mb-2 flex flex-wrap gap-2">
					{chips.map((chip) => (
						<span
							key={chip}
							className="inline-flex items-center gap-1 rounded-full bg-brand-50 px-3 py-1 text-sm text-brand-700"
						>
							{chip}
							<button
								type="button"
								aria-label={`${removeLabel} ${chip}`}
								onClick={() => onRemove(chip)}
								className="ml-1 text-brand-400 hover:text-brand-700"
							>
								&times;
							</button>
						</span>
					))}
				</div>
			)}
			<input
				ref={inputRef}
				id={inputId}
				type="text"
				value={inputValue}
				placeholder={placeholder}
				onChange={(e) => onInputChange(e.target.value)}
				onKeyDown={handleKeyDown}
				onBlur={() => {
					if (inputValue.trim()) onAdd(inputValue);
				}}
				className={inputClass}
			/>
		</div>
	);
}

const STATUS_COLORS = ENGAGEMENT_STATUS_COLORS;

export default function ProfileOverviewPage() {
	const auth = useAuth();
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	const navigate = useNavigate();
	const [searchParams, setSearchParams] = useSearchParams();
	usePageTitle(t("profile.title"));

	const rawTab = searchParams.get("tab");
	const activeTab: Tab = isTab(rawTab) ? rawTab : "profile";

	function switchTab(tab: Tab) {
		setSearchParams(tab === "profile" ? {} : { tab }, { replace: true });
	}

	const accessToken = auth.user?.access_token;
	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

	// --- Profile tab state ---
	const [profile, setProfile] = useState<MyProfileResponse | null>(null);
	const [profileLoading, setProfileLoading] = useState(true);
	const [saving, setSaving] = useState(false);
	const [profileError, setProfileError] = useState<string | null>(null);
	const [successMessage, setSuccessMessage] = useState<string | null>(null);
	const [firstName, setFirstName] = useState("");
	const [lastName, setLastName] = useState("");
	const [bio, setBio] = useState("");
	const [skills, setSkills] = useState<string[]>([]);
	const [languages, setLanguages] = useState<string[]>([]);
	const [preferredContact, setPreferredContact] = useState<ContactPref>("");
	const [skillInput, setSkillInput] = useState("");
	const [langInput, setLangInput] = useState("");
	const skillInputRef = useRef<HTMLInputElement>(null);
	const langInputRef = useRef<HTMLInputElement>(null);
	const [showDeleteDialog, setShowDeleteDialog] = useState(false);
	const [deleting, setDeleting] = useState(false);
	const [deleteError, setDeleteError] = useState<string | null>(null);
	const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
	const [uploadingAvatar, setUploadingAvatar] = useState(false);
	const [avatarError, setAvatarError] = useState<string | null>(null);
	const avatarInputRef = useRef<HTMLInputElement>(null);
	const [showCreateOrgModal, setShowCreateOrgModal] = useState(false);
	const [editing, setEditing] = useState(false);

	// --- Engagements tab state ---
	const [engagements, setEngagements] = useState<EngagementSummary[]>([]);
	const [engagementsLoading, setEngagementsLoading] = useState(false);
	const [engagementsError, setEngagementsError] = useState<string | null>(null);
	const [engagementsInitialized, setEngagementsInitialized] = useState(false);
	const [confirmWithdrawId, setConfirmWithdrawId] = useState<string | null>(
		null,
	);
	const [withdrawing, setWithdrawing] = useState(false);
	const [withdrawError, setWithdrawError] = useState<string | null>(null);
	const [checkInEngagement, setCheckInEngagement] =
		useState<EngagementSummary | null>(null);
	const [feedbackEngagement, setFeedbackEngagement] =
		useState<EngagementSummary | null>(null);

	// --- Achievements tab state ---
	const [achievements, setAchievements] = useState<AchievementSummary[]>([]);
	const [catalog, setCatalog] = useState<BadgeCatalogEntry[]>([]);
	const [streaks, setStreaks] = useState<StreakSummary | null>(null);
	const [achievementsLoading, setAchievementsLoading] = useState(false);
	const [achievementsError, setAchievementsError] = useState<string | null>(
		null,
	);
	const [achievementsInitialized, setAchievementsInitialized] = useState(false);
	const [shareModalOpen, setShareModalOpen] = useState(false);

	// --- Invitations tab state ---
	const [invitations, setInvitations] = useState<MyInvitationDto[]>([]);
	const [invitationsLoading, setInvitationsLoading] = useState(false);
	const [invitationsError, setInvitationsError] = useState<string | null>(null);
	const [invitationsInitialized, setInvitationsInitialized] = useState(false);
	const [acceptingId, setAcceptingId] = useState<string | null>(null);
	const [decliningId, setDecliningId] = useState<string | null>(null);
	const [invitationActionError, setInvitationActionError] = useState<
		string | null
	>(null);

	const STATUS_LABELS: Record<string, string> = {
		Pending: t("myEngagements.status.Pending"),
		Confirmed: t("myEngagements.status.Confirmed"),
		Cancelled: t("myEngagements.status.Cancelled"),
		Withdrawn: t("myEngagements.status.Withdrawn"),
	};

	// Load profile data (always load on mount with retry)
	useEffect(() => {
		let cancelled = false;
		const retryDelaysMs = [500, 1000, 2000];

		async function loadProfile() {
			setProfileLoading(true);
			for (let attempt = 0; ; attempt++) {
				try {
					const data = await api.getUserProfile();
					if (cancelled) return;
					setProfile(data);
					setFirstName(data.firstName ?? "");
					setLastName(data.lastName ?? "");
					setBio(data.bio ?? "");
					setSkills(data.skills ?? []);
					setLanguages(data.languages ?? []);
					setAvatarUrl(data.avatarUrl ?? null);
					const pref = data.preferredContact;
					setPreferredContact(pref === "Email" || pref === "Phone" ? pref : "");
					setProfileError(null);
					return;
				} catch {
					if (cancelled) return;
					if (attempt >= retryDelaysMs.length) {
						setProfileError(t("profile.loadError"));
						return;
					}
					await new Promise<void>((resolve) =>
						setTimeout(resolve, retryDelaysMs[attempt]),
					);
				}
			}
		}

		loadProfile().finally(() => {
			if (!cancelled) setProfileLoading(false);
		});

		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [accessToken]);

	// Load engagements lazily on first visit to that tab
	useEffect(() => {
		if (activeTab !== "engagements" || engagementsInitialized) return;
		setEngagementsInitialized(true);
		setEngagementsLoading(true);
		api
			.getMyEngagements()
			.then(setEngagements)
			.catch((err) =>
				setEngagementsError(getApiErrorMessage(err, t("error.serverError"))),
			)
			.finally(() => setEngagementsLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [activeTab, engagementsInitialized]);

	// Load invitations lazily on first visit to that tab
	useEffect(() => {
		if (activeTab !== "invitations" || invitationsInitialized) return;
		setInvitationsInitialized(true);
		setInvitationsLoading(true);
		api
			.getMyInvitations()
			.then(setInvitations)
			.catch(() => setInvitationsError(t("invitations.loadError")))
			.finally(() => setInvitationsLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [activeTab, invitationsInitialized]);

	// Load achievements lazily on first visit to that tab
	useEffect(() => {
		if (activeTab !== "achievements" || achievementsInitialized) return;
		setAchievementsInitialized(true);
		setAchievementsLoading(true);
		Promise.all([
			api.getMyAchievements(),
			api.getBadgeCatalog(),
			api.getMyStreaks(),
		])
			.then(([ach, cat, str]) => {
				setAchievements(ach);
				setCatalog(cat);
				setStreaks(str);
			})
			.catch((err) =>
				setAchievementsError(getApiErrorMessage(err, t("error.serverError"))),
			)
			.finally(() => setAchievementsLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [activeTab, achievementsInitialized]);

	function addChip(
		value: string,
		list: string[],
		setList: (l: string[]) => void,
		setInput: (s: string) => void,
	) {
		const trimmed = value.trim();
		if (trimmed && !list.includes(trimmed)) {
			setList([...list, trimmed]);
		}
		setInput("");
	}

	function removeChip(
		item: string,
		list: string[],
		setList: (l: string[]) => void,
	) {
		setList(list.filter((s) => s !== item));
	}

	async function handleAvatarChange(e: React.ChangeEvent<HTMLInputElement>) {
		const file = e.target.files?.[0];
		if (!file) return;
		if (!AVATAR_TYPES.includes(file.type)) {
			setAvatarError(t("profile.avatarHint"));
			return;
		}
		if (file.size > MAX_AVATAR_BYTES) {
			setAvatarError(t("profile.avatarHint"));
			return;
		}
		setUploadingAvatar(true);
		setAvatarError(null);
		try {
			await api.uploadUserAvatar({ data: file, fileName: file.name });
			setAvatarUrl(URL.createObjectURL(file));
		} catch {
			setAvatarError(t("profile.avatarUploadError"));
		} finally {
			setUploadingAvatar(false);
			if (avatarInputRef.current) avatarInputRef.current.value = "";
		}
	}

	async function handleSave(e: React.FormEvent) {
		e.preventDefault();
		setSaving(true);
		setProfileError(null);
		setSuccessMessage(null);
		try {
			await api.updateUserProfile({
				firstName: firstName || undefined,
				lastName: lastName || undefined,
				bio: bio || undefined,
				skills,
				languages,
				preferredContact: preferredContact || undefined,
			});
			setSuccessMessage(t("profile.savedSuccess"));
			setEditing(false);
		} catch {
			setProfileError(t("profile.saveError"));
		} finally {
			setSaving(false);
		}
	}

	function handleCancel() {
		if (profile) {
			setFirstName(profile.firstName ?? "");
			setLastName(profile.lastName ?? "");
			setBio(profile.bio ?? "");
			setSkills(profile.skills ?? []);
			setLanguages(profile.languages ?? []);
			const pref = profile.preferredContact;
			setPreferredContact(pref === "Email" || pref === "Phone" ? pref : "");
		}
		setProfileError(null);
		setEditing(false);
	}

	async function handleDeleteAccount() {
		setDeleting(true);
		setDeleteError(null);
		try {
			await api.deleteMyAccount();
			await auth.removeUser();
			navigate("/");
		} catch {
			setDeleteError(t("account.deleteError"));
			setDeleting(false);
		}
	}

	async function handleWithdrawConfirm() {
		if (!confirmWithdrawId) return;
		setWithdrawing(true);
		setWithdrawError(null);
		try {
			const updated = await api.withdrawEngagement(confirmWithdrawId);
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === confirmWithdrawId ? { ...e, status: updated.status } : e,
				),
			);
			setConfirmWithdrawId(null);
		} catch (err) {
			setWithdrawError(
				err instanceof Error ? err.message : t("myEngagements.withdrawError"),
			);
		} finally {
			setWithdrawing(false);
		}
	}

	function handleWithdrawClose() {
		if (withdrawing) return;
		setConfirmWithdrawId(null);
		setWithdrawError(null);
	}

	function handleCheckedIn() {
		if (!checkInEngagement) return;
		setEngagements((prev) =>
			prev.map((e) =>
				e.id === checkInEngagement.id ? { ...e, isCheckedIn: true } : e,
			),
		);
	}

	function handleFeedbackSubmitted() {
		if (!feedbackEngagement) return;
		setEngagements((prev) =>
			prev.map((e) =>
				e.id === feedbackEngagement.id ? { ...e, hasFeedback: true } : e,
			),
		);
	}

	async function handleAcceptInvitation(invitationId: string) {
		setAcceptingId(invitationId);
		setInvitationActionError(null);
		try {
			await api.acceptInvitation(invitationId);
			setInvitations((prev) => prev.filter((i) => i.id !== invitationId));
		} catch {
			setInvitationActionError(t("invitations.acceptError"));
		} finally {
			setAcceptingId(null);
		}
	}

	async function handleDeclineInvitation(invitationId: string) {
		setDecliningId(invitationId);
		setInvitationActionError(null);
		try {
			await api.declineInvitation(invitationId);
			setInvitations((prev) => prev.filter((i) => i.id !== invitationId));
		} catch {
			setInvitationActionError(t("invitations.declineError"));
		} finally {
			setDecliningId(null);
		}
	}

	const shareUrl = auth.user?.profile?.sub
		? window.location.origin +
			"/users/" +
			auth.user.profile.sub +
			"/achievements"
		: window.location.origin + "/profile?tab=achievements";

	const tabs: { key: Tab; label: string }[] = [
		{ key: "profile", label: t("profileOverview.tabProfile") },
		{ key: "engagements", label: t("profileOverview.tabEngagements") },
		{ key: "achievements", label: t("profileOverview.tabAchievements") },
		{ key: "invitations", label: t("profileOverview.tabInvitations") },
	];

	return (
		<>
			<h1 className="mb-6 text-2xl font-bold text-gray-900">
				{t("profile.title")}
			</h1>

			{/* Tab bar */}
			<div className="mb-6 border-b border-gray-200">
				<nav className="-mb-px flex gap-6" aria-label={t("profile.title")}>
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

			{/* Profile tab */}
			{activeTab === "profile" && (
				<div className="mx-auto max-w-2xl">
					{profileLoading && (
						<div className="flex items-center justify-center py-16">
							<span className="text-gray-500">{t("profile.loading")}</span>
						</div>
					)}

					{!profileLoading && (
						<>
							{profileError && (
								<div className="mb-4 rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
									{profileError}
								</div>
							)}
							{successMessage && (
								<div className="mb-4 rounded-md bg-green-50 px-4 py-3 text-sm text-green-700">
									{successMessage}
								</div>
							)}

							{/* View mode */}
							{!editing && (
								<div className="space-y-5">
									<div className="flex items-start justify-between">
										<div className="flex items-center gap-4">
											{avatarUrl ? (
												<img
													src={avatarUrl}
													alt=""
													className="h-16 w-16 rounded-full object-cover ring-2 ring-brand-100"
												/>
											) : (
												<span className="flex h-16 w-16 items-center justify-center rounded-full bg-brand-100 text-2xl font-semibold text-brand-700">
													{profile?.username?.charAt(0).toUpperCase() ?? "?"}
												</span>
											)}
											<div>
												<p className="text-xl font-semibold text-gray-900">
													{firstName || lastName
														? `${firstName} ${lastName}`.trim()
														: profile?.username}
												</p>
												<p className="text-sm text-gray-500">
													@{profile?.username}
												</p>
												<p className="text-sm text-gray-500">
													{profile?.email}
												</p>
											</div>
										</div>
										<button
											type="button"
											onClick={() => setEditing(true)}
											className="rounded-md border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
										>
											{t("profile.edit")}
										</button>
									</div>

									{bio && (
										<div>
											<p className="mb-1 text-sm font-medium text-gray-700">
												{t("profile.fieldBio")}
											</p>
											<p className="whitespace-pre-wrap text-sm text-gray-600">
												{bio}
											</p>
										</div>
									)}

									{skills.length > 0 && (
										<div>
											<p className="mb-2 text-sm font-medium text-gray-700">
												{t("profile.fieldSkills")}
											</p>
											<div className="flex flex-wrap gap-2">
												{skills.map((s) => (
													<span
														key={s}
														className="rounded-full bg-brand-50 px-3 py-1 text-sm text-brand-700"
													>
														{s}
													</span>
												))}
											</div>
										</div>
									)}

									{languages.length > 0 && (
										<div>
											<p className="mb-2 text-sm font-medium text-gray-700">
												{t("profile.fieldLanguages")}
											</p>
											<div className="flex flex-wrap gap-2">
												{languages.map((l) => (
													<span
														key={l}
														className="rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-600"
													>
														{l}
													</span>
												))}
											</div>
										</div>
									)}

									{preferredContact && (
										<div>
											<p className="mb-1 text-sm font-medium text-gray-700">
												{t("profile.fieldPreferredContact")}
											</p>
											<p className="text-sm text-gray-600">
												{preferredContact === "Email"
													? t("profile.preferredContactEmail")
													: t("profile.preferredContactPhone")}
											</p>
										</div>
									)}
								</div>
							)}

							{/* Edit mode */}
							{editing && (
								<form onSubmit={handleSave} className="space-y-6">
									<section>
										<h2 className="mb-4 text-base font-semibold text-gray-900">
											{t("account.title")}
										</h2>
										<div className="space-y-5">
											<div>
												<p className="mb-1 block text-sm font-medium text-gray-700">
													{t("profile.fieldAvatar")}
												</p>
												<div className="flex items-center gap-4">
													{avatarUrl ? (
														<img
															src={avatarUrl}
															alt=""
															className="h-16 w-16 rounded-full object-cover ring-2 ring-brand-100"
														/>
													) : (
														<span className="flex h-16 w-16 items-center justify-center rounded-full bg-brand-100 text-2xl font-semibold text-brand-700">
															{profile?.username?.charAt(0).toUpperCase() ??
																"?"}
														</span>
													)}
													<div>
														<label
															htmlFor="avatar-upload"
															className={`cursor-pointer rounded-md border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 ${uploadingAvatar ? "opacity-50 pointer-events-none" : ""}`}
														>
															{uploadingAvatar
																? t("profile.avatarUploading")
																: t("profile.avatarUpload")}
														</label>
														<input
															ref={avatarInputRef}
															id="avatar-upload"
															type="file"
															accept="image/jpeg,image/png,image/webp"
															className="sr-only"
															onChange={handleAvatarChange}
															disabled={uploadingAvatar}
														/>
														<p className="mt-1 text-xs text-gray-500">
															{t("profile.avatarHint")}
														</p>
														{avatarError && (
															<p className="mt-1 text-xs text-red-600">
																{avatarError}
															</p>
														)}
													</div>
												</div>
											</div>

											<Field label={t("account.fieldUsername")} id="username">
												<input
													id="username"
													disabled
													value={profile?.username ?? ""}
													className={`${inputClass} cursor-not-allowed bg-gray-50 text-gray-500`}
												/>
											</Field>

											<Field label={t("account.fieldEmail")} id="email">
												<input
													id="email"
													disabled
													type="email"
													value={profile?.email ?? ""}
													className={`${inputClass} cursor-not-allowed bg-gray-50 text-gray-500`}
												/>
												<p className="mt-1 text-xs text-gray-500">
													{t("account.emailHint")}
												</p>
											</Field>

											<Field
												label={t("account.fieldFirstName")}
												id="first-name"
											>
												<input
													id="first-name"
													value={firstName}
													onChange={(e) => setFirstName(e.target.value)}
													className={inputClass}
												/>
											</Field>

											<Field label={t("account.fieldLastName")} id="last-name">
												<input
													id="last-name"
													value={lastName}
													onChange={(e) => setLastName(e.target.value)}
													className={inputClass}
												/>
											</Field>
										</div>
									</section>

									<hr className="border-gray-200" />

									<section>
										<h2 className="mb-4 text-base font-semibold text-gray-900">
											{t("profile.sectionDetails")}
										</h2>
										<div className="space-y-5">
											<Field label={t("profile.fieldBio")} id="bio">
												<textarea
													id="bio"
													rows={4}
													value={bio}
													placeholder={t("profile.bioPlaceholder")}
													onChange={(e) => setBio(e.target.value)}
													className={textareaClass}
												/>
											</Field>

											<Field label={t("profile.fieldSkills")} id="skill-input">
												<ChipInput
													inputRef={skillInputRef}
													inputId="skill-input"
													chips={skills}
													inputValue={skillInput}
													placeholder={t("profile.skillsPlaceholder")}
													onInputChange={setSkillInput}
													onAdd={(v) =>
														addChip(v, skills, setSkills, setSkillInput)
													}
													onRemove={(v) => removeChip(v, skills, setSkills)}
													removeLabel={t("profile.removeChip")}
												/>
											</Field>

											<Field
												label={t("profile.fieldLanguages")}
												id="lang-input"
											>
												<ChipInput
													inputRef={langInputRef}
													inputId="lang-input"
													chips={languages}
													inputValue={langInput}
													placeholder={t("profile.languagesPlaceholder")}
													onInputChange={setLangInput}
													onAdd={(v) =>
														addChip(v, languages, setLanguages, setLangInput)
													}
													onRemove={(v) =>
														removeChip(v, languages, setLanguages)
													}
													removeLabel={t("profile.removeChip")}
												/>
											</Field>

											<Field
												label={t("profile.fieldPreferredContact")}
												id="preferred-contact"
											>
												<select
													id="preferred-contact"
													value={preferredContact}
													onChange={(e) =>
														setPreferredContact(e.target.value as ContactPref)
													}
													className={inputClass}
												>
													<option value="">
														{t("profile.preferredContactNone")}
													</option>
													<option value="Email">
														{t("profile.preferredContactEmail")}
													</option>
													<option value="Phone">
														{t("profile.preferredContactPhone")}
													</option>
												</select>
											</Field>
										</div>
									</section>

									<div className="flex justify-end gap-3">
										<button
											type="button"
											onClick={handleCancel}
											className="rounded-md border border-gray-300 px-5 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
										>
											{t("profile.cancel")}
										</button>
										<button
											type="submit"
											disabled={saving}
											className="rounded-md bg-brand-700 px-5 py-2 text-sm font-medium text-white hover:bg-brand-800 disabled:opacity-50"
										>
											{saving ? t("profile.saving") : t("profile.save")}
										</button>
									</div>
								</form>
							)}

							<div className="mt-8 rounded-lg border border-gray-200 bg-gray-50 p-6">
								<h2 className="mb-1 text-base font-semibold text-gray-900">
									{t("profile.sectionOrganization")}
								</h2>
								<p className="mb-4 text-sm text-gray-600">
									{t("profile.createOrgHint")}
								</p>
								<button
									type="button"
									onClick={() => setShowCreateOrgModal(true)}
									data-testid="create-org-btn"
									className="rounded-md border border-brand-700 px-4 py-2 text-sm font-medium text-brand-700 hover:bg-brand-50"
								>
									{t("organization.create")}
								</button>
							</div>

							<div className="mt-8 rounded-lg border border-red-200 bg-red-50 p-6">
								<h2 className="mb-1 text-base font-semibold text-red-800">
									{t("account.dangerZoneTitle")}
								</h2>
								<p className="mb-4 text-sm text-red-700">
									{t("account.dangerZoneDescription")}
								</p>
								<button
									type="button"
									onClick={() => setShowDeleteDialog(true)}
									className="rounded-md border border-red-700 px-4 py-2 text-sm font-medium text-red-700 hover:bg-red-50"
								>
									{t("account.deleteAccountButton")}
								</button>
							</div>
						</>
					)}
				</div>
			)}

			{/* Engagements tab */}
			{activeTab === "engagements" && (
				<>
					{engagementsLoading && (
						<p className="text-gray-500">{t("myEngagements.loading")}</p>
					)}
					{engagementsError && (
						<p className="text-red-600">
							{t("myEngagements.error", { message: engagementsError })}
						</p>
					)}

					{!engagementsLoading &&
						!engagementsError &&
						engagements.length === 0 && (
							<EmptyState
								title={t("myEngagements.noEngagements")}
								message={t("myEngagements.noEngagementsHint")}
								action={{
									label: t("myEngagements.exploreNeeds"),
									onClick: () => navigate("/"),
								}}
							/>
						)}

					{!engagementsLoading &&
						!engagementsError &&
						engagements.length > 0 && (
							<ul className="space-y-3">
								{engagements.map((e) => (
									<li
										key={e.id}
										className="rounded-xl border border-gray-100 bg-white px-4 py-4 shadow-sm transition-shadow hover:shadow-md"
									>
										<div className="flex items-start justify-between gap-3">
											<div className="min-w-0">
												<Link
													to={`/volunteer-opportunities/${e.opportunityId}`}
													className="text-sm font-semibold text-gray-900 transition-colors hover:text-brand-700"
												>
													{e.opportunityTitle}
												</Link>
												<p className="mt-0.5 text-xs text-gray-500">
													<Link
														to={`/organizations/${e.organizationId}`}
														className="hover:underline"
													>
														{e.organizationName}
													</Link>
												</p>
												{e.message && (
													<p className="mt-1 truncate text-sm italic text-gray-500">
														&ldquo;{e.message}&rdquo;
													</p>
												)}
												<p className="mt-1.5 text-xs text-gray-400">
													{t("myEngagements.registeredOn", {
														date: new Date(e.createdOn).toLocaleDateString(
															locale,
														),
													})}
												</p>
												{e.isCheckedIn && (
													<span className="mt-2 inline-flex items-center gap-1 rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-800">
														<svg
															className="h-3 w-3"
															fill="currentColor"
															viewBox="0 0 20 20"
															aria-hidden="true"
														>
															<path
																fillRule="evenodd"
																d="M16.704 4.153a.75.75 0 0 1 .143 1.052l-8 10.5a.75.75 0 0 1-1.127.075l-4.5-4.5a.75.75 0 0 1 1.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 0 1 1.05-.143Z"
																clipRule="evenodd"
															/>
														</svg>
														{t("checkIn.checkedInLabel")}
													</span>
												)}
											</div>
											<div className="flex shrink-0 flex-col items-end gap-2">
												<span
													className={`rounded-full border px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[e.status] ?? "bg-gray-100 text-gray-600 border-gray-200"}`}
												>
													{STATUS_LABELS[e.status] ?? e.status}
												</span>
												{e.status === "Confirmed" && !e.isCheckedIn && (
													<button
														onClick={() => setCheckInEngagement(e)}
														className="rounded-lg bg-brand-700 px-3 py-1 text-xs font-medium text-white transition-colors hover:bg-brand-800"
													>
														{t("checkIn.buttonLabel")}
													</button>
												)}
												{e.isCheckedIn && !e.hasFeedback && (
													<button
														onClick={() => setFeedbackEngagement(e)}
														className="rounded-lg bg-yellow-500 px-3 py-1 text-xs font-medium text-white transition-colors hover:bg-yellow-600"
													>
														{t("feedback.buttonLabel")}
													</button>
												)}
												{e.isCheckedIn && e.hasFeedback && (
													<span className="rounded-full bg-yellow-50 px-2.5 py-0.5 text-xs text-yellow-700">
														{t("feedback.submitted")}
													</span>
												)}
												{(e.status === "Pending" ||
													e.status === "Confirmed") && (
													<button
														onClick={() => setConfirmWithdrawId(e.id)}
														className="rounded-lg border border-red-200 px-3 py-1 text-xs text-red-600 transition-colors hover:bg-red-50"
													>
														{t("myEngagements.withdraw")}
													</button>
												)}
											</div>
										</div>
									</li>
								))}
							</ul>
						)}
				</>
			)}

			{/* Achievements tab */}
			{activeTab === "achievements" && (
				<>
					{achievementsError && (
						<p className="text-sm text-red-600">
							{t("achievements.error", { message: achievementsError })}
						</p>
					)}

					{!achievementsError && (
						<>
							<div className="mb-6 flex items-center justify-between">
								<div />
								<button
									type="button"
									onClick={() => setShareModalOpen(true)}
									className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50"
								>
									<svg
										className="h-4 w-4"
										fill="none"
										viewBox="0 0 24 24"
										strokeWidth="1.5"
										stroke="currentColor"
										aria-hidden="true"
									>
										<path
											strokeLinecap="round"
											strokeLinejoin="round"
											d="M7.217 10.907a2.25 2.25 0 1 0 0 2.186m0-2.186c.18.324.283.696.283 1.093s-.103.77-.283 1.093m0-2.186 9.566-5.314m-9.566 7.5 9.566 5.314m0 0a2.25 2.25 0 1 0 3.935 2.186 2.25 2.25 0 0 0-3.935-2.186Zm0-12.814a2.25 2.25 0 1 0 3.933-2.185 2.25 2.25 0 0 0-3.933 2.185Z"
										/>
									</svg>
									{t("achievements.shareButton")}
								</button>
							</div>

							{streaks && (
								<div className="mb-6 flex flex-wrap gap-3">
									<div className="flex items-center gap-3 rounded-xl border border-gray-100 bg-gray-50 px-4 py-3">
										<span className="text-2xl" aria-hidden="true">
											🔥
										</span>
										<div>
											<p className="text-xl font-bold text-gray-900">
												{streaks.loginStreak}
											</p>
											<p className="text-xs text-gray-500">
												{t("achievements.loginStreak", {
													count: streaks.loginStreak,
												})}
											</p>
										</div>
									</div>
									<div className="flex items-center gap-3 rounded-xl border border-gray-100 bg-gray-50 px-4 py-3">
										<span className="text-2xl" aria-hidden="true">
											📅
										</span>
										<div>
											<p className="text-xl font-bold text-gray-900">
												{streaks.activityStreak}
											</p>
											<p className="text-xs text-gray-500">
												{t("achievements.activityStreak", {
													count: streaks.activityStreak,
												})}
											</p>
										</div>
									</div>
								</div>
							)}

							<section>
								<h2 className="mb-4 text-base font-semibold text-gray-700">
									{t("achievements.badgesTitle")}
								</h2>
								<BadgeGrid
									earned={achievements}
									catalog={catalog}
									loading={achievementsLoading}
								/>
							</section>
						</>
					)}
				</>
			)}

			{/* Invitations tab */}
			{activeTab === "invitations" && (
				<>
					{invitationsLoading && (
						<p className="text-gray-500">{t("invitations.loading")}</p>
					)}
					{invitationsError && (
						<p className="text-red-600">{invitationsError}</p>
					)}
					{invitationActionError && (
						<div className="mb-4 rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
							{invitationActionError}
						</div>
					)}
					{!invitationsLoading &&
						!invitationsError &&
						invitations.length === 0 && (
							<EmptyState
								title={t("invitations.empty")}
								message={t("invitations.emptyHint")}
							/>
						)}
					{!invitationsLoading && invitations.length > 0 && (
						<ul className="space-y-3">
							{invitations.map((inv) => (
								<li
									key={inv.id}
									className="rounded-xl border border-gray-100 bg-white px-4 py-4 shadow-sm"
								>
									<div className="flex items-start justify-between gap-3">
										<div>
											<p className="text-sm font-semibold text-gray-900">
												{inv.organizationName}
											</p>
											<p className="mt-0.5 text-xs text-gray-500">
												{t("invitations.invitedOn", {
													date: new Date(inv.createdOn).toLocaleDateString(
														locale,
													),
												})}
											</p>
										</div>
										<div className="flex shrink-0 gap-2">
											<button
												type="button"
												onClick={() => handleAcceptInvitation(inv.id)}
												disabled={
													acceptingId === inv.id || decliningId === inv.id
												}
												className="rounded-md bg-brand-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-800 disabled:opacity-50"
											>
												{acceptingId === inv.id
													? t("invitations.accepting")
													: t("invitations.accept")}
											</button>
											<button
												type="button"
												onClick={() => handleDeclineInvitation(inv.id)}
												disabled={
													acceptingId === inv.id || decliningId === inv.id
												}
												className="rounded-md border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
											>
												{decliningId === inv.id
													? t("invitations.declining")
													: t("invitations.decline")}
											</button>
										</div>
									</div>
								</li>
							))}
						</ul>
					)}
				</>
			)}

			{/* Dialogs / Modals */}
			{showCreateOrgModal && (
				<CreateOrganizationModal
					onClose={() => setShowCreateOrgModal(false)}
					onSuccess={() => setShowCreateOrgModal(false)}
				/>
			)}

			{showDeleteDialog && (
				<ConfirmDialog
					title={t("account.deleteConfirmTitle")}
					message={t("account.deleteConfirmMessage")}
					confirmLabel={t("account.deleteConfirmButton")}
					onConfirm={handleDeleteAccount}
					onClose={() => {
						setShowDeleteDialog(false);
						setDeleteError(null);
					}}
					loading={deleting}
					error={deleteError}
				/>
			)}

			{confirmWithdrawId && (
				<ConfirmDialog
					title={t("confirmDialog.withdraw.title")}
					message={t("confirmDialog.withdraw.message")}
					confirmLabel={t("confirmDialog.withdraw.confirm")}
					onConfirm={handleWithdrawConfirm}
					onClose={handleWithdrawClose}
					loading={withdrawing}
					error={withdrawError}
				/>
			)}

			{checkInEngagement && (
				<CheckInModal
					engagementId={checkInEngagement.id}
					opportunityId={checkInEngagement.opportunityId}
					onCheckedIn={handleCheckedIn}
					onClose={() => setCheckInEngagement(null)}
				/>
			)}

			{feedbackEngagement && (
				<SubmitFeedbackModal
					engagementId={feedbackEngagement.id}
					opportunityTitle={feedbackEngagement.opportunityTitle}
					onSubmitted={handleFeedbackSubmitted}
					onClose={() => setFeedbackEngagement(null)}
				/>
			)}

			{shareModalOpen && (
				<ShareAchievementsModal
					shareUrl={shareUrl}
					onClose={() => setShareModalOpen(false)}
				/>
			)}
		</>
	);
}
