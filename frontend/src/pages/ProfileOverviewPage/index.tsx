import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import type { MyProfileResponse, StreakSummary } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { usePageTitle } from "../../hooks/usePageTitle";
import { usePageToolbar } from "../../contexts/ToolbarContext";
import { useEditModeQuickActions } from "../../hooks/useEditModeQuickActions";
import { inputClass, textareaClass } from "../../lib/formClasses";
import Dropdown from "../../components/Dropdown";
import ProfileFieldsView from "../../components/ProfileFieldsView";
import Skeleton from "../../components/Skeleton";
import ErrorBanner from "../../components/ErrorBanner";
import AchievementsSection from "./AchievementsSection";
import ActivitySection from "./ActivitySection";
import DangerZoneCard from "./DangerZoneCard";
import { useProfileForm, type ContactPref } from "./useProfileForm";
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

export default function ProfileOverviewPage() {
	const auth = useAuth();
	const api = useApiClient();
	const { t } = useTranslation();
	const [searchParams] = useSearchParams();
	usePageTitle(t("profile.title"));
	usePageToolbar([{ label: t("breadcrumb.profile") }]);

	const accessToken = auth.user?.access_token;

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
	}, [accessToken]);

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
	useEffect(() => {
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
	}, []);

	async function handleSave(e: React.FormEvent) {
		e.preventDefault();
		setSaving(true);
		setProfileError(null);
		setSuccessMessage(null);
		try {
			await api.updateUserProfile({
				firstName: form.state.firstName || undefined,
				lastName: form.state.lastName || undefined,
				bio: form.state.bio || undefined,
				skills: form.state.skills,
				languages: form.state.languages,
				preferredContact: form.state.preferredContact || undefined,
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

	return (
		<>
			<h1 className="mb-6 text-2xl font-bold text-gray-900">
				{t("profile.title")}
			</h1>

			{profileLoading && (
				<div
					className="mb-6 flex items-center gap-4 rounded-2xl border border-gray-100 bg-gray-50 px-4 py-4"
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
					{successMessage && (
						<div className="mb-4 rounded-md bg-green-50 px-4 py-3 text-sm text-green-700">
							{successMessage}
						</div>
					)}

					{/* Identity + momentum hero */}
					{!editing && (
						<div className="mb-6 flex flex-col gap-4 rounded-2xl border border-gray-100 bg-gray-50 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
							<div className="flex items-center gap-4">
								{avatarUrl ? (
									<img
										src={avatarUrl}
										alt=""
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
									<div className="flex items-center gap-3 rounded-xl border border-gray-100 bg-white px-4 py-3">
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
									<div className="flex items-center gap-3 rounded-xl border border-gray-100 bg-white px-4 py-3">
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
						</div>
					)}

					{/* Profile details */}
					<section className="mb-6">
						<h2 className="mb-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
							{t("profile.sectionDetails")}
						</h2>

						{!editing && (
							<ProfileFieldsView
								bio={form.state.bio}
								skills={form.state.skills}
								languages={form.state.languages}
								preferredContact={form.state.preferredContact || null}
							/>
						)}

						{editing && (
							<form ref={formRef} onSubmit={handleSave} className="space-y-6">
								<div>
									<h3 className="mb-4 text-sm font-semibold text-gray-900">
										{t("account.title")}
									</h3>
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
														{profile?.username?.charAt(0).toUpperCase() ?? "?"}
													</span>
												)}
												<div>
													<label
														htmlFor="avatar-upload"
														className={`cursor-pointer rounded-md border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 ${avatarUpload.uploading ? "opacity-50 pointer-events-none" : ""}`}
													>
														{avatarUpload.uploading
															? t("profile.avatarUploading")
															: t("profile.avatarUpload")}
													</label>
													<input
														ref={avatarUpload.inputRef}
														id="avatar-upload"
														type="file"
														accept="image/jpeg,image/png,image/webp"
														className="sr-only"
														onChange={avatarUpload.handleChange}
														disabled={avatarUpload.uploading}
													/>
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
													value={form.state.firstName}
													onChange={(e) => form.setFirstName(e.target.value)}
													className={inputClass}
												/>
											</Field>

											<Field label={t("account.fieldLastName")} id="last-name">
												<input
													id="last-name"
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

			<DangerZoneCard />
		</>
	);
}
