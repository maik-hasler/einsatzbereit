import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import type { MyProfileResponse, StreakSummary } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { usePageTitle } from "../../hooks/usePageTitle";
import { usePageToolbar } from "../../contexts/ToolbarContext";
import { useEditModeQuickActions } from "../../hooks/useEditModeQuickActions";
import { inputClass, labelClass, textareaClass } from "../../lib/formClasses";
import { pageTitleClass } from "../../lib/headingClasses";
import Chip, { type ChipTone } from "../../components/Chip";
import Dropdown from "../../components/Dropdown";
import EmptyState from "../../components/EmptyState";
import ProfileFieldsView from "../../components/ProfileFieldsView";
import Skeleton from "../../components/Skeleton";
import ErrorBanner from "../../components/ErrorBanner";
import ImageCropModal from "../../components/ImageCropModal";
import FileUploadButton from "../../components/FileUploadButton";
import Field from "../../components/Field";
import AchievementsSection from "./AchievementsSection";
import ActivitySection from "./ActivitySection";
import NotificationPreferencesSection from "./NotificationPreferencesSection";
import DangerZoneCard from "./DangerZoneCard";
import {
	useProfileForm,
	type ContactPref,
	type PreferredLanguage,
} from "./useProfileForm";
import { useAvatarUpload } from "./useAvatarUpload";

// Legacy ?tab= values - from the pre-#794 two-tab scheme (profile/activity)
// and the older four-tab scheme still used by the /my-engagements and
// /achievements redirects in App.tsx - resolve to the section that now
// contains that content, scrolled into view instead of switching tabs.
// "profile" has no entry: that content is already the top of the page.
const LEGACY_TAB_SECTIONS: Record<string, string> = {
	activity: "activity",
	engagements: "activity",
	invitations: "activity",
	achievements: "achievements",
};

function FireIcon({ className = "h-5 w-5" }: { className?: string }) {
	return (
		<svg
			className={className}
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth={1.5}
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M15.362 5.214A8.252 8.252 0 0 1 12 21 8.25 8.25 0 0 1 6.038 7.047 8.287 8.287 0 0 0 9 9.601a8.983 8.983 0 0 1 3.361-6.867 8.21 8.21 0 0 0 3 2.48Z"
			/>
		</svg>
	);
}

function CalendarIcon({ className = "h-5 w-5" }: { className?: string }) {
	return (
		<svg
			className={className}
			fill="none"
			viewBox="0 0 24 24"
			strokeWidth={1.5}
			stroke="currentColor"
			aria-hidden="true"
		>
			<path
				strokeLinecap="round"
				strokeLinejoin="round"
				d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5"
			/>
		</svg>
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
	tone = "brand",
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
	tone?: ChipTone;
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
						<Chip
							key={chip}
							tone={tone}
							onRemove={() => onRemove(chip)}
							removeLabel={`${removeLabel} ${chip}`}
						>
							{chip}
						</Chip>
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

export default function ProfileOverviewPage() {
	const auth = useAuth();
	const api = useApiClient();
	const { t } = useTranslation();
	const [searchParams] = useSearchParams();
	usePageTitle(t("profile.title"));
	usePageToolbar([{ label: t("breadcrumb.profile") }]);

	const [profile, setProfile] = useState<MyProfileResponse | null>(null);
	const [profileLoading, setProfileLoading] = useState(true);
	const [saving, setSaving] = useState(false);
	const [profileError, setProfileError] = useState<string | null>(null);
	const [successMessage, setSuccessMessage] = useState<string | null>(null);
	const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
	const [editing, setEditing] = useState(false);
	const [streaks, setStreaks] = useState<StreakSummary | null>(null);
	const formRef = useRef<HTMLFormElement>(null);

	const form = useProfileForm(profile);
	const avatarUpload = useAvatarUpload(setAvatarUrl);

	// Load profile data (always load on mount with retry). Depends on
	// auth.isAuthenticated, not on the access token itself - react-oidc-context's
	// automaticSilentRenew mints a fresh access token every ~4 minutes, and this
	// effect previously kept re-running on that token's identity (#1221),
	// re-triggering form.reset() and discarding whatever the user was mid-typing.
	// ProtectedRoute only ever mounts this page while isAuthenticated is already
	// true, so in practice this now runs once per mount.
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
					form.reset(data);
					setAvatarUrl(data.avatarUrl ?? null);
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
	}, [auth.isAuthenticated]);

	// Streak chips in the identity hero - a lightweight, non-critical stat
	// display, so a failed fetch is silently ignored rather than surfaced.
	useEffect(() => {
		api
			.getMyStreaks()
			.then(setStreaks)
			.catch(() => {});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	// Legacy ?tab= deep links (App.tsx's /my-engagements and /achievements
	// redirects still land here with ?tab=engagements/achievements) scroll to
	// the section that now contains that content instead of switching tabs.
	// Gated on profileLoading/streaks rather than firing once on mount ([]):
	// the identity hero and "Profile details" section below it only render
	// their full height once those finish loading, so scrolling before then
	// targets a layout that's about to shift and never re-fires afterward.
	useEffect(() => {
		if (profileLoading) return;
		const sectionId = LEGACY_TAB_SECTIONS[searchParams.get("tab") ?? ""];
		if (!sectionId) return;
		const reduceMotion = window.matchMedia(
			"(prefers-reduced-motion: reduce)",
		).matches;
		const frame = requestAnimationFrame(() => {
			document.getElementById(sectionId)?.scrollIntoView({
				block: "start",
				behavior: reduceMotion ? "auto" : "smooth",
			});
		});
		return () => cancelAnimationFrame(frame);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [profileLoading, streaks]);

	async function handleSave(e: React.FormEvent) {
		e.preventDefault();
		setSaving(true);
		setProfileError(null);
		setSuccessMessage(null);
		const savedValues = {
			firstName: form.state.firstName || undefined,
			lastName: form.state.lastName || undefined,
			bio: form.state.bio || undefined,
			phone: form.state.phone || undefined,
			skills: form.state.skills,
			languages: form.state.languages,
			preferredContact: form.state.preferredContact || undefined,
			preferredLanguage: form.state.preferredLanguage,
		};
		try {
			await api.updateUserProfile(savedValues);
			// Keeps `profile` (the source handleCancel's form.reset(profile) reads
			// from) in sync with what was just saved - otherwise re-entering edit
			// mode and cancelling would silently restore the pre-save values (#1247).
			setProfile((prev) => (prev ? { ...prev, ...savedValues } : prev));
			setSuccessMessage(t("profile.savedSuccess"));
			setEditing(false);
		} catch {
			setProfileError(t("profile.saveError"));
		} finally {
			setSaving(false);
		}
	}

	function handleCancel() {
		form.reset(profile);
		setProfileError(null);
		setEditing(false);
	}

	useEditModeQuickActions({
		editing,
		saving,
		onEdit: () => setEditing(true),
		// Goes through the form's native submit (not handleSave() directly) so
		// the browser still runs constraint validation and focuses/announces
		// the offending field, same as pressing Enter in the form used to.
		onSave: () => formRef.current?.requestSubmit(),
		onCancel: handleCancel,
	});

	// ProfileFieldsView renders nothing at all once every field below is
	// empty (a fresh account has none of them set) - previously that left the
	// "Profile Details" heading sitting over a blank gap on every new user's
	// very first view of this page (#985).
	const isProfileFieldsEmpty =
		!form.state.bio &&
		form.state.skills.length === 0 &&
		form.state.languages.length === 0 &&
		!form.state.preferredContact &&
		!form.state.phone &&
		!form.state.preferredLanguage;

	return (
		<>
			<h1 className={`mb-6 text-gray-900 ${pageTitleClass}`}>
				{t("profile.title")}
			</h1>

			{profileLoading && (
				<div
					className="mb-6 flex items-center gap-4 rounded-card border border-gray-100 bg-gray-50 px-4 py-4"
					role="status"
				>
					<span className="sr-only">{t("profile.loading")}</span>
					<Skeleton className="h-16 w-16 shrink-0 rounded-full" />
					<div className="flex-1 space-y-2">
						<Skeleton className="h-5 w-40" />
						<Skeleton className="h-4 w-24" />
					</div>
				</div>
			)}

			{!profileLoading && (
				<>
					{profileError && (
						<ErrorBanner message={profileError} className="mb-4" />
					)}
					{/* Always mounted (not conditional on `successMessage`) so the live
					region is registered before it ever gets content - see
					CheckInModal.tsx's identical pattern for why. */}
					<div
						role="status"
						className={
							successMessage
								? "mb-4 rounded-md bg-green-50 px-4 py-3 text-sm text-green-700"
								: "sr-only"
						}
					>
						{successMessage}
					</div>

					{/* Identity + momentum hero */}
					{!editing && (
						<div className="mb-6 flex flex-col gap-4 rounded-card border border-gray-100 bg-gray-50 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
							<div className="flex items-center gap-4">
								{avatarUrl ? (
									<img
										src={avatarUrl}
										alt=""
										width={64}
										height={64}
										className="h-16 w-16 shrink-0 rounded-full object-cover ring-2 ring-brand-100"
									/>
								) : (
									<span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-brand-100 text-2xl font-semibold text-brand-700">
										{profile?.username?.charAt(0).toUpperCase() ?? "?"}
									</span>
								)}
								<div>
									<p className="text-xl font-semibold text-gray-900">
										{form.state.firstName || form.state.lastName
											? `${form.state.firstName} ${form.state.lastName}`.trim()
											: profile?.username}
									</p>
									<p className="text-sm text-gray-500">@{profile?.username}</p>
									<p className="text-sm text-gray-500">{profile?.email}</p>
								</div>
							</div>

							{streaks && (
								<div className="flex flex-wrap gap-3">
									<div className="flex items-center gap-3 rounded-card border border-gray-100 bg-white px-4 py-3">
										<span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-100 text-brand-700">
											<FireIcon />
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
									<div className="flex items-center gap-3 rounded-card border border-gray-100 bg-white px-4 py-3">
										<span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-100 text-brand-700">
											<CalendarIcon />
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
						</div>
					)}

					{/* Profile details */}
					<section className="mb-6">
						<h2 className="mb-3 text-xs font-semibold tracking-wider text-gray-600 uppercase">
							{t("profile.sectionDetails")}
						</h2>

						{!editing &&
							(isProfileFieldsEmpty ? (
								<EmptyState
									title={t("profile.emptyStateTitle")}
									message={t("profile.emptyStateMessage")}
									action={{
										label: t("profile.emptyStateCta"),
										onClick: () => setEditing(true),
									}}
								/>
							) : (
								<ProfileFieldsView
									bio={form.state.bio}
									skills={form.state.skills}
									languages={form.state.languages}
									preferredContact={form.state.preferredContact || null}
									phone={form.state.phone || null}
									preferredLanguage={form.state.preferredLanguage}
								/>
							))}

						{editing && (
							<form ref={formRef} onSubmit={handleSave} className="space-y-6">
								<div>
									<h3 className="mb-4 text-sm font-semibold text-gray-900">
										{t("account.title")}
									</h3>
									<div className="space-y-5">
										<div>
											<p className={`mb-1 ${labelClass}`}>
												{t("profile.fieldAvatar")}
											</p>
											<div className="flex items-center gap-4">
												{avatarUrl ? (
													<img
														src={avatarUrl}
														alt=""
														width={64}
														height={64}
														className="h-16 w-16 rounded-full object-cover ring-2 ring-brand-100"
													/>
												) : (
													<span className="flex h-16 w-16 items-center justify-center rounded-full bg-brand-100 text-2xl font-semibold text-brand-700">
														{profile?.username?.charAt(0).toUpperCase() ?? "?"}
													</span>
												)}
												<div>
													<div className="flex items-center gap-3">
														<FileUploadButton
															id="avatar-upload"
															label={
																avatarUpload.uploading
																	? t("profile.avatarUploading")
																	: t("profile.avatarUpload")
															}
															accept="image/jpeg,image/png,image/webp"
															onChange={avatarUpload.handleChange}
															disabled={
																avatarUpload.uploading || avatarUpload.removing
															}
															inputRef={avatarUpload.inputRef}
														/>
														{avatarUrl && (
															<button
																type="button"
																data-testid="avatar-remove"
																onClick={() => void avatarUpload.handleRemove()}
																disabled={
																	avatarUpload.uploading ||
																	avatarUpload.removing
																}
																className="text-sm font-medium text-red-600 hover:underline disabled:cursor-not-allowed disabled:opacity-50"
															>
																{avatarUpload.removing
																	? t("profile.avatarRemoving")
																	: t("profile.avatarRemove")}
															</button>
														)}
													</div>
													<p className="mt-1 text-xs text-gray-500">
														{t("profile.avatarHint")}
													</p>
													{avatarUpload.error && (
														<p className="mt-1 text-xs text-red-600">
															{avatarUpload.error}
														</p>
													)}
												</div>
											</div>
										</div>

										<div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
											<Field label={t("account.fieldUsername")} id="username">
												<input
													id="username"
													disabled
													autoComplete="username"
													value={profile?.username ?? ""}
													className={`${inputClass} cursor-not-allowed bg-gray-50 text-gray-500`}
												/>
											</Field>

											<Field label={t("account.fieldEmail")} id="email">
												<input
													id="email"
													disabled
													type="email"
													autoComplete="email"
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
													autoComplete="given-name"
													value={form.state.firstName}
													onChange={(e) => form.setFirstName(e.target.value)}
													className={inputClass}
												/>
											</Field>

											<Field label={t("account.fieldLastName")} id="last-name">
												<input
													id="last-name"
													autoComplete="family-name"
													value={form.state.lastName}
													onChange={(e) => form.setLastName(e.target.value)}
													className={inputClass}
												/>
											</Field>
										</div>
									</div>
								</div>

								<hr className="border-gray-200" />

								<div className="space-y-5">
									<Field label={t("profile.fieldBio")} id="bio">
										<textarea
											id="bio"
											rows={4}
											value={form.state.bio}
											placeholder={t("profile.bioPlaceholder")}
											onChange={(e) => form.setBio(e.target.value)}
											className={textareaClass}
										/>
									</Field>

									<Field label={t("profile.fieldSkills")} id="skill-input">
										<ChipInput
											inputRef={form.skillInputRef}
											inputId="skill-input"
											chips={form.state.skills}
											inputValue={form.state.skillInput}
											placeholder={t("profile.skillsPlaceholder")}
											onInputChange={form.setSkillInput}
											onAdd={form.addSkill}
											onRemove={form.removeSkill}
											removeLabel={t("profile.removeChip")}
										/>
									</Field>

									<Field label={t("profile.fieldLanguages")} id="lang-input">
										<ChipInput
											inputRef={form.langInputRef}
											inputId="lang-input"
											chips={form.state.languages}
											inputValue={form.state.langInput}
											placeholder={t("profile.languagesPlaceholder")}
											onInputChange={form.setLangInput}
											onAdd={form.addLanguage}
											onRemove={form.removeLanguage}
											removeLabel={t("profile.removeChip")}
											tone="neutral"
										/>
									</Field>

									<Field label={t("profile.fieldPhone")} id="phone">
										<input
											id="phone"
											type="tel"
											autoComplete="tel"
											value={form.state.phone}
											placeholder={t("profile.phonePlaceholder")}
											onChange={(e) => form.setPhone(e.target.value)}
											className={inputClass}
										/>
									</Field>

									<Field
										label={t("profile.fieldPreferredContact")}
										id="preferred-contact"
									>
										<Dropdown
											id="preferred-contact"
											value={form.state.preferredContact}
											onChange={(v) =>
												form.setPreferredContact(v as ContactPref)
											}
											className={inputClass}
											options={[
												{
													value: "",
													label: t("profile.preferredContactNone"),
												},
												{
													value: "Email",
													label: t("profile.preferredContactEmail"),
												},
												{
													value: "Phone",
													label: t("profile.preferredContactPhone"),
												},
											]}
										/>
									</Field>

									<Field
										label={t("profile.fieldPreferredLanguage")}
										id="preferred-language"
									>
										<Dropdown
											id="preferred-language"
											value={form.state.preferredLanguage}
											onChange={(v) =>
												form.setPreferredLanguage(v as PreferredLanguage)
											}
											className={inputClass}
											options={[
												{
													value: "de",
													label: t("profile.preferredLanguageDe"),
												},
												{
													value: "en",
													label: t("profile.preferredLanguageEn"),
												},
											]}
										/>
									</Field>
								</div>
							</form>
						)}
					</section>
				</>
			)}

			{/* Mounted unconditionally (not gated behind profileLoading) so their
			own independent data fetches start immediately, and so their
			section ids exist right away for the legacy ?tab= scroll-to-section
			effect above regardless of how long the profile fetch takes. */}
			<AchievementsSection />
			<ActivitySection />

			<NotificationPreferencesSection />

			<DangerZoneCard />

			{avatarUpload.croppingFile && (
				<ImageCropModal
					file={avatarUpload.croppingFile}
					aspectRatio={1}
					shape="circle"
					outputWidth={320}
					outputHeight={320}
					title={t("profile.avatarUpload")}
					onCancel={avatarUpload.handleCropCancel}
					onCropped={(f) => void avatarUpload.handleCropped(f)}
				/>
			)}
		</>
	);
}
