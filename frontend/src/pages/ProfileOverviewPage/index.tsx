import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams, Link } from "react-router";
import { useAuth } from "react-oidc-context";
import { Trans, useTranslation } from "react-i18next";
import type { MyProfileResponse, StreakSummary } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { usePageTitle } from "../../hooks/usePageTitle";
import { inputClass, labelClass, textareaClass } from "../../lib/formClasses";
import { cardClass, cardSubtleClass } from "../../lib/surfaceClasses";
import { IMAGE_UPLOAD_ACCEPT, getImageUploadHint } from "../../lib/imageUpload";
import { getInitials } from "../../lib/initials";
import Chip, { type ChipTone } from "../../components/Chip";
import Dropdown from "../../components/Dropdown";
import EmptyState from "../../components/EmptyState";
import ProfileFieldsView from "../../components/ProfileFieldsView";
import ProfileSubNav from "../../components/ProfileSubNav";
import PageHeaderBand from "../../components/PageHeaderBand";
import SectionHeading from "../../components/SectionHeading";
import Skeleton from "../../components/Skeleton";
import LoadMoreError from "../../components/LoadMoreError";
import SuccessBanner from "../../components/SuccessBanner";
import ImageCropModal from "../../components/ImageCropModal";
import FileUploadButton from "../../components/FileUploadButton";
import Field from "../../components/Field";
import Button from "../../components/Button";
import { CheckIcon, PencilIcon } from "../../components/icons";
import AchievementsSection from "./AchievementsSection";
import {
	useProfileForm,
	type ContactPref,
	type PreferredLanguage,
} from "./useProfileForm";
import { useAvatarUpload } from "./useAvatarUpload";

// Legacy ?tab=achievements deep link - from the pre-#794 two-tab scheme
// (profile/activity) and the older four-tab scheme still used by the
// /achievements redirect in App.tsx - scrolls to the Badges section, which
// still lives here, instead of switching tabs. "profile" has no entry: that
// content is already the top of the page.
const LEGACY_SCROLL_SECTIONS: Record<string, string> = {
	achievements: "achievements",
};

// Legacy ?tab= values for content that #1684 split off this page entirely -
// "activity"/"engagements" are the older aliases the /my-signups redirect
// used to produce; "invitations" is what backend-generated notification
// action URLs still send (NotificationReadRepository.cs). All three now
// redirect to the dedicated page instead of scrolling to a section that no
// longer exists on /profile.
const LEGACY_REDIRECT_TABS = new Set([
	"activity",
	"engagements",
	"invitations",
]);

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
	const { t, i18n } = useTranslation();
	const [searchParams] = useSearchParams();
	const navigate = useNavigate();
	usePageTitle(t("profile.title"));

	const [profile, setProfile] = useState<MyProfileResponse | null>(null);
	const [profileLoading, setProfileLoading] = useState(true);
	const [saving, setSaving] = useState(false);
	const [profileError, setProfileError] = useState<string | null>(null);
	// Manual retry after the mount effect's own automatic backoff (below) is
	// exhausted - distinct from profileLoading so retrying doesn't flip the
	// page back to the full-page skeleton (see LoadMoreError.tsx's rationale).
	const [retryingProfileLoad, setRetryingProfileLoad] = useState(false);
	const [successMessage, setSuccessMessage] = useState<string | null>(null);
	const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
	const [editing, setEditing] = useState(false);
	const [streaks, setStreaks] = useState<StreakSummary | null>(null);
	const [engagementCount, setEngagementCount] = useState<number | null>(null);
	const formRef = useRef<HTMLFormElement>(null);
	// Guards state updates from a stale in-flight request (initial load or a
	// later manual retry) after the component has unmounted.
	const profileLoadCancelledRef = useRef(false);

	const form = useProfileForm(profile);
	const avatarUpload = useAvatarUpload(setAvatarUrl);

	// Load profile data (always load on mount with retry). Depends on
	// auth.isAuthenticated, not on the access token itself - react-oidc-context's
	// automaticSilentRenew mints a fresh access token every ~4 minutes, and this
	// effect previously kept re-running on that token's identity (#1221),
	// re-triggering form.reset() and discarding whatever the user was mid-typing.
	// ProtectedRoute only ever mounts this page while isAuthenticated is already
	// true, so in practice this now runs once per mount.
	// Retries with backoff (attempt starts fresh at 0 on every call, so a
	// manual retry - see handleRetryProfileLoad below - re-runs the full
	// backoff sequence from attempt 1 same as the initial mount fetch) before
	// giving up and setting profileError.
	async function loadProfile() {
		const retryDelaysMs = [500, 1000, 2000];
		for (let attempt = 0; ; attempt++) {
			try {
				const data = await api.getUserProfile();
				if (profileLoadCancelledRef.current) return;
				setProfile(data);
				form.reset(data);
				setAvatarUrl(data.avatarUrl ?? null);
				setProfileError(null);
				return;
			} catch {
				if (profileLoadCancelledRef.current) return;
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

	useEffect(() => {
		profileLoadCancelledRef.current = false;
		setProfileLoading(true);
		loadProfile().finally(() => {
			if (!profileLoadCancelledRef.current) setProfileLoading(false);
		});

		return () => {
			profileLoadCancelledRef.current = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [auth.isAuthenticated]);

	// Manual retry once the automatic backoff above has given up and
	// profileError is set - re-runs the whole load sequence from attempt 1.
	function handleRetryProfileLoad() {
		setRetryingProfileLoad(true);
		loadProfile().finally(() => setRetryingProfileLoad(false));
	}

	// Stat chips in the identity hero - a lightweight, non-critical display,
	// so a failed fetch is silently ignored rather than surfaced.
	//
	// The headline stat is confirmed opportunities, not the login streak this
	// used to lead with: a volunteering platform that puts "days in a row you
	// opened the app" first is rewarding the wrong thing, and on a new account
	// it opened with a 0. The activity streak stays as an equal-weight chip
	// (it counts weeks with real activity); the login streak is back too, but
	// only as a small secondary line below the chips (#1848) - just enough for
	// the "On a Roll" badge to have a visible progress metric, without
	// re-promoting it to a headline stat.
	useEffect(() => {
		api
			.getMyStreaks()
			.then(setStreaks)
			.catch(() => {});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	const userId = auth.user?.profile?.sub;
	useEffect(() => {
		if (!userId) return;
		api
			.getPublicUserProfile(userId)
			.then((p) => setEngagementCount(p.engagementCount))
			.catch(() => {});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [userId]);

	// Legacy ?tab= deep links that #1684 moved off this page entirely
	// (invitations/sign-ups now live at /my-signups) redirect there
	// immediately rather than waiting on profileLoading - there's no section
	// left on this page to scroll to.
	useEffect(() => {
		if (LEGACY_REDIRECT_TABS.has(searchParams.get("tab") ?? "")) {
			navigate("/my-signups", { replace: true });
		}
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [searchParams]);

	// Legacy ?tab=achievements deep link (App.tsx's /achievements redirect
	// still lands here with it) scrolls to the section that now contains that
	// content instead of switching tabs. Gated on profileLoading/streaks
	// rather than firing once on mount ([]): the identity hero and "Profile
	// details" section below it only render their full height once those
	// finish loading, so scrolling before then targets a layout that's about
	// to shift and never re-fires afterward.
	useEffect(() => {
		if (profileLoading) return;
		const sectionId = LEGACY_SCROLL_SECTIONS[searchParams.get("tab") ?? ""];
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

	// Edit/Save/Cancel live in this section's own header, not in the page
	// chrome. They used to be published through QuickActionsContext so the
	// header band could render them, which meant the account area ran two
	// different editing paradigms side by side: /profile toggled a page-wide
	// mode from a button in the hero, while /profile/settings saved inline
	// from a button next to the fields it saves. This page now does what
	// Settings does - the controls sit with the content they act on.

	// ProfileFieldsView renders nothing at all once every field below is
	// empty (a fresh account has none of them set) - previously that left the
	// "Profile details" heading sitting over a blank gap on every new user's
	// very first view of this page (#985).
	//
	// preferredLanguage is deliberately not part of this check: it always has
	// a value (it defaults), so counting it meant a profile with nothing else
	// filled in still rendered a full card wrapping one row - "Email language:
	// English" - instead of the empty state that invites you to fill the
	// profile in.
	const isProfileFieldsEmpty =
		!form.state.bio &&
		form.state.skills.length === 0 &&
		form.state.languages.length === 0 &&
		!form.state.preferredContact &&
		!form.state.phone;

	// Same name shown in the header account button, so its initials must be
	// derived the same way - this used to fall back to a bare `charAt(0)`,
	// which showed a one-letter avatar here against the header's two-letter
	// "VV" for the same user (#1896).
	const displayName =
		form.state.firstName || form.state.lastName
			? `${form.state.firstName} ${form.state.lastName}`.trim()
			: (profile?.username ?? "");

	return (
		// max-w-5xl (#1755): unconstrained this inherited <main>'s 90rem, which
		// stretched the identity band to ~1376x130 around two lines of text and
		// pulled the six badges out to 160px-wide slivers. The content is a
		// person, a couple of stats and six badges - it needs a column, not the
		// full page.
		<>
			<PageHeaderBand
				eyebrow={t("profile.eyebrow")}
				title={t("profile.title")}
				compactTitle
			/>

			<div
				data-content-wrapper
				className="mx-auto grid max-w-5xl gap-8 lg:grid-cols-[11rem_minmax(0,1fr)] lg:gap-12"
			>
				<ProfileSubNav active="profile" />
				<div className="min-w-0">
					{profileLoading && (
						<div
							className={`mb-6 flex items-center gap-4 ${cardSubtleClass}`}
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
								<LoadMoreError
									message={profileError}
									retrying={retryingProfileLoad}
									onRetry={handleRetryProfileLoad}
								/>
							)}
							{/* Always mounted (not conditional on `successMessage`) so the live
					region is registered before it ever gets content - see
					CheckInModal.tsx's identical pattern for why. SuccessBanner itself
					collapses to sr-only when `message` is empty, so this stays mounted
					across the toggle. */}
							<SuccessBanner message={successMessage} className="mb-4" />

							{/* Identity + momentum hero. Was a gray-50 panel (#1755): the
					person is the subject of this page, and rendering their own
					name, handle and avatar as a grey utility strip - the largest
					flat surface on the page - was most of why it read as dead.
					Pale-mint brand stage instead, the same tier the landing page's
					founder band uses, with the avatar ringed in white so it reads
					as a portrait rather than another tile. */}
							{!editing && (
								<div className="mb-8 flex flex-col gap-5 rounded-card bg-brand-100 p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
									<div className="flex items-center gap-4">
										{avatarUrl ? (
											<img
												src={avatarUrl}
												alt=""
												width={72}
												height={72}
												className="h-18 w-18 shrink-0 rounded-full object-cover ring-3 ring-white"
											/>
										) : (
											<span className="flex h-18 w-18 shrink-0 items-center justify-center rounded-full bg-white text-2xl font-semibold text-brand-700 ring-3 ring-white">
												{getInitials(displayName)}
											</span>
										)}
										<div className="min-w-0">
											<p className="font-display text-3xl font-bold text-gray-900">
												{displayName}
											</p>
											<p className="text-sm text-brand-800">
												@{profile?.username}
											</p>
											<p className="truncate text-sm text-brand-800">
												{profile?.email}
											</p>
										</div>
									</div>

									<div className="flex flex-wrap gap-3">
										{engagementCount !== null && (
											<div
												data-testid="profile-stat-engagements"
												className="flex items-center gap-3 rounded-card border border-gray-100 bg-white px-4 py-3"
											>
												<span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-100 text-brand-700">
													<CheckIcon className="h-5 w-5" />
												</span>
												<div>
													<p className="text-xl font-bold text-gray-900">
														{engagementCount}
													</p>
													<p className="text-xs text-gray-500">
														{t("achievements.engagementStatLabel", {
															count: engagementCount,
														})}
													</p>
												</div>
											</div>
										)}
										{streaks &&
											(streaks.activityStreak > 0 ||
												streaks.loginStreak > 0) && (
												// Grouped in one flex-col unit instead of two siblings of
												// the engagement chip, so the login-streak caption stacks
												// directly under the stat pill it explains instead of
												// landing on its own at the far right of the row at
												// sm:justify-between widths, disconnected from the streak
												// it describes (#1892).
												<div className="flex flex-col gap-1.5">
													{streaks.activityStreak > 0 && (
														<div
															data-testid="profile-stat-streak"
															className="flex items-center gap-3 rounded-card border border-gray-100 bg-white px-4 py-3"
														>
															<span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-100 text-brand-700">
																<FireIcon />
															</span>
															<div>
																<p className="text-xl font-bold text-gray-900">
																	{streaks.activityStreak}
																</p>
																<p className="text-xs text-gray-500">
																	{t("achievements.activityStreak", {
																		count: streaks.activityStreak,
																		badge: t(
																			"achievements.badges.weekly-hero-4.name",
																		),
																	})}
																</p>
															</div>
														</div>
													)}
													{streaks.loginStreak > 0 && (
														<p
															data-testid="profile-stat-login-streak"
															className="flex items-center gap-1.5 text-xs text-brand-800"
														>
															<FireIcon className="h-3.5 w-3.5" />
															{t("achievements.loginStreak", {
																count: streaks.loginStreak,
																badge: t(
																	"achievements.badges.on-a-roll-7.name",
																),
															})}
														</p>
													)}
												</div>
											)}
									</div>
								</div>
							)}

							<section className="mb-10">
								<div className="flex items-center justify-between gap-3">
									<SectionHeading>{t("profile.sectionDetails")}</SectionHeading>
									{!editing ? (
										<Button
											type="button"
											variant="outline"
											size="sm"
											onClick={() => setEditing(true)}
											data-testid="profile-edit"
											className="shrink-0"
										>
											<PencilIcon className="h-4 w-4" />
											{t("common.edit")}
										</Button>
									) : (
										<div className="flex shrink-0 items-center gap-2">
											<Button
												type="button"
												variant="outline"
												size="sm"
												onClick={handleCancel}
												disabled={saving}
												data-testid="profile-cancel"
											>
												{t("common.cancel")}
											</Button>
											<Button
												type="button"
												size="sm"
												disabled={saving}
												data-testid="profile-save"
												// Goes through the form's native submit (not
												// handleSave() directly) so the browser still runs
												// constraint validation and focuses/announces the
												// offending field, same as pressing Enter in the form.
												onClick={() => formRef.current?.requestSubmit()}
											>
												<CheckIcon className="h-4 w-4" />
												{saving ? t("common.saving") : t("common.save")}
											</Button>
										</div>
									)}
								</div>

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
										// Boxed, like the identity band above and the badges
										// below: as bare label/value pairs on white this was the
										// one section on the page with no surface of its own.
										<div className={`${cardClass} sm:p-6`}>
											<ProfileFieldsView
												bio={form.state.bio}
												skills={form.state.skills}
												languages={form.state.languages}
												preferredContact={form.state.preferredContact || null}
												phone={form.state.phone || null}
												preferredLanguage={form.state.preferredLanguage}
											/>
										</div>
									))}

								{/* Small print under the fields, not a lead paragraph above
						them (#1755): as the first thing in the section it made a
						privacy footnote look like the section's actual content, on a
						page where the section otherwise holds very little.
						Suppressed over the empty state too: the notice names the
						picture, bio, skills and languages that will be public, and
						reading that over a card saying you have not filled any of
						them in described fields that were not on screen. */}
								{!editing && !isProfileFieldsEmpty && (
									<p className="mt-4 text-xs text-gray-500">
										<Trans
											i18nKey="profile.publicProfileNotice"
											components={{
												privacyLink: (
													<Link to="/privacy-policy" className="underline" />
												),
											}}
										/>
									</p>
								)}

								{editing && (
									<form
										ref={formRef}
										onSubmit={handleSave}
										className="space-y-6"
									>
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
																{getInitials(displayName)}
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
																	accept={IMAGE_UPLOAD_ACCEPT}
																	onChange={avatarUpload.handleChange}
																	disabled={
																		avatarUpload.uploading ||
																		avatarUpload.removing
																	}
																	inputRef={avatarUpload.inputRef}
																	ariaDescribedBy={
																		avatarUpload.error
																			? "avatar-upload-error"
																			: undefined
																	}
																/>
																{avatarUrl && (
																	<button
																		type="button"
																		data-testid="avatar-remove"
																		onClick={() =>
																			void avatarUpload.handleRemove()
																		}
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
																{getImageUploadHint(t, i18n.language)}
															</p>
															{avatarUpload.error && (
																<p
																	id="avatar-upload-error"
																	className="mt-1 text-xs text-red-600"
																	role="alert"
																>
																	{avatarUpload.error}
																</p>
															)}
														</div>
													</div>
												</div>

												<div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
													<Field
														label={t("account.fieldUsername")}
														id="username"
													>
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
															onChange={(e) =>
																form.setFirstName(e.target.value)
															}
															className={inputClass}
														/>
													</Field>

													<Field
														label={t("account.fieldLastName")}
														id="last-name"
													>
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

											<Field
												label={t("profile.fieldLanguages")}
												id="lang-input"
											>
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

					{/* Mounted unconditionally (not gated behind profileLoading) so its own
			independent data fetch starts immediately, and so its section id
			exists right away for the legacy ?tab=achievements scroll-to-section
			effect above regardless of how long the profile fetch takes. */}
					<AchievementsSection />
				</div>
			</div>

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
