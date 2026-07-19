import { useEffect, useState } from "react";
import { Link, Outlet, useLocation, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrganizationDetailsResponse } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import { setActiveOrgId } from "../lib/activeOrg";
import {
	OrgBreadcrumbProvider,
	useOrgBreadcrumbExtra,
} from "../contexts/OrgBreadcrumbContext";
import Header from "../components/Header";

export interface OrgAppContext {
	org: OrganizationDetailsResponse;
	reloadOrg: () => void;
}

const TABS = [
	{ key: "dashboard", labelKey: "orgOverview.tabCalendar" },
	{ key: "opportunities", labelKey: "orgOverview.tabOpportunities" },
	{ key: "members", labelKey: "orgOverview.tabMembers" },
	{ key: "settings", labelKey: "orgOverview.tabSettings" },
] as const;

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

	const breadcrumbItems = extra
		? [
				{
					label: activeTabLabel,
					href: `/app/${organizationId}/${activeTabKey}`,
				},
				{ label: extra },
			]
		: [{ label: activeTabLabel }];

	return (
		<div className="flex min-h-screen flex-col bg-gray-50">
			<Header
				orgSwitcher={{ currentOrgId: org.id, currentTab: activeTabKey }}
				breadcrumb={{
					homeHref: `/app/${organizationId}/dashboard`,
					items: breadcrumbItems,
				}}
			/>

			<main className="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-6 lg:px-8">
				<nav aria-label={t("orgApp.tabsLabel")} className="mb-6 sm:mb-8">
					<div className="flex gap-6 overflow-x-auto border-b border-gray-200 [-ms-overflow-style:none] [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
						{TABS.map((tab) => (
							<Link
								key={tab.key}
								to={`/app/${organizationId}/${tab.key}`}
								aria-current={activeTabKey === tab.key ? "page" : undefined}
								className={`shrink-0 whitespace-nowrap border-b-2 pb-3 pt-3 text-sm font-medium transition-colors ${
									activeTabKey === tab.key
										? "border-brand-700 text-brand-700"
										: "border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700"
								}`}
							>
								{t(tab.labelKey)}
							</Link>
						))}
					</div>
				</nav>

				<Outlet
					context={{ org, reloadOrg: load } satisfies OrgAppContext}
					// Outlet re-mounts children on org identity change so per-tab state resets cleanly
					key={org.id}
				/>
			</main>

			<footer className="border-t border-gray-200 bg-white py-4 text-center text-xs text-gray-500">
				<Link to="/impressum" className="hover:text-gray-600">
					{t("footer.imprint")}
				</Link>
				<span className="mx-2">&middot;</span>
				<Link to="/datenschutz" className="hover:text-gray-600">
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

	// Matches the tab segment directly rather than endsWith(), so nested
	// routes under a tab (e.g. opportunities/:opportunityId/engagements) still
	// keep that tab active.
	const tabSegment = location.pathname
		.slice(`/app/${organizationId}/`.length)
		.split("/")[0];
	const activeTabKey =
		TABS.find((tab) => tab.key === tabSegment)?.key ?? "dashboard";
	const activeTab = TABS.find((tab) => tab.key === activeTabKey) ?? TABS[0];
	const activeTabLabel = t(activeTab.labelKey);

	if (loading) {
		return (
			<div className="flex min-h-screen items-center justify-center bg-gray-50">
				<span className="text-gray-500">{t("orgDashboard.loading")}</span>
			</div>
		);
	}

	if (forbidden || !org) {
		return (
			<div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-gray-50 px-4 text-center">
				<h1 className="text-xl font-semibold text-gray-900">
					{t("orgApp.notAuthorized")}
				</h1>
				<Link
					to="/"
					className="rounded-lg bg-brand-700 px-4 py-2 text-sm font-medium text-white hover:bg-brand-800"
				>
					{t("orgApp.backToSite")}
				</Link>
			</div>
		);
	}

	return (
		<OrgBreadcrumbProvider>
			<OrgAppShell
				organizationId={organizationId}
				org={org}
				activeTabKey={activeTabKey}
				activeTabLabel={activeTabLabel}
				load={load}
			/>
		</OrgBreadcrumbProvider>
	);
}
