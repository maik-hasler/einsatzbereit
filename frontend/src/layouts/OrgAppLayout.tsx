import { useEffect, useState } from "react";
import { Link, Outlet, useLocation, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrganizationDetailsResponse } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import { setActiveOrgId } from "../lib/activeOrg";
import { ORG_TABS, orgTabPath } from "../lib/orgTabs";
import {
	OrgBreadcrumbProvider,
	useOrgBreadcrumbExtra,
} from "../contexts/OrgBreadcrumbContext";
import {
	QuickActionsProvider,
	useQuickActionsList,
} from "../contexts/QuickActionsContext";
import Header from "../components/Header/Header";
import Spinner from "../components/Spinner";
import Button from "../components/Button";

export interface OrgAppContext {
	org: OrganizationDetailsResponse;
	reloadOrg: () => void;
}

// Renders the org app shell body inside OrgBreadcrumbProvider so it can read
// the nested-page breadcrumb extra (see useSetOrgBreadcrumbExtra) and pass
// the resulting trail into Header's shared breadcrumb/action-bar prop -
// normally just the active tab's own name, current-page style; a nested page
// (e.g. engagement management) can add a further segment, which demotes the
// tab name to a link back to that tab.
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
	const { t } = useTranslation();
	const extra = useOrgBreadcrumbExtra();
	const quickActions = useQuickActionsList();

	// Now that the tab bar is gone (dashboard UX redesign), the breadcrumb is
	// the only thing that shows an organizer where they are relative to the
	// dashboard - so every non-dashboard tab gets an explicit leading
	// "Dashboard" crumb (linking back to it) instead of starting directly at
	// the tab's own name.
	const dashboardTab = ORG_TABS.find((tab) => tab.key === "dashboard");
	const isDashboardTab = activeTabKey === "dashboard";
	const breadcrumbItems = [
		...(isDashboardTab || !organizationId || !dashboardTab
			? []
			: [
					{
						label: t(dashboardTab.labelKey),
						href: `/app/${organizationId}/dashboard`,
					},
				]),
		{
			label: activeTabLabel,
			href:
				extra && organizationId
					? orgTabPath(organizationId, activeTabKey)
					: undefined,
		},
		...(extra ? [{ label: extra }] : []),
	];

	return (
		<div className="flex min-h-screen flex-col bg-gray-50">
			<Header
				orgSwitcher={{ currentOrgId: org.id, currentTab: activeTabKey }}
				breadcrumb={{
					homeHref: `/app/${organizationId}/dashboard`,
					items: breadcrumbItems,
					actions: quickActions,
				}}
			/>

			<main className="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-6 lg:px-8">
				<Outlet
					context={{ org, reloadOrg: load } satisfies OrgAppContext}
					// Outlet re-mounts children on org identity change so per-tab state resets cleanly
					key={org.id}
				/>
			</main>

			<footer className="border-t border-gray-200 bg-white py-4 text-center text-xs text-gray-500">
				<Link to="/imprint" className="hover:text-gray-600">
					{t("footer.imprint")}
				</Link>
				<span className="mx-2">&middot;</span>
				<Link to="/privacy-policy" className="hover:text-gray-600">
					{t("footer.privacy")}
				</Link>
			</footer>
		</div>
	);
}

export default function OrgAppLayout() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const api = useApiClient();
	const { t } = useTranslation();
	const location = useLocation();

	const [org, setOrg] = useState<OrganizationDetailsResponse | null>(null);
	const [loading, setLoading] = useState(true);
	const [forbidden, setForbidden] = useState(false);

	function load() {
		if (!organizationId) return;
		setLoading(true);
		setForbidden(false);
		api
			.getOrganizationDetails(organizationId)
			.then((data) => {
				setOrg(data);
				setActiveOrgId(organizationId);
			})
			.catch(() => setForbidden(true))
			.finally(() => setLoading(false));
	}

	useEffect(() => {
		load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	usePageTitle(org?.name ?? t("orgDashboard.title"));

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

	if (loading) {
		return (
			<div className="flex min-h-screen items-center justify-center bg-gray-50">
				<Spinner label={t("orgDashboard.loading")} size="lg" />
			</div>
		);
	}

	if (forbidden || !org) {
		return (
			<div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-gray-50 px-4 text-center">
				<h1 className="text-xl font-semibold text-gray-900">
					{t("orgApp.notAuthorized")}
				</h1>
				<Button to="/">{t("orgApp.backToSite")}</Button>
			</div>
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
