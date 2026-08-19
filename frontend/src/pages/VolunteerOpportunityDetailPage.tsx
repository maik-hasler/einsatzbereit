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
import PublicOpportunityCard from "../components/PublicOpportunityCard";
import RouteState from "../components/RouteState";
import { usePageTitle } from "../hooks/usePageTitle";
import { dispatchToast } from "../lib/toastBus";
import { getApiErrorMessage, isNetworkError } from "../lib/apiError";
import { signinLocaleArgs } from "../lib/authLocale";
import { cardClass } from "../lib/surfaceClasses";
import {
	CalendarIcon,
	CheckIconSolid,
	EnvelopeIcon,
	FlagIcon,
	GlobeIcon,
	MapPinIcon,
	PhoneIcon,
	UserGroupIcon,
} from "../components/icons";

// Lazy-loaded: Leaflet only renders once an opportunity actually has
// coordinates, so keep it out of this page's (and thus the shared home page
// bundle's) initial chunk - #971.
const SingleMarkerMap = lazy(() => import("../components/SingleMarkerMap"));

// Lazy-loaded: the multi-step create/edit form is only ever needed by the
// owning organizer editing their own draft, never by the public visitors who
// make up the vast majority of this page's traffic.
const CreateVolunteerOpportunityModal = lazy(
	() => import("../components/CreateVolunteerOpportunityModal"),
);

/**
 * The page's one capacity sentence, in the same "free places" framing the
 * cards use - never the per-slot maximum, which is what let the list and this
 * page describe the same opportunity two different ways (#1777).
 */
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
			// The type badge stays put regardless of the current viewer's own
			// application status; the joined count - once there is one - is an
			// addition next to it, not a replacement, so which piece of
			// information appears here doesn't depend on whether this
			// particular viewer has already applied (#1941).
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

/** A single slot's remaining places, sharing the sign-up modal's helpers. */
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

export default function VolunteerOpportunityDetailPage() {
	const { opportunityId } = useParams<{ opportunityId: string }>();
	const auth = useAuth();
	const api = useApiClient();
	const { t, i18n } = useTranslation();

	const [opportunity, setOpportunity] =
		useState<VolunteerOpportunityDetails | null>(null);
	usePageTitle(
		opportunity &&
			pickLocalizedText(
				opportunity.titleDe,
				opportunity.titleEn,
				i18n.language,
			),
	);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	// #2065: whether `error` came from a request that never got an HTTP
	// response at all - the same offline signal useLoadMore/OrgAppLayout use,
	// since `navigator.onLine` alone can misreport `true` right after a hard
	// reload while genuinely offline (#1901).
	const [errorIsNetworkFailure, setErrorIsNetworkFailure] = useState(false);
	const online = useOnlineStatus();
	const errorIsOffline = error !== null && (!online || errorIsNetworkFailure);
	const [showSignUp, setShowSignUp] = useState(false);
	const [showReport, setShowReport] = useState(false);
	const [showWithdrawConfirm, setShowWithdrawConfirm] = useState(false);
	const [withdrawing, setWithdrawing] = useState(false);
	const [withdrawError, setWithdrawError] = useState<string | null>(null);
	const [showEditModal, setShowEditModal] = useState(false);
	const [publishing, setPublishing] = useState(false);

	const isAuthenticated = auth.isAuthenticated;
	const roles = (
		Array.isArray(auth.user?.profile?.roles) ? auth.user?.profile?.roles : []
	) as string[];
	// A stale Keycloak session left in sessionStorage from an earlier login still
	// populates auth.user.profile after the token has expired, so this must
	// gate on isAuthenticated too - otherwise an anonymous visitor with old
	// organisator claims fires an authenticated getOrganizations() call below,
	// 401s, and gets force-redirected to sign-in on a page that's meant to
	// work without being logged in.
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
		// Avoids briefly showing the previous opportunity's organization card
		// (email/phone/address) while the new one loads, since this state isn't
		// otherwise tied to opportunityId and would only refresh once the
		// organizationId effect below notices it changed.
		setOrgProfile(null);
		setOrgProfileError(null);
		load();
		// `auth.isAuthenticated`, not `api` itself (#1237): useApiClient() memoizes
		// on user.access_token, which automaticSilentRenew replaces every ~4
		// minutes even though the user's actual auth status hasn't changed -
		// depending on `api` directly reran this effect (and its setLoading(true)
		// skeleton swap) on every one of those renewals. Unlike ProfileOverviewPage
		// (behind ProtectedRoute, so always already-authenticated on mount), this
		// page is public and can mount before auth has finished resolving - the
		// anonymous-vs-authenticated fetch genuinely differs (draft visibility,
		// currentUserEngagement), so a real `isAuthenticated` flip (once auth
		// settles, or on sign-in/out) still needs to trigger a refetch.
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
				{/* Same flush-left reading column as the loaded page below, so the
				skeleton doesn't sit at a different x than the content replacing it. */}
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
			// #2065: this page had no offline handling at all - a dropped
			// connection fell into the generic error branch below, with a retry
			// button that could not succeed while the connection was down. Not
			// `inline`: unlike OpportunityResultsList's offline notice, this
			// replaces the whole route rather than one section of a page that
			// already owns an <h1>.
			<RouteState
				variant="offline"
				title={t("routeState.offline.title")}
				message={t("opportunities.offlineDetail")}
				onRetry={load}
			/>
		) : (
			<LoadMoreError
				message={t("opportunities.error", { message: error })}
				// load() unconditionally flips `loading` back to true, so the
				// `if (loading)` skeleton above always pre-empts this branch the
				// instant a retry starts - there's no in-between state where this
				// button would be visible mid-request, so it can never actually be
				// clicked twice.
				retrying={false}
				onRetry={load}
			/>
		);
	if (!opportunity)
		return <p className="text-gray-500">{t("opportunities.notFound")}</p>;

	const isOwner =
		isOrganisator && userOrgIds.includes(opportunity.organizationId);
	const isDraft = opportunity.status === "Draft";

	// Everything in the action row above the at-a-glance panel is conditional,
	// so the row itself has to be too - otherwise an anonymous visitor (the
	// bulk of this page's traffic) gets an empty flex row and its mb-4 as a
	// dead gap between the band and the panel.
	const hasActionRow = (isDraft && isOwner) || (isAuthenticated && !isOwner);

	const cue = opportunity.currentUserEngagement;

	// The slot the signed-in volunteer registered for, so the status card
	// below can show its date/time next to the status Chip - matching what
	// /my-signups already shows for the same engagement (#1938).
	const registeredTimeSlot = cue
		? opportunity.timeSlots.find((ts) => ts.id === cue.timeSlotId)
		: undefined;

	// Folded down by the shared contract, with the same rule the list
	// projection uses - so this page can no longer state a different capacity
	// than the card the reader clicked to get here (#1777).
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

	// formatPostedAgo's relative text ("Vor 5 Tagen veroeffentlicht") is the
	// one documented exception to the site's numeric date convention - it
	// still needs the absolute date available to screen readers (`aria-label`,
	// not just `title`, which browse-mode AT users and touch/mobile never see
	// - #2047) and to sighted mouse users (`title`).
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

	// The four mutually-exclusive-ish blocks the sticky rail can show. Named
	// here (rather than inlined twice) because #1965 renders the same rail
	// content a second time on narrow viewports - see renderActionRail below.
	const showDeadlineCard =
		opportunity.participationType === "IndividualContact" &&
		!!opportunity.validUntil &&
		!(isAuthenticated && !isOwner && !cue && !isDraft);
	const showApplicationStatus =
		isAuthenticated && !isOwner && !!cue && !isDraft;
	const showSignUpCta = isAuthenticated && !isOwner && !cue && !isDraft;
	const showLoginPrompt = !isAuthenticated && !isDraft;
	// An owner viewing their own published opportunity gets none of the three
	// blocks above (each requires !isOwner) - without this, the rail is just
	// silently empty, indistinguishable from a rendering failure, and offers
	// no way back to the management view (#2081). Excluded for isDraft: the
	// action row already shows the draft badge plus Edit/Publish there, and an
	// unpublished draft has no engagements yet for the linked management page
	// to show.
	const showOwnerNotice = isOwner && !isDraft;
	const hasActionRail =
		showDeadlineCard ||
		showApplicationStatus ||
		showSignUpCta ||
		showLoginPrompt ||
		showOwnerNotice;

	// The sticky rail's content, rendered once for the lg+ sidebar and once
	// more (testIdSuffix "-mobile") right above the map for narrow viewports -
	// the aside is invisible below lg (`hidden` until `lg:block`) since sticky
	// positioning and the grid column it's pinned to (#2050) only exist at
	// that breakpoint, so without this second copy a mobile visitor would
	// have no way to reach the CTA at all (#1965). Only one instance is ever
	// visible at a given viewport (the aside hides below lg, this copy hides
	// at lg+), so duplicated ids don't collide and duplicated buttons are
	// never both reachable at once.
	// Takes the already-null-checked opportunity as a parameter rather than
	// closing over the outer `opportunity` state directly - TS control-flow
	// narrowing doesn't carry a `const`'s narrowed type into a nested function
	// declared after the null check, so referencing `opportunity` in here
	// would still type as possibly-null.
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
							{/* Withdrawing after check-in is rejected server-side (Engagement.Withdraw's
							IsCheckedIn guard, #673) - hide the action rather than let a volunteer hit
							that 409, matching the same isCheckedIn gate /my-signups already applies
							(#1893). */}
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

				{/* Sign-up CTA */}
				{showSignUpCta && (
					<div
						data-testid={`signup-cta${testIdSuffix}`}
						className={`space-y-3 ${cardClass} sm:p-5`}
					>
						{/* Only the full state speaks here now: it explains why the
						button below is disabled. The remaining places themselves are
						stated in the meta row above, where every visitor sees them
						rather than only signed-in non-owners (#1777). */}
						{isFull && (
							<p className="text-sm font-medium text-red-600">
								{t("opportunities.noSpotsLeft")}
							</p>
						)}
						<Button
							onClick={() => setShowSignUp(true)}
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
				title={pickLocalizedText(
					opportunity.titleDe,
					opportunity.titleEn,
					i18n.language,
				)}
				lead={pickLocalizedText(
					opportunity.descriptionDe,
					opportunity.descriptionEn,
					i18n.language,
				)}
			/>

			<div data-content-wrapper className="mx-auto max-w-6xl">
				{/* Banner image - spans the full width of this wider wrapper; a
			reading column is right for prose below, but there's no reason to
			confine a banner to it too (#1727). */}
				{opportunity.bannerImageUrl && (
					<img
						src={opportunity.bannerImageUrl}
						alt=""
						width={1200}
						height={480}
						className="mb-6 h-56 w-full rounded-card object-cover shadow-resting sm:h-72"
					/>
				)}

				{/* Two columns from lg up (#1755). What a visitor reads to decide -
			what it is, when, where, who runs it - stays in the reading column;
			what they act on (deadline, their own status, the sign-up button) moves
			into a sticky rail beside it. The CTA used to sit inline after the
			time-slot list, which on a long opportunity put the page's only
			conversion point below the fold while ~500px of page sat empty next to
			it. One column below lg, where a 20rem rail would just be a narrow box
			and the CTA is better off in reading order anyway. The rail is the
			first child below (not the reading column) with explicit grid
			placement pinning each side back to its visual column/row (#2050) -
			DOM order used to match visual order by accident, which put the rail
			last in both and made the CTA the 8th focusable element on the page,
			after the report button, the map and the organization's contact
			links. */}
				<div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start lg:gap-10">
					{/* Hidden below lg: the mobile-only copy right above the map (#1965)
				carries the same content there instead. sticky needs a scroll
				container that is not the grid item itself; lg:items-start on the
				grid keeps this from stretching to full row height, which would
				make top-24 have nothing left to stick against. */}
					<aside className="hidden lg:sticky lg:top-24 lg:col-start-2 lg:row-start-1 lg:block">
						<div className="space-y-6">{renderActionRail("", opportunity)}</div>
					</aside>

					<div className="min-w-0 lg:col-start-1 lg:row-start-1">
						{/* Flush left inside the outer wrapper, not centred within it.
			#1727 deliberately let the banner span the full wrapper while prose
			stayed at a reading measure - but centring the prose meant the page
			alternated between two column widths, reading as misaligned blocks
			rather than one document. Sharing a left edge keeps #1727's wider
			banner and a readable measure at the same time. */}
						<div className="max-w-2xl">
							{/* Report (and the owner's draft controls) sit on the same line
							as the at-a-glance panel's top edge rather than floating alone
							above an empty stretch of column - the org chip that used to
							anchor this row moved into the band's eyebrow. */}
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
									{/* ml-auto rather than justify-between on the row: the draft
									chip beside it is conditional, and justify-between would have
									needed an empty placeholder div to keep the actions right of
									the column for everyone who doesn't see the chip. */}
									<div className="ml-auto flex shrink-0 gap-2">
										{isAuthenticated && !isOwner && (
											<Button
												variant="outline"
												size="sm"
												onClick={() => setShowReport(true)}
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

							{/* At-a-glance panel (#1755). The three facts a volunteer decides
						on - when, how, where - were three identical grey icon rows in a
						flat white box, with the category chip, the participant count and
						the posted-on date orphaned as three more separate lines under
						it. One tinted panel with labelled columns gives them a hierarchy
						and puts the page's first real colour below the band; the loose
						lines collapse into a single meta row beneath it. */}
							<dl
								className="mb-5 grid gap-5 rounded-card bg-brand-50 p-5 sm:grid-cols-3 sm:p-6"
								data-testid="opportunity-at-a-glance"
							>
								<div>
									<dt className="flex items-center gap-2 text-xs font-semibold tracking-widest text-brand-700 uppercase">
										<CalendarIcon className="h-4 w-4 shrink-0" />
										{t("opportunities.factWhen")}
									</dt>
									<dd className="mt-2 text-sm font-medium text-gray-900">
										{formatOccurrence(opportunity.occurrence, t)}
									</dd>
								</div>

								<div>
									<dt className="flex items-center gap-2 text-xs font-semibold tracking-widest text-brand-700 uppercase">
										<UserGroupIcon className="h-4 w-4 shrink-0" />
										{t("opportunities.factFormat")}
									</dt>
									<dd className="mt-2 text-sm font-medium text-gray-900">
										{formatParticipationType(opportunity.participationType, t)}
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
										{opportunity.isRemote
											? t("opportunities.remote")
											: `${opportunity.street} ${opportunity.houseNumber}, ${opportunity.zipCode} ${opportunity.city}`}
									</dd>
								</div>
							</dl>

							{/* Meta row - category, tags, headcount and posted-on, previously
						three separate stranded lines. */}
							<div className="mb-6 flex flex-wrap items-center gap-2">
								{opportunity.category && (
									<Chip tone="brand">
										{t(`opportunities.category.${opportunity.category}`)}
									</Chip>
								)}
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
								{/* Capacity, stated once and to everyone. It used to live only
							inside the sign-up box, gated on `isAuthenticated && !isOwner &&
							!cue && !isDraft`, so an anonymous visitor - most of this page's
							traffic - never saw the remaining places at all and read the
							per-slot maximum instead. That is what made a card saying "19
							spots left" open a page saying "(max. 20 people)" (#1777). */}
								<span
									data-testid="opportunity-capacity"
									className={`text-sm font-medium ${capacityTone}`}
								>
									{capacityLabel}
								</span>
								{/* Addition, not a replacement, for the type badge above -
								an "Interessenbekundung" offer keeps stating its type once it
								has applicants, instead of the slot swapping to the applicant
								count only for the viewer who happens to have applied (#1941). */}
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

							{/* Mobile-only duplicate of the sticky rail (#1965) - see
						renderActionRail's comment above. lg:hidden because the aside
						below already carries the same content at lg+. */}
							{hasActionRail && (
								<div
									className="mb-6 space-y-6 lg:hidden"
									data-testid="opportunity-action-rail-mobile"
								>
									{renderActionRail("-mobile", opportunity)}
								</div>
							)}

							{!opportunity.isRemote &&
								(opportunity.latitude != null &&
								opportunity.longitude != null ? (
									<div className="mb-6 overflow-hidden rounded-card border border-gray-100 shadow-resting">
										<Suspense fallback={<Skeleton className="h-64 w-full" />}>
											<SingleMarkerMap
												latitude={opportunity.latitude}
												longitude={opportunity.longitude}
												label={`${opportunity.street} ${opportunity.houseNumber}, ${opportunity.zipCode} ${opportunity.city}`}
											/>
										</Suspense>
									</div>
								) : (
									// Coordinates can be missing (geocoding failure/pending
									// retry, see backend/AGENTS.md's "Domain events") - showing
									// this note instead of omitting the section keeps the layout
									// consistent with opportunities that do have a map (#1963).
									<div
										data-testid="map-unavailable"
										className="mb-6 flex h-64 flex-col items-center justify-center gap-2 rounded-card border border-gray-100 bg-gray-50 shadow-resting"
									>
										<MapPinIcon className="h-6 w-6 text-gray-400" />
										<p className="text-sm text-gray-500">
											{t("opportunities.mapUnavailable")}
										</p>
									</div>
								))}
						</div>

						{/* Time slots - held to the same max-w-2xl measure as the blocks
			above and below it (#1794). #1727 had let this list span the full grid
			column on the grounds that date/spot rows aren't prose, but at 1440px
			that gave the main column a second right edge 120px out from its
			neighbours', so the page read as three misaligned blocks rather than
			one. One measure for the whole column wins over the marginally roomier
			rows. */}
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
										{opportunity.timeSlots.map((ts) => (
											<li
												key={ts.id}
												className={`flex items-center justify-between ${cardClass} text-sm text-gray-700`}
											>
												<span>
													{formatDateTimeRange(
														ts.startDateTime as unknown as string,
														ts.endDateTime as unknown as string,
														i18n.language,
													)}
												</span>
												{/* Free places, the same framing the cards and the sign-up
												modal's slot picker use. This said "(max. N people)" while the
												card that linked here said "N spots left", so the two
												disagreed about the same opportunity (#1777). */}
												<span className="ml-3 shrink-0 text-xs text-gray-600">
													{slotCapacityLabel(ts, t)}
												</span>
											</li>
										))}
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
											<p className="mb-3 leading-relaxed text-gray-600">
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

				{/* More from this organization - held to the same max-w-2xl measure
			as the reading column above (#2044). #1727 had let this section span
			the full outer wrapper so a third card wouldn't orphan onto its own
			row, but that put a 1152px-wide block directly below a 672px-wide
			column on the same scroll, reading as two different pages stacked.
			One measure for the whole page wins over the marginally roomier grid -
			dropping the xl:grid-cols-3 step rather than keeping a column count
			this measure can no longer fit without squeezing each card. */}
				{otherOrgOpportunities.length > 0 && (
					<div className="mb-6 max-w-2xl" data-testid="more-from-organization">
						<SectionHeading>
							{t("opportunities.moreFromOrganization")}
						</SectionHeading>
						<ul className="grid grid-cols-1 gap-4 sm:grid-cols-2">
							{otherOrgOpportunities.map((opp) => (
								<PublicOpportunityCard key={opp.id} opportunity={opp} />
							))}
						</ul>
					</div>
				)}
				{showSignUp && (
					<SignUpModal
						opportunityId={opportunity.id}
						participationType={opportunity.participationType}
						timeSlots={opportunity.timeSlots}
						onClose={() => setShowSignUp(false)}
						onSuccess={() => {
							setShowSignUp(false);
							load();
						}}
					/>
				)}

				{showWithdrawConfirm && (
					<ConfirmDialog
						title={t("confirmDialog.withdraw.title")}
						message={t("confirmDialog.withdraw.message")}
						confirmLabel={t("confirmDialog.withdraw.confirm")}
						onConfirm={handleWithdrawConfirm}
						onClose={() => {
							setShowWithdrawConfirm(false);
							setWithdrawError(null);
						}}
						loading={withdrawing}
						error={withdrawError}
					/>
				)}

				{showReport && (
					<ReportContentModal
						targetLabel={pickLocalizedText(
							opportunity.titleDe,
							opportunity.titleEn,
							i18n.language,
						)}
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
