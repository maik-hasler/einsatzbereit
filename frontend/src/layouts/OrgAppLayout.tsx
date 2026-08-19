import { Suspense, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Outlet, useLocation, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrganizationDetailsResponse } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { useAchievementNotifier } from "../hooks/useAchievementNotifier";
import { useOnlineStatus } from "../hooks/useOnlineStatus";
import { setActiveOrgId } from "../lib/activeOrg";
import { ORG_TABS, orgTabPath } from "../lib/orgTabs";
import {
	getApiErrorMessage,
	getApiErrorStatus,
	isNetworkError,
} from "../lib/apiError";
import {
	OrgBreadcrumbProvider,
	useOrgBreadcrumbExtra,
	useOrgBreadcrumbExtraLang,
} from "../contexts/OrgBreadcrumbContext";
import { QuickActionsProvider } from "../contexts/QuickActionsContext";
import Header from "../components/Header/Header";
import OrgPageHeader from "../components/OrgPageHeader";
import Footer from "../components/Footer";
import Spinner from "../components/Spinner";
import SkipLink from "../components/SkipLink";
import Button from "../components/Button";
import ErrorBoundary from "../components/ErrorBoundary";
import RouteState from "../components/RouteState";

export interface OrgAppContext {
	org: OrganizationDetailsResponse;
	reloadOrg: () => void;
	// Whether the signed-in user is an Organizer of this specific organization
	// (as opposed to a plain Member) - gates every management action (invite,
	// promote/demote, remove, settings edit, opportunity/engagement management)
	// across every org-app page. A plain Member has read-only access.
	isOrganizer: boolean;
}

// Renders the org app shell body inside OrgBreadcrumbProvider so it can read
// the nested-page title extra (see useSetOrgBreadcrumbExtra). Normally the
// page header shows the active tab's own name; a nested page (e.g. engagement
// management) overrides it with its own, which demotes the tab name to the
// header's back link.
function OrgAppShell({
	organizationId,
	org,
	activeTabKey,
	activeTabLabel,
	load,
}: {
	organizationId: string | undefined;
	org: OrganizationDetailsResponse;
	activeTabKey: string;
	activeTabLabel: string;
	load: () => void;
}) {
	// #1034: this hook was previously only mounted in AppLayout (the public
	// site layout), so users working inside the org app shell never polled
	// for newly-unlocked badges and never saw the unlock toast.
	useAchievementNotifier();
	const { t } = useTranslation();
	const location = useLocation();
	const extra = useOrgBreadcrumbExtra();
	const extraLang = useOrgBreadcrumbExtraLang();
	// Answered from organization_membership (see OrganizationDetailsResponse's
	// requestingUserRole), not by scanning the Keycloak-sourced org.members roster
	// below - so the shell's own nav/actions still know the caller's role even
	// when that roster couldn't be loaded (#1709).
	const isOrganizer = org.requestingUserRole === "Organizer";

	// The way back, one level up - only from a nested page (e.g. an
	// opportunity's sign-ups) to the tab that owns it. A tab page itself needs
	// no back link any more: OrgPageHeader's section rail lists every tab with
	// the current one marked, which says where you are and gets you anywhere
	// else in one click rather than one click per level.
	const back =
		extra && organizationId
			? {
					href: orgTabPath(organizationId, activeTabKey),
					label: activeTabLabel,
				}
			: null;

	// #973: OrgAppShell is the only place that knows the current tab/nested-page
	// title (activeTabLabel/extra, same source as the breadcrumb's last item) -
	// render it as a real h1 here rather than in each of the four tab pages, so
	// axe's page-has-heading-one/heading-order rules pass on every org app page.
	const pageTitle = extra ?? activeTabLabel;
	// extraLang only applies while `extra` (not the tab's own, always-UI-language
	// label) is actually what's shown.
	const pageTitleLang = extra ? (extraLang ?? undefined) : undefined;

	return (
		// bg-gray-50 stays: it is the app canvas that makes the dashboard's
		// white widget cards read as cards, and is a deliberate app-vs-marketing
		// distinction rather than drift - the same distinction OrgPageHeader
		// draws by not carrying the public site's brand band up here.
		<div className="flex min-h-screen flex-col bg-gray-50">
			<SkipLink />
			<Header
				orgSwitcher={{ currentOrgId: org.id, currentTab: activeTabKey }}
			/>

			<main
				id="main-content"
				tabIndex={-1}
				className="mx-auto w-full max-w-page flex-1 scroll-mt-24 px-4 pt-[var(--main-top-padding)] pb-8 focus:outline-none sm:px-6 lg:px-8"
			>
				{/* App chrome, not the public site's PageHeaderBand - see
				OrgPageHeader for why the org app parts ways with it here. It reads
				QuickActionsContext itself, so the org app's "Create opportunity"
				and dashboard edit-mode actions land in it without the bar. */}
				<OrgPageHeader
					organizationId={org.id}
					orgName={org.name}
					title={pageTitle}
					titleLang={pageTitleLang}
					activeTabKey={activeTabKey}
					back={back}
				/>
				{/* Scoped to this route (remounts, clearing any caught error, whenever
				the location changes) so a render crash in a single tab replaces just
				the content below Header/Footer instead of the whole app - see the
				top-level ErrorBoundary in main.tsx for the last-resort fallback this
				can't catch (e.g. a crash in Header itself). A custom `fallback` is
				required here (unlike AppLayout's equivalent boundary) - the default
				one renders its own <h1>, which would collide with `pageTitle` above,
				and its min-h-screen centering would break out of this Header/Footer
				shell it's nested in. */}
				<ErrorBoundary
					key={location.pathname}
					fallback={
						<div className="flex flex-col items-center justify-center gap-4 px-4 py-16 text-center">
							<p className="max-w-md text-gray-500">
								{t("error.boundaryMessage")}
							</p>
							<div className="flex gap-3">
								<Button
									variant="secondary"
									onClick={() => window.history.back()}
								>
									{t("error.goBack")}
								</Button>
								<Button onClick={() => window.location.reload()}>
									{t("error.reload")}
								</Button>
							</div>
						</div>
					}
				>
					<Suspense
						fallback={
							<div className="flex justify-center py-16">
								<Spinner label={t("common.pageLoading")} size="lg" />
							</div>
						}
					>
						<Outlet
							context={
								{ org, reloadOrg: load, isOrganizer } satisfies OrgAppContext
							}
							// Outlet re-mounts children on org identity change so per-tab state resets cleanly
							key={org.id}
						/>
					</Suspense>
				</ErrorBoundary>
			</main>

			<Footer compact />
		</div>
	);
}

type LoadStatus = "loading" | "ok" | "forbidden" | "notFound" | "error";

export default function OrgAppLayout() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const api = useApiClient();
	const { t } = useTranslation();
	const location = useLocation();
	const online = useOnlineStatus();

	const [org, setOrg] = useState<OrganizationDetailsResponse | null>(null);
	const [status, setStatus] = useState<LoadStatus>("loading");
	const [errorMessage, setErrorMessage] = useState<string | null>(null);
	// #1901: whether the failure behind status "error" never got an HTTP
	// response at all - a stronger, cold-reload-safe offline signal than
	// `online` alone, which can misreport `true` right after a hard reload
	// while genuinely offline (see useOnlineStatus.ts).
	const [errorIsNetworkFailure, setErrorIsNetworkFailure] = useState(false);
	// Guards against a fast org switch (or a manual retry) racing its own
	// previous request: only the response for the most recently issued
	// request is allowed to update state, same pattern as
	// VolunteerOpportunityDetailPage's latestRequestRef.
	const latestRequestRef = useRef(0);

	function load() {
		if (!organizationId) return;
		const requestId = ++latestRequestRef.current;
		setStatus("loading");
		api
			.getOrganizationDetails(organizationId)
			.then((data) => {
				if (requestId !== latestRequestRef.current) return;
				setOrg(data);
				setActiveOrgId(organizationId);
				setStatus("ok");
			})
			.catch((err) => {
				if (requestId !== latestRequestRef.current) return;
				// #1774: the HTTP status is the only thing that tells these
				// failures apart once the client has thrown, and this branch used
				// to keep just the message string - so anything that wasn't a
				// clean 403/404 (notably the 400 an all-zero organization id
				// produces, which NSwag generates no branch for and therefore
				// throws as a bare ApiException) landed in the generic bucket.
				const httpStatus = getApiErrorStatus(err);
				if (httpStatus === 403) {
					setStatus("forbidden");
				} else if (httpStatus === 404 || httpStatus === 400) {
					// 400 means the id in the URL is not a usable organization id at
					// all (OrganizationId.Create rejects Guid.Empty) - this endpoint
					// takes no other input, so there is nothing else a 400 can be
					// about, and "that organization does not exist" is the honest
					// reading of it rather than "something went wrong".
					setStatus("notFound");
				} else {
					// Covers everything that isn't a permanent 400/403/404 - a
					// dropped connection, a 500, an unexpected exception - so it gets
					// a recoverable state instead of being mislabeled as "not
					// authorized" (#1224). Whether that state offers a retry is
					// decided at render time from the live connection status: while
					// the browser reports itself offline, retrying cannot work.
					setErrorMessage(getApiErrorMessage(err, t("error.serverError")));
					setErrorIsNetworkFailure(isNetworkError(err));
					setStatus("error");
				}
			});
	}

	useEffect(() => {
		load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	// The primary recovery path stays the `online` event, not the offline
	// state's manual retry button (#2065) - it needs no click and covers the
	// common case. Scoped to a failed load: a shell that is already showing an
	// organization is never refetched out from under the user just because a
	// tunnel ended.
	useEffect(() => {
		if (online && status === "error") load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [online]);

	// #9: every tab now lives under /dashboard/... (App.tsx's pathless
	// "dashboard" parent route), so the segment right after "dashboard" -
	// not the first segment, which is always "dashboard" itself - is what
	// identifies the active tab. Matches that segment directly rather than
	// endsWith(), so nested routes under a tab (e.g.
	// opportunities/:opportunityId/engagements) still keep that tab active.
	// Missing entirely (bare /dashboard) means the dashboard tab itself.
	const tabSegment =
		location.pathname
			.slice(`/app/${organizationId}/dashboard`.length)
			.split("/")
			.filter(Boolean)[0] ?? "dashboard";
	const activeTabKey =
		ORG_TABS.find((tab) => tab.key === tabSegment)?.key ?? "dashboard";
	const activeTab =
		ORG_TABS.find((tab) => tab.key === activeTabKey) ?? ORG_TABS[0];
	const activeTabLabel = t(activeTab.labelKey);

	if (status === "loading") {
		return (
			<main className="flex min-h-screen items-center justify-center bg-gray-50">
				<Spinner label={t("orgDashboard.loading")} size="lg" />
			</main>
		);
	}

	// RouteState renders no landmark of its own - the org app bypasses
	// AppLayout entirely, so every one of these states has to supply the
	// <main> (and the app canvas background) itself.
	const backToSite = { to: "/", label: t("orgApp.backToSite") };
	function stateScreen(node: ReactNode) {
		return (
			<main className="flex min-h-screen flex-col justify-center bg-gray-50">
				{node}
			</main>
		);
	}

	if (status === "notFound") {
		// Not the site-wide NotFoundPage any more (#1774): "Page not found" is
		// true of a URL that routes nowhere, but this URL routes fine - it names
		// an organization that does not exist, which is what the copy should
		// say.
		return stateScreen(
			<RouteState
				variant="notFound"
				title={t("orgApp.notFoundTitle")}
				message={t("orgApp.notFoundMessage")}
				action={backToSite}
			/>,
		);
	}

	if (status === "error") {
		// Offline is a distinct, self-clearing situation, not a server fault -
		// and while it lasts, the retry button the error state offers is a lie
		// (#1774). The effect above reloads as soon as the connection is back.
		// #1901: `online` alone misses a hard reload/cold PWA launch while
		// genuinely offline (navigator.onLine can misreport `true` there) - a
		// failure that never got an HTTP response at all is trusted just as
		// much, since that could only happen with no working connection.
		return stateScreen(
			online && !errorIsNetworkFailure ? (
				<RouteState
					variant="error"
					title={t("error.boundaryTitle")}
					message={errorMessage ?? t("error.serverError")}
					onRetry={load}
					action={backToSite}
				/>
			) : (
				<RouteState
					variant="offline"
					title={t("routeState.offline.title")}
					message={t("routeState.offline.message")}
					onRetry={load}
					action={backToSite}
				/>
			),
		);
	}

	if (status === "forbidden" || !org) {
		return stateScreen(
			<RouteState
				variant="forbidden"
				title={t("orgApp.notAuthorized")}
				message={t("orgApp.notAuthorizedMessage")}
				action={backToSite}
			/>,
		);
	}

	return (
		<OrgBreadcrumbProvider>
			<QuickActionsProvider>
				<OrgAppShell
					organizationId={organizationId}
					org={org}
					activeTabKey={activeTabKey}
					activeTabLabel={activeTabLabel}
					load={load}
				/>
			</QuickActionsProvider>
		</OrgBreadcrumbProvider>
	);
}
