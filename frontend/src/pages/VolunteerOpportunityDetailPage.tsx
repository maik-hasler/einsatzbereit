import { lazy, Suspense, useEffect, useRef, useState } from "react";
import { useParams, Link } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import type {
	PublicOrganizationProfileResponse,
	VolunteerOpportunityDetails,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { useOnlineStatus } from "../hooks/useOnlineStatus";
import {
	computeSpotsLeft,
	findNextTimeSlot,
	formatDate,
	formatDateTime,
	formatDateTimeRange,
	formatOccurrence,
	formatParticipationType,
	formatPostedAgo,
	isSlotFull,
	pickLocalizedText,
} from "../lib/format";
import {
	FEW_SPOTS_THRESHOLD,
	getCapacityFromTimeSlots,
	type OpportunityCapacity,
} from "../lib/opportunityCapacity";
import Chip from "../components/Chip";
import SectionHeading from "../components/SectionHeading";
import SignUpModal from "../components/SignUpModal";
import ReportContentModal, {
	type ReportReason,
} from "../components/ReportContentModal";
import ConfirmDialog from "../components/ConfirmDialog";
import Button from "../components/Button";
import Skeleton from "../components/Skeleton";
import LoadMoreError from "../components/LoadMoreError";
import ModalLoadingFallback from "../components/ModalLoadingFallback";
import PageHeaderBand from "../components/PageHeaderBand";
import OpportunityCard from "../components/OpportunityCard";
import RouteState from "../components/RouteState";
import WarningBanner from "../components/WarningBanner";
import { usePageTitle } from "../hooks/usePageTitle";
import { dispatchToast } from "../lib/toastBus";
import { getApiErrorMessage, isNetworkError } from "../lib/apiError";
import { signinLocaleArgs } from "../lib/authLocale";
import { cardClass } from "../lib/surfaceClasses";
import {
	ArrowTopRightOnSquareIcon,
	CalendarIcon,
	CheckIconSolid,
	ChevronRightIcon,
	EnvelopeIcon,
	FlagIcon,
	GlobeIcon,
	MapPinIcon,
	PhoneIcon,
	UserGroupIcon,
} from "../components/icons";

const SingleMarkerMap = lazy(() => import("../components/SingleMarkerMap"));

const CreateVolunteerOpportunityModal = lazy(
	() => import("../components/CreateVolunteerOpportunityModal"),
);

function describeCapacity(
	capacity: OpportunityCapacity,
	t: TFunction,
): { label: string; tone: string; secondaryLabel?: string } {
	switch (capacity.kind) {
		case "unlimited":
			return {
				label: t("opportunities.unlimitedSpots"),
				tone: "text-teal-700",
			};
		case "notApplicable":
			return {
				label: t("opportunities.byInterest"),
				tone: "text-gray-700",
				secondaryLabel:
					capacity.booked > 0
						? t("opportunities.participantsJoined", { count: capacity.booked })
						: undefined,
			};
		case "capped":
			if (capacity.isFull) {
				return { label: t("opportunities.full"), tone: "text-red-600" };
			}
			return capacity.spotsLeft <= FEW_SPOTS_THRESHOLD
				? {
						label: t("opportunities.fewSpotsLeft", {
							count: capacity.spotsLeft,
						}),
						tone: "text-orange-700",
					}
				: {
						label: t("opportunities.spotsLeft", { count: capacity.spotsLeft }),
						tone: "text-gray-700",
					};
	}
}

function slotCapacityLabel(
	slot: { maxParticipants?: number | undefined; bookedCount: number },
	t: TFunction,
): string {
	const spotsLeft = computeSpotsLeft(slot.maxParticipants, slot.bookedCount);
	if (spotsLeft === null) return t("opportunities.unlimitedSpots");
	return isSlotFull(slot.maxParticipants, slot.bookedCount)
		? t("opportunities.full")
		: t("opportunities.spotsLeft", { count: spotsLeft });
}

function describeWhenFact(
	opportunity: VolunteerOpportunityDetails,
	t: TFunction,
	lng: string,
): string {
	if (opportunity.participationType === "IndividualContact") {
		return opportunity.validUntil
			? t("opportunities.applyBy", {
					date: formatDate(opportunity.validUntil as unknown as string, lng),
				})
			: t("opportunities.flexibleDate");
	}

	const nextSlot = findNextTimeSlot(opportunity.timeSlots);
	return nextSlot
		? formatDateTime(nextSlot.startDateTime as unknown as string, lng)
		: t("opportunities.flexibleDate");
}

function describeHowFact(
	opportunity: VolunteerOpportunityDetails,
	t: TFunction,
): string {
	return opportunity.participationType === "ScheduledSlots"
		? t("opportunities.slotCount", { count: opportunity.timeSlots.length })
		: formatParticipationType(opportunity.participationType, t);
}

export default function VolunteerOpportunityDetailPage() {
	const { opportunityId } = useParams<{ opportunityId: string }>();
	const auth = useAuth();
	const api = useApiClient();
	const { t, i18n } = useTranslation();

	const [opportunity, setOpportunity] =
		useState<VolunteerOpportunityDetails | null>(null);
	usePageTitle(
		opportunity &&
			pickLocalizedText(opportunity.titleDe, opportunity.titleEn, i18n.language)
				.text,
	);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);

	const [errorIsNetworkFailure, setErrorIsNetworkFailure] = useState(false);
	const online = useOnlineStatus();
	const errorIsOffline = error !== null && (!online || errorIsNetworkFailure);
	const [showSignUp, setShowSignUp] = useState(false);

	const [preselectedSlotId, setPreselectedSlotId] = useState<
		string | undefined
	>(undefined);
	const [showReport, setShowReport] = useState(false);
	const [showWithdrawConfirm, setShowWithdrawConfirm] = useState(false);
	const [withdrawing, setWithdrawing] = useState(false);
	const [withdrawError, setWithdrawError] = useState<string | null>(null);
	const [showEditModal, setShowEditModal] = useState(false);
	const [publishing, setPublishing] = useState(false);

	const withdrawLimitWarningRef = useRef<HTMLParagraphElement>(null);
	const withdrawLimitWarningActive =
		showWithdrawConfirm &&
		opportunity?.currentUserEngagement?.remainingReactivations === 1;
	useEffect(() => {
		if (withdrawLimitWarningActive) withdrawLimitWarningRef.current?.focus();
	}, [showWithdrawConfirm, withdrawLimitWarningActive]);

	const isAuthenticated = auth.isAuthenticated;
	const roles = (
		Array.isArray(auth.user?.profile?.roles) ? auth.user?.profile?.roles : []
	) as string[];

	const isOrganisator = isAuthenticated && roles.includes("organisator");
	const [userOrgIds, setUserOrgIds] = useState<string[]>([]);

	useEffect(() => {
		if (!isOrganisator) return;
		api
			.getOrganizations()
			.then((orgs) => setUserOrgIds(orgs.map((o) => o.id)))
			.catch(() => {});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [isOrganisator]);

	const [orgProfile, setOrgProfile] =
		useState<PublicOrganizationProfileResponse | null>(null);
	const [orgProfileError, setOrgProfileError] = useState<string | null>(null);
	const [retryingOrgProfile, setRetryingOrgProfile] = useState(false);

	function loadOrgProfile() {
		if (!opportunity?.organizationId) return Promise.resolve();
		setOrgProfileError(null);
		return api
			.getPublicOrganizationProfile(opportunity.organizationId)
			.then(setOrgProfile)
			.catch((err) => {
				setOrgProfileError(
					getApiErrorMessage(
						err,
						t("opportunities.aboutOrganizationLoadError"),
					),
				);
			});
	}

	useEffect(() => {
		loadOrgProfile();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [opportunity?.organizationId]);

	function retryLoadOrgProfile() {
		setRetryingOrgProfile(true);
		loadOrgProfile().finally(() => setRetryingOrgProfile(false));
	}

	const latestRequestRef = useRef(0);

	useEffect(() => {
		if (!opportunityId) return;

		setOrgProfile(null);
		setOrgProfileError(null);
		load();

		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [opportunityId, isAuthenticated]);

	function load() {
		if (!opportunityId) return;
		const requestId = ++latestRequestRef.current;
		setLoading(true);
		setError(null);
		setErrorIsNetworkFailure(false);
		api
			.getVolunteerOpportunityDetails(opportunityId)
			.then((details) => {
				if (requestId !== latestRequestRef.current) return;
				setOpportunity(details);
			})
			.catch((err) => {
				if (requestId !== latestRequestRef.current) return;
				setError(getApiErrorMessage(err, t("error.serverError")));
				setErrorIsNetworkFailure(isNetworkError(err));
			})
			.finally(() => {
				if (requestId !== latestRequestRef.current) return;
				setLoading(false);
			});
	}

	async function handleReportSubmit(reason: ReportReason, details: string) {
		if (!opportunity) return;
		await api.reportVolunteerOpportunity(opportunity.id, {
			reason,
			details: details || undefined,
		});
		dispatchToast("success", t("report.submitSuccess"));
	}

	async function handlePublish() {
		if (!opportunity) return;
		setPublishing(true);
		try {
			await api.publishVolunteerOpportunity(opportunity.id);
			dispatchToast("success", t("opportunities.publishSuccess"));
			load();
		} catch (err) {
			dispatchToast("error", getApiErrorMessage(err, t("error.serverError")));
		} finally {
			setPublishing(false);
		}
	}

	async function handleWithdrawConfirm() {
		if (!opportunity?.currentUserEngagement) return;
		setWithdrawing(true);
		setWithdrawError(null);
		try {
			await api.withdrawEngagement(opportunity.currentUserEngagement.id);
			dispatchToast("success", t("myEngagements.withdrawSuccess"));
			setShowWithdrawConfirm(false);
			load();
		} catch (err) {
			setWithdrawError(
				getApiErrorMessage(err, t("myEngagements.withdrawError")),
			);
		} finally {
			setWithdrawing(false);
		}
	}

	if (loading)
		return (
			<div className="mx-auto max-w-4xl" role="status">
				<span className="sr-only">{t("opportunities.loading")}</span>
				<Skeleton className="mb-6 h-56 w-full sm:h-72" />

				<div className="max-w-2xl">
					<div className="mb-3 flex items-center justify-between gap-3">
						<Skeleton className="h-6 w-32 rounded-full" />
						<Skeleton className="h-8 w-20 rounded-lg" />
					</div>
					<Skeleton className="mb-3 h-8 w-3/4" />
					<Skeleton className="mb-2 h-4 w-full" />
					<Skeleton className="mb-6 h-4 w-2/3" />
					<Skeleton className="h-32 w-full" />
				</div>
			</div>
		);
	if (error)
		return errorIsOffline ? (
			<RouteState
				variant="offline"
				title={t("routeState.offline.title")}
				message={t("opportunities.offlineDetail")}
				onRetry={load}
			/>
		) : (
			<LoadMoreError
				message={t("opportunities.error", { message: error })}

				retrying={false}
				onRetry={load}
			/>
		);
	if (!opportunity)
		return <p className="text-gray-500">{t("opportunities.notFound")}</p>;

	const isOwner =
		isOrganisator && userOrgIds.includes(opportunity.organizationId);
	const isDraft = opportunity.status === "Draft";

	const hasActionRow = isDraft || !isOwner;

	const cue = opportunity.currentUserEngagement;

	const registeredTimeSlot = cue
		? opportunity.timeSlots.find((ts) => ts.id === cue.timeSlotId)
		: undefined;

	const capacity = getCapacityFromTimeSlots(
		opportunity.timeSlots,
		opportunity.currentParticipantCount,
		opportunity.participationType,
	);
	const isFull = capacity.kind === "capped" && capacity.isFull;
	const {
		label: capacityLabel,
		tone: capacityTone,
		secondaryLabel: capacitySecondaryLabel,
	} = describeCapacity(capacity, t);

	const address = opportunity.isRemote
		? ""
		: `${opportunity.street} ${opportunity.houseNumber}, ${opportunity.zipCode} ${opportunity.city}`;

	const directionsUrl =
		opportunity.latitude != null && opportunity.longitude != null
			? `https://www.google.com/maps/dir/?api=1&destination=${opportunity.latitude},${opportunity.longitude}`
			: `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(address)}`;

	const postedOnRelative = formatPostedAgo(
		opportunity.createdOn as unknown as string,
		t,
	);
	const postedOnAbsolute = formatDateTime(
		opportunity.createdOn as unknown as string,
		i18n.language,
	);

	const otherOrgOpportunities =
		orgProfile?.openOpportunities
			.filter((opp) => opp.id !== opportunity.id)
			.slice(0, 3) ?? [];

	const showDeadlineCard =
		opportunity.participationType === "IndividualContact" &&
		!!opportunity.validUntil &&
		!(isAuthenticated && !isOwner && !cue && !isDraft);
	const showApplicationStatus =
		isAuthenticated && !isOwner && !!cue && !isDraft;
	const showSignUpCta = isAuthenticated && !isOwner && !cue && !isDraft;
	const showLoginPrompt = !isAuthenticated && !isDraft;

	const showOwnerNotice = isOwner && !isDraft;
	const hasActionRail =
		showDeadlineCard ||
		showApplicationStatus ||
		showSignUpCta ||
		showLoginPrompt ||
		showOwnerNotice;

	function renderActionRail(
		testIdSuffix: string,
		opp: VolunteerOpportunityDetails,
	) {
		return (
			<>
				{showOwnerNotice && (
					<RouteState
						inline
						variant="forbidden"
						title={t("opportunities.ownOpportunityNoticeTitle")}
						message={t("opportunities.ownOpportunityNoticeMessage")}
						action={{
							label: t("engagementManagement.title"),
							to: `/app/${opp.organizationId}/dashboard/opportunities/${opp.id}/engagements`,
						}}
						data-testid={`opportunity-owner-notice${testIdSuffix}`}
					/>
				)}

				{showDeadlineCard && (
					<div
						className={`flex items-center gap-1.5 text-sm font-medium text-gray-700 ${cardClass}`}
					>
						<span>
							{t("opportunities.applyBy", {
								date: formatDate(
									opp.validUntil as unknown as string,
									i18n.language,
								),
							})}
						</span>
					</div>
				)}

				{showApplicationStatus && cue && (
					<div
						data-testid={`application-status${testIdSuffix}`}
						className={`${cardClass} sm:p-5`}
					>
						<div className="flex items-center justify-between gap-4">
							<div>
								<p className="mb-1 text-xs text-gray-500">
									{t("opportunities.yourApplication")}
								</p>
								<Chip
									tone={cue.status === "Confirmed" ? "success" : "warning"}
									size="sm"
								>
									{t(`myEngagements.status.${cue.status}`)}
								</Chip>

								{cue.status === "Pending" && (
									<p className="mt-1.5 text-xs text-gray-600">
										{t("myEngagements.pendingExplanation")}
									</p>
								)}
								{registeredTimeSlot && (
									<p className="mt-1.5 flex items-center gap-1.5 text-xs font-medium text-gray-700">
										<CalendarIcon className="h-3.5 w-3.5 shrink-0" />
										<span>
											{t("myEngagements.scheduledFor", {
												range: formatDateTimeRange(
													registeredTimeSlot.startDateTime as unknown as string,
													registeredTimeSlot.endDateTime as unknown as string,
													i18n.language,
												),
											})}
										</span>
									</p>
								)}
								{cue.isCheckedIn && (
									<Chip tone="success" size="sm" className="mt-2">
										<CheckIconSolid className="h-3 w-3" />
										{t("checkIn.checkedInLabel")}
									</Chip>
								)}
							</div>

							{!cue.isCheckedIn && (
								<Button
									type="button"
									variant="dangerOutline"
									size="sm"
									className="shrink-0"
									onClick={() => setShowWithdrawConfirm(true)}
									disabled={withdrawing}
								>
									{t("myEngagements.withdraw")}
								</Button>
							)}
						</div>
					</div>
				)}

				{showSignUpCta && (
					<div
						data-testid={`signup-cta${testIdSuffix}`}
						className={`space-y-3 ${cardClass} sm:p-5`}
					>
						{isFull && (
							<p className="text-sm font-medium text-red-600">
								{t("opportunities.noSpotsLeft")}
							</p>
						)}
						<Button
							onClick={() => {
								setPreselectedSlotId(undefined);
								setShowSignUp(true);
							}}
							disabled={isFull}
							fullWidth
							size="lg"
						>
							{opp.participationType === "ScheduledSlots"
								? t("opportunities.joinWaitlist")
								: t("opportunities.expressInterest")}
						</Button>
						{opp.participationType === "IndividualContact" &&
							opp.validUntil && (
								<p className="text-sm text-gray-600">
									{t("opportunities.applyBy", {
										date: formatDate(
											opp.validUntil as unknown as string,
											i18n.language,
										),
									})}
								</p>
							)}
					</div>
				)}

				{showLoginPrompt && (
					<div
						data-testid={`login-prompt${testIdSuffix}`}
						className={`space-y-3 ${cardClass} sm:p-5`}
					>
						<p className="text-sm text-gray-600">
							{t("opportunities.loginPrompt")}
						</p>
						<Button
							onClick={() => auth.signinRedirect(signinLocaleArgs())}
							data-testid={`opportunity-signin${testIdSuffix}`}
							fullWidth
							size="lg"
						>
							{t("nav.signIn")}
						</Button>
					</div>
				)}
			</>
		);
	}

	const headerTitle = pickLocalizedText(
		opportunity.titleDe,
		opportunity.titleEn,
		i18n.language,
	);
	const headerLead = pickLocalizedText(
		opportunity.descriptionDe,
		opportunity.descriptionEn,
		i18n.language,
	);

	const isGermanFallback =
		headerTitle.lang !== i18n.language ||
		(headerLead !== undefined && headerLead.lang !== i18n.language);

	return (
		<>
			<PageHeaderBand
				eyebrow={
					<Link
						to={`/organizations/${opportunity.organizationId}`}
						className="text-brand-200 underline-offset-2 transition-colors hover:text-white hover:underline"
					>
						{opportunity.organizationName}
					</Link>
				}
				title={headerTitle.text}
				titleLang={headerTitle.lang}
				lead={headerLead?.text}
				leadLang={headerLead?.lang}
			>
				{isGermanFallback && (
					<p className="text-sm text-brand-200">
						{t("opportunities.germanOnlyNotice")}
					</p>
				)}
			</PageHeaderBand>

			<div data-content-wrapper className="mx-auto max-w-6xl">
				{opportunity.bannerImageUrl && (
					<img
						src={opportunity.bannerImageUrl}
						alt=""
						width={1200}
						height={480}
						className="mb-6 h-56 w-full rounded-card object-cover shadow-resting sm:h-72"
					/>
				)}

				<div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start lg:gap-10">
					<aside className="hidden lg:sticky lg:top-24 lg:col-start-2 lg:row-start-1 lg:block">
						<div className="space-y-6">{renderActionRail("", opportunity)}</div>
					</aside>

					<div className="min-w-0 lg:col-start-1 lg:row-start-1">
						<div className="max-w-2xl">
							{hasActionRow && (
								<div
									className="mb-4 flex items-center gap-3"
									data-testid="opportunity-detail-actions"
								>
									{isDraft && isOwner && (
										<div className="flex min-w-0 items-center gap-2">
											<Chip
												tone="warning"
												size="sm"
												data-testid="opportunity-detail-draft-badge"
											>
												{t("opportunities.draftBadge")}
											</Chip>
										</div>
									)}

									<div className="ml-auto flex shrink-0 gap-2">
										{!isOwner && (
											<Button
												variant="outline"
												size="sm"
												onClick={() =>
													isAuthenticated
														? setShowReport(true)
														: auth.signinRedirect(signinLocaleArgs())
												}
												data-testid="report-opportunity"
												aria-label={t("opportunities.reportOpportunity")}
											>
												<FlagIcon className="h-4 w-4" />
												<span className="hidden sm:inline">
													{t("opportunities.report")}
												</span>
											</Button>
										)}
										{isDraft && isOwner && (
											<>
												<Button
													variant="outline"
													size="sm"
													onClick={() => setShowEditModal(true)}
													data-testid="opportunity-detail-edit"
												>
													{t("opportunities.edit")}
												</Button>
												<Button
													type="button"
													size="sm"
													onClick={() => void handlePublish()}
													disabled={publishing}
													data-testid="opportunity-detail-publish"
												>
													{publishing
														? t("opportunities.publishing")
														: t("opportunities.publish")}
												</Button>
											</>
										)}
									</div>
								</div>
							)}

							<dl
								className="mb-5 grid gap-5 rounded-card bg-brand-50 p-5 sm:grid-cols-3 sm:p-6"
								data-testid="opportunity-at-a-glance"
							>
								<div>
									<dt className="flex items-center gap-2 text-xs font-semibold tracking-widest text-brand-700 uppercase">
										<CalendarIcon className="h-4 w-4 shrink-0" />
										{t("opportunities.factWhen")}
									</dt>
									<dd
										className="mt-2 text-sm font-medium text-gray-900"
										data-testid="opportunity-detail-when"
									>
										{describeWhenFact(opportunity, t, i18n.language)}
									</dd>
								</div>

								<div>
									<dt className="flex items-center gap-2 text-xs font-semibold tracking-widest text-brand-700 uppercase">
										<UserGroupIcon className="h-4 w-4 shrink-0" />
										{t("opportunities.factFormat")}
									</dt>
									<dd
										className="mt-2 text-sm font-medium text-gray-900"
										data-testid="opportunity-detail-how"
									>
										{describeHowFact(opportunity, t)}
									</dd>
								</div>

								<div>
									<dt className="flex items-center gap-2 text-xs font-semibold tracking-widest text-brand-700 uppercase">
										{opportunity.isRemote ? (
											<GlobeIcon className="h-4 w-4 shrink-0" />
										) : (
											<MapPinIcon className="h-4 w-4 shrink-0" />
										)}
										{t("opportunities.factWhere")}
									</dt>
									<dd className="mt-2 text-sm font-medium text-gray-900">
										{opportunity.isRemote ? t("opportunities.remote") : address}
									</dd>
								</div>
							</dl>

							<div className="mb-6 flex flex-wrap items-center gap-2">
								{opportunity.category && (
									<Chip tone="brand">
										{t(`opportunities.category.${opportunity.category}`)}
									</Chip>
								)}

								<Chip
									tone="neutral"
									size="sm"
									data-testid="opportunity-occurrence"
								>
									{formatOccurrence(opportunity.occurrence, t)}
								</Chip>
								{opportunity.tags?.map((tag) => (
									<Chip
										key={tag}
										tone="neutral"
										to={`/opportunities?tag=${encodeURIComponent(tag)}`}
										aria-label={t("opportunities.filterByTag", { tag })}
									>
										{tag}
									</Chip>
								))}

								<span
									data-testid="opportunity-capacity"
									className={`text-sm font-medium ${capacityTone}`}
								>
									{capacityLabel}
								</span>

								{capacitySecondaryLabel && (
									<span
										data-testid="opportunity-capacity-secondary"
										className="text-sm text-gray-600"
									>
										{capacitySecondaryLabel}
									</span>
								)}
								<span
									className="text-xs text-gray-500"
									title={postedOnAbsolute}
									aria-label={`${postedOnRelative} (${postedOnAbsolute})`}
								>
									{postedOnRelative}
								</span>
							</div>

							{hasActionRail && (
								<div
									className="mb-6 space-y-6 lg:hidden"
									data-testid="opportunity-action-rail-mobile"
								>
									{renderActionRail("-mobile", opportunity)}
								</div>
							)}

							{!opportunity.isRemote && (
								<div className="mb-6">
									{opportunity.latitude != null &&
										opportunity.longitude != null && (
											<div className="overflow-hidden rounded-card border border-gray-100 shadow-resting">
												<Suspense
													fallback={<Skeleton className="h-64 w-full" />}
												>
													<SingleMarkerMap
														latitude={opportunity.latitude}
														longitude={opportunity.longitude}
														label={address}
													/>
												</Suspense>
											</div>
										)}

									<a
										href={directionsUrl}
										target="_blank"
										rel="noopener noreferrer"
										data-testid="opportunity-directions-link"
										className="mt-3 inline-flex items-center gap-1.5 text-sm font-medium text-brand-700 transition-colors hover:text-brand-800 hover:underline"
									>
										<ArrowTopRightOnSquareIcon className="h-3.5 w-3.5" />
										{t("opportunities.getDirections")}
									</a>
								</div>
							)}
						</div>

						{opportunity.participationType === "ScheduledSlots" &&
							opportunity.timeSlots.length > 0 && (
								<div
									className="mb-6 max-w-2xl"
									data-testid="opportunity-time-slots"
								>
									<SectionHeading>
										{t("opportunities.availableTimeSlots")}
									</SectionHeading>
									<ul className="space-y-2">
										{opportunity.timeSlots.map((ts) => {
											const clickable =
												showSignUpCta &&
												!isSlotFull(ts.maxParticipants, ts.bookedCount);
											const rowContent = (
												<>
													<span>
														{formatDateTimeRange(
															ts.startDateTime as unknown as string,
															ts.endDateTime as unknown as string,
															i18n.language,
														)}
													</span>

													<span className="ml-3 flex shrink-0 items-center gap-1.5 text-xs text-gray-600">
														{slotCapacityLabel(ts, t)}
														{clickable && (
															<ChevronRightIcon className="h-3.5 w-3.5 text-gray-400" />
														)}
													</span>
												</>
											);
											return (
												<li key={ts.id}>
													{clickable ? (
														<button
															type="button"
															onClick={() => {
																setPreselectedSlotId(ts.id);
																setShowSignUp(true);
															}}
															data-testid="opportunity-time-slot-row"
															className={`flex w-full items-center justify-between ${cardClass} text-left text-sm text-gray-700 transition-shadow hover:shadow-raised`}
														>
															{rowContent}
														</button>
													) : (
														<div
															className={`flex items-center justify-between ${cardClass} text-sm text-gray-700`}
														>
															{rowContent}
														</div>
													)}
												</li>
											);
										})}
									</ul>
								</div>
							)}
						<div className="max-w-2xl">
							{orgProfileError && !orgProfile && (
								<div className="mb-6" data-testid="about-organization">
									<SectionHeading>
										{t("opportunities.aboutOrganization")}
									</SectionHeading>
									<LoadMoreError
										message={orgProfileError}
										retrying={retryingOrgProfile}
										onRetry={retryLoadOrgProfile}
									/>
								</div>
							)}
							{orgProfile &&
								(orgProfile.description ||
									orgProfile.contactEmail ||
									orgProfile.contactPhone ||
									orgProfile.website ||
									orgProfile.address) && (
									<div className="mb-6" data-testid="about-organization">
										<SectionHeading>
											{t("opportunities.aboutOrganization")}
										</SectionHeading>
										{orgProfile.description && (
											<p
												lang="de"
												className="mb-3 leading-relaxed text-gray-600"
											>
												{orgProfile.description}
											</p>
										)}
										{(orgProfile.contactEmail ||
											orgProfile.contactPhone ||
											orgProfile.website ||
											orgProfile.address) && (
											<div
												className={`space-y-2.5 ${cardClass} text-sm text-gray-700`}
											>
												{orgProfile.contactEmail && (
													<div className="flex items-center gap-3">
														<EnvelopeIcon className="h-4 w-4 shrink-0 text-brand-700" />
														<a
															href={`mailto:${orgProfile.contactEmail}`}
															className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
														>
															{orgProfile.contactEmail}
														</a>
													</div>
												)}
												{orgProfile.contactPhone && (
													<div className="flex items-center gap-3">
														<PhoneIcon className="h-4 w-4 shrink-0 text-brand-700" />
														<a
															href={`tel:${orgProfile.contactPhone}`}
															className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
														>
															{orgProfile.contactPhone}
														</a>
													</div>
												)}
												{orgProfile.website && (
													<div className="flex items-center gap-3">
														<GlobeIcon className="h-4 w-4 shrink-0 text-brand-700" />
														<a
															href={orgProfile.website}
															target="_blank"
															rel="noopener noreferrer"
															className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
														>
															{orgProfile.website}
														</a>
													</div>
												)}
												{orgProfile.address && (
													<div className="flex items-center gap-3">
														<MapPinIcon className="h-4 w-4 shrink-0 text-brand-700" />
														<span>
															{orgProfile.address.street}{" "}
															{orgProfile.address.houseNumber},{" "}
															{orgProfile.address.zipCode}{" "}
															{orgProfile.address.city}
														</span>
													</div>
												)}
											</div>
										)}
									</div>
								)}
						</div>
					</div>
				</div>

				{otherOrgOpportunities.length > 0 && (
					<div className="mb-6 max-w-2xl" data-testid="more-from-organization">
						<SectionHeading>
							{t("opportunities.moreFromOrganization")}
						</SectionHeading>
						<ul className="grid grid-cols-1 gap-4 sm:grid-cols-2">
							{otherOrgOpportunities.map((opp) => (
								<OpportunityCard key={opp.id} item={opp} headingLevel={3} />
							))}
						</ul>
					</div>
				)}
				{showSignUp && (
					<SignUpModal
						opportunityId={opportunity.id}
						organizationId={opportunity.organizationId}
						participationType={opportunity.participationType}
						timeSlots={opportunity.timeSlots}
						preselectedTimeSlotId={preselectedSlotId}
						onClose={() => {
							setShowSignUp(false);
							setPreselectedSlotId(undefined);
						}}
						onSuccess={() => {
							setShowSignUp(false);
							setPreselectedSlotId(undefined);
							dispatchToast("success", t("signUp.success"));
							load();
						}}
					/>
				)}

				{showWithdrawConfirm && cue && (
					<ConfirmDialog
						title={t("confirmDialog.withdraw.title")}
						message={t(
							cue.remainingReactivations === 0
								? "confirmDialog.withdraw.messageLimitReached"
								: "confirmDialog.withdraw.message",
							{ title: headerTitle.text },
						)}
						confirmLabel={t("confirmDialog.withdraw.confirm")}
						onConfirm={handleWithdrawConfirm}
						onClose={() => {
							setShowWithdrawConfirm(false);
							setWithdrawError(null);
						}}
						loading={withdrawing}
						error={withdrawError}
					>
						{cue.remainingReactivations === 0 && (
							<Link
								to={`/organizations/${opportunity.organizationId}`}
								className="mt-1 inline-block text-sm text-brand-700 underline-offset-2 hover:text-brand-800 hover:underline"
							>
								{t("common.contactOrganization")}
							</Link>
						)}
						{withdrawLimitWarningActive && (
							<WarningBanner
								ref={withdrawLimitWarningRef}
								tabIndex={-1}
								className="focus:outline-none"
								message={t("confirmDialog.withdraw.limitWarning")}
							/>
						)}
					</ConfirmDialog>
				)}

				{showReport && (
					<ReportContentModal
						targetLabel={headerTitle.text}
						targetLabelLang={headerTitle.lang}
						onSubmit={handleReportSubmit}
						onClose={() => setShowReport(false)}
					/>
				)}

				{showEditModal && (
					<Suspense
						fallback={
							<ModalLoadingFallback onClose={() => setShowEditModal(false)} />
						}
					>
						<CreateVolunteerOpportunityModal
							organizationId={opportunity.organizationId}
							initialOpportunity={opportunity}
							onClose={() => setShowEditModal(false)}
							onSuccess={() => {
								setShowEditModal(false);
								load();
							}}
						/>
					</Suspense>
				)}
			</div>
		</>
	);
}
