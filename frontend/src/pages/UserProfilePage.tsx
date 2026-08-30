import { useEffect, useState } from "react";
import { useLocation, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type {
	BadgeCatalogEntry,
	PublicUserProfileResponse,
} from "../client/api-client";
import BadgeGrid from "../components/BadgeGrid";
import EmptyState from "../components/EmptyState";
import PageHeaderBand from "../components/PageHeaderBand";
import ProfileFieldsView from "../components/ProfileFieldsView";
import ReportFlagButton from "../components/ReportFlagButton";
import SectionHeading from "../components/SectionHeading";
import Skeleton from "../components/Skeleton";
import DetailLoadFailure from "../components/DetailLoadFailure";
import { useApiClient } from "../hooks/useApiClient";
import { useOnlineStatus } from "../hooks/useOnlineStatus";
import { usePageTitle } from "../hooks/usePageTitle";
import {
	classifyLoadFailure,
	getApiErrorMessage,
	type LoadFailureKind,
} from "../lib/apiError";
import { getInitials } from "../lib/initials";
import {
	reportIntentSigninArgs,
	usePendingReportIntent,
} from "../lib/reportIntent";

export default function UserProfilePage() {
	const { userId } = useParams<{ userId: string }>();
	const location = useLocation();
	const pendingReportTargetId = usePendingReportIntent();
	const { t } = useTranslation();
	const api = useApiClient();
	const auth = useAuth();

	const [profile, setProfile] = useState<PublicUserProfileResponse | null>(
		null,
	);
	const [catalog, setCatalog] = useState<BadgeCatalogEntry[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [failure, setFailure] = useState<LoadFailureKind | null>(null);
	const online = useOnlineStatus();

	// A resolved-but-absent profile is the same dead end as a 404.
	const failureKind = loading
		? null
		: (failure ?? (profile ? null : "notFound"));

	// `null` while a failure is on screen: DetailLoadFailure sets the title
	// itself, and letting the loading placeholder win would leave the tab
	// claiming the page is still loading (#2320).
	usePageTitle(
		failureKind ? null : (profile?.displayName ?? t("userProfile.loading")),
	);

	function load() {
		setLoading(true);
		if (!userId) {
			setFailure("notFound");
			setLoading(false);
			return Promise.resolve();
		}
		setError(null);
		setFailure(null);
		return Promise.all([
			api.getPublicUserProfile(userId),
			api.getBadgeCatalog(),
		])
			.then(([prof, cat]) => {
				setProfile(prof);
				setCatalog(cat);
				setError(null);
				setFailure(null);
			})
			.catch((err) => {
				setError(getApiErrorMessage(err, t("error.serverError")));
				setFailure(classifyLoadFailure(err, online));
			})
			.finally(() => setLoading(false));
	}

	useEffect(() => {
		void load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [userId]);

	if (loading)
		return (
			<div role="status">
				<span className="sr-only">{t("userProfile.loading")}</span>
				<div className="mb-8 flex items-center gap-4">
					<Skeleton className="h-16 w-16 shrink-0 rounded-full" />
					<div className="space-y-2">
						<Skeleton className="h-6 w-40" />
						<Skeleton className="h-4 w-32" />
					</div>
				</div>
				<Skeleton className="mb-4 h-4 w-32" />
				<div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
					{Array.from({ length: 6 }).map((_, i) => (
						<Skeleton key={i} className="h-28 w-full" />
					))}
				</div>
			</div>
		);
	if (failureKind || !profile)
		return (
			<DetailLoadFailure
				kind={failureKind ?? "notFound"}
				notFoundTitle={t("userProfile.notFoundTitle")}
				notFoundMessage={t("userProfile.notFoundMessage")}
				errorMessage={error ?? t("error.serverError")}
				onRetry={() => void load()}
				action={{ label: t("notFound.backHome"), to: "/" }}
				data-testid="user-profile-load-failure"
			/>
		);

	const isProfileEmpty =
		!profile.bio &&
		profile.skills.length === 0 &&
		profile.languages.length === 0;

	return (
		<>
			<PageHeaderBand
				eyebrow={t("userProfile.eyebrow")}
				title={profile.displayName}
			/>

			<div className="max-w-5xl">
				<div className="mb-8 flex items-center gap-4">
					{profile.avatarUrl ? (
						<img
							src={profile.avatarUrl}
							alt=""
							width={64}
							height={64}
							className="h-16 w-16 shrink-0 rounded-full object-cover ring-2 ring-brand-100"
						/>
					) : (
						<span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-brand-100 text-2xl font-semibold text-brand-700">
							{getInitials(profile.displayName)}
						</span>
					)}
					<p className="text-sm text-gray-600">
						{t("userProfile.engagementCount", {
							count: profile.engagementCount,
						})}
					</p>
					{/* Offered to anonymous visitors too, routed through sign-in with the click
					carried along - the same treatment every other report affordance gets
					(#2326). Still hidden on your own profile. */}
					{auth.user?.profile.sub !== userId && userId && (
						<ReportFlagButton
							targetLabel={profile.displayName}
							ariaLabel={t("userProfile.reportUser")}
							onReport={async (reason, details) => {
								await api.reportUser(userId, {
									reason,
									details: details || undefined,
								});
							}}
							onRequireSignIn={
								auth.isAuthenticated
									? undefined
									: () =>
											void auth.signinRedirect(
												reportIntentSigninArgs(
													location.pathname,
													location.search,
													userId,
												),
											)
							}
							autoOpen={
								auth.isAuthenticated && pendingReportTargetId === userId
							}
							className="relative z-20 ml-auto inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-gray-600 transition-colors hover:bg-gray-100 hover:text-gray-800"
						/>
					)}
				</div>

				<div data-content-wrapper className="mb-8 max-w-2xl">
					{isProfileEmpty ? (
						<EmptyState
							title={t("userProfile.emptyStateTitle")}
							message={t("userProfile.emptyStateMessage")}
						/>
					) : (
						<ProfileFieldsView
							bio={profile.bio}
							skills={profile.skills}
							languages={profile.languages}
						/>
					)}
				</div>

				<section>
					<SectionHeading>{t("achievements.badgesTitle")}</SectionHeading>
					<BadgeGrid
						earned={profile.badges}
						catalog={catalog}
						loading={false}
					/>
				</section>
			</div>
		</>
	);
}
