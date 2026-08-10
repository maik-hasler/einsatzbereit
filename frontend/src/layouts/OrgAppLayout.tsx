import { Suspense, useEffect, useRef, useState } from "react";
import { Outlet, useLocation, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrganizationDetailsResponse } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { useAchievementNotifier } from "../hooks/useAchievementNotifier";
import { setActiveOrgId } from "../lib/activeOrg";
import { ORG_TABS, orgTabPath } from "../lib/orgTabs";
import { statusTitleClass } from "../lib/headingClasses";
import {
	getApiErrorMessage,
	isApiForbiddenError,
	isApiNotFoundError,
} from "../lib/apiError";
import {
	OrgBreadcrumbProvider,
	useOrgBreadcrumbExtra,
} from "../contexts/OrgBreadcrumbContext";
import { QuickActionsProvider } from "../contexts/QuickActionsContext";
import Header from "../components/Header/Header";
import PageHeaderBand from "../components/PageHeaderBand";
import Footer from "../components/Footer";
import Spinner from "../components/Spinner";
import SkipLink from "../components/SkipLink";
import Button from "../components/Button";
import ErrorBanner from "../components/ErrorBanner";
import ErrorBoundary from "../components/ErrorBoundary";
import NotFoundPage from "../pages/NotFoundPage";

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
// band shows the active tab's own name; a nested page (e.g. engagement
// management) overrides it with its own, which demotes the tab name to the
// band's back link.
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
	// Answered from organization_membership (see OrganizationDetailsResponse's
	// requestingUserRole), not by scanning the Keycloak-sourced org.members roster
	// below - so the shell's own nav/actions still know the caller's role even
	// when that roster couldn't be loaded (#1709).
	const isOrganizer = org.requestingUserRole === "Organizer";

	// The way back, one level up. This used to be a full BreadcrumbBar strip
	// below the header - the last surface in the app still using it, and the
	// reason the org app read as a third visual system next to the public site
	// and the account area. PageHeaderBand carries it now, same as everywhere
	// else, so "back" is a single link rather than a trail: from a nested page
	// to its tab, and from a tab to the dashboard.
	const dashboardTab = ORG_TABS.find((tab) => tab.key === "dashboard");
	const isDashboardTab = activeTabKey === "dashboard";
	const back =
		extra && organizationId
			? {
					href: orgTabPath(organizationId, activeTabKey),
					label: activeTabLabel,
				}
			: !isDashboardTab && organizationId && dashboardTab
				? {
						href: `/app/${organizationId}/dashboard`,
						label: t(dashboardTab.labelKey),
					}
				: null;

	// #973: OrgAppShell is the only place that knows the current tab/nested-page
	// title (activeTabLabel/extra, same source as the breadcrumb's last item) -
	// render it as a real h1 here rather than in each of the four tab pages, so
	// axe's page-has-heading-one/heading-order rules pass on every org app page.
	const pageTitle = extra ?? activeTabLabel;

	return (
		// bg-gray-50 stays: it is the app canvas that makes the dashboard's
		// white widget cards read as cards, and is a deliberate app-vs-marketing
		// distinction rather than drift. What was drift - the breadcrumb strip
		// and a bare <h1> where every other page has the brand band - is gone.
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
				{/* Same band as the public site and the account area. It reads
				QuickActionsContext itself, so the org app's "Create opportunity"
				and dashboard edit-mode actions land in it without the bar. */}
				<PageHeaderBand
					eyebrow={org.name}
					title={pageTitle}
					backHref={back?.href ?? `/app/${organizationId}/dashboard`}
					backLabel={back?.label ?? t("orgOverview.tabDashboard")}
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

	const [org, setOrg] = useState<OrganizationDetailsResponse | null>(null);
	const [status, setStatus] = useState<LoadStatus>("loading");
	const [errorMessage, setErrorMessage] = useState<string | null>(null);
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
				if (isApiForbiddenError(err)) {
					setStatus("forbidden");
				} else if (isApiNotFoundError(err)) {
					setStatus("notFound");
				} else {
					// Covers everything that isn't a permanent 403/404 - a dropped
					// connection, a 500, an unexpected exception - so it gets a
					// recoverable "try again" state instead of being mislabeled as
					// "not authorized" (#1224).
					setErrorMessage(getApiErrorMessage(err, t("error.serverError")));
					setStatus("error");
				}
			});
	}

	useEffect(() => {
		load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

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

	if (status === "notFound") {
		// NotFoundPage has no <main> of its own - it relies on AppLayout to
		// supply one on its usual wildcard route. OrgAppLayout bypasses
		// AppLayout entirely, so it must supply the landmark here instead.
		return (
			<main>
				<NotFoundPage />
			</main>
		);
	}

	if (status === "error") {
		return (
			<main className="flex min-h-screen flex-col items-center justify-center gap-4 bg-gray-50 px-4 text-center">
				<h1 className={`text-gray-900 ${statusTitleClass}`}>
					{t("error.boundaryTitle")}
				</h1>
				{/* role="alert"/aria-live (via ErrorBanner) so a retry that fails again
				- re-rendering this same branch, no navigation - is still announced to
				screen reader users, not just sighted ones. */}
				<ErrorBanner
					message={errorMessage ?? t("error.serverError")}
					className="max-w-md"
				/>
				<div className="flex gap-3">
					<Button onClick={load}>{t("orgApp.retry")}</Button>
					<Button to="/" variant="secondary">
						{t("orgApp.backToSite")}
					</Button>
				</div>
			</main>
		);
	}

	if (status === "forbidden" || !org) {
		return (
			<main className="flex min-h-screen flex-col items-center justify-center gap-4 bg-gray-50 px-4 text-center">
				<h1 className={`text-gray-900 ${statusTitleClass}`}>
					{t("orgApp.notAuthorized")}
				</h1>
				<Button to="/">{t("orgApp.backToSite")}</Button>
			</main>
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
