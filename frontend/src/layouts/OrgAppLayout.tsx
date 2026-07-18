import { useEffect, useState } from "react";
import {
	Link,
	Outlet,
	useLocation,
	useNavigate,
	useParams,
} from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import type { OrganizationDetailsResponse } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import { useAccountMenu } from "../hooks/useAccountMenu";
import { setActiveOrgId } from "../lib/activeOrg";
import OrganizationSwitcher from "../components/OrganizationSwitcher";
import LanguageSelector from "../components/LanguageSelector";
import AccountControls from "../components/AccountControls";

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

function getInitials(name: string): string {
	const parts = name.trim().split(/\s+/);
	if (parts.length > 1) return (parts[0][0] + parts[1][0]).toUpperCase();
	return name.slice(0, 2).toUpperCase();
}

export default function OrgAppLayout() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const api = useApiClient();
	const auth = useAuth();
	const { t } = useTranslation();
	const location = useLocation();
	const navigate = useNavigate();
	const menu = useAccountMenu();

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

	const activeTabKey =
		TABS.find((tab) => location.pathname.endsWith(`/${tab.key}`))?.key ??
		"dashboard";
	const activeTab = TABS.find((tab) => tab.key === activeTabKey) ?? TABS[0];

	const displayName = (auth.user?.profile?.name ??
		auth.user?.profile?.preferred_username ??
		"") as string;
	const initials = getInitials(displayName || "?");

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
		<div className="flex min-h-screen flex-col bg-gray-50">
			<header className="border-b border-gray-200 bg-white">
				<div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-4 px-4 sm:px-6 lg:px-8">
					<Link to="/" className="flex shrink-0 items-center">
						<img src="/logo.svg" alt={t("brand.name")} className="h-8" />
					</Link>

					<div className="min-w-0 flex-1 sm:flex-none">
						<OrganizationSwitcher
							currentOrgId={org.id}
							currentTab={activeTabKey}
						/>
					</div>

					<div className="flex shrink-0 items-center gap-3">
						<AccountControls
							menu={menu}
							displayName={displayName}
							initials={initials}
							onSignOut={() => auth.signoutRedirect()}
							onNotificationNavigate={(actionUrl) =>
								navigate(actionUrl ?? "/my-engagements")
							}
						/>

						<div className="h-6 w-px bg-gray-200" />

						<LanguageSelector />
					</div>
				</div>
			</header>

			<div className="border-b border-gray-200 bg-white">
				<div className="mx-auto max-w-7xl px-4 py-3 sm:px-6 lg:px-8">
					<nav
						aria-label={t("breadcrumb.label")}
						className="flex min-w-0 items-center gap-1.5 text-sm"
					>
						<Link
							to={`/app/${organizationId}/dashboard`}
							aria-label={t("breadcrumb.home")}
							className="flex shrink-0 items-center text-gray-400 transition-colors hover:text-brand-700"
						>
							<svg
								className="h-4 w-4"
								fill="none"
								viewBox="0 0 24 24"
								strokeWidth="1.5"
								stroke="currentColor"
								aria-hidden="true"
							>
								<path
									strokeLinecap="round"
									strokeLinejoin="round"
									d="M2.25 12l8.954-8.955c.44-.439 1.152-.439 1.591 0L21.75 12M4.5 9.75v10.125c0 .621.504 1.125 1.125 1.125H9.75v-4.875c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21h4.125c.621 0 1.125-.504 1.125-1.125V9.75M8.25 21h8.25"
								/>
							</svg>
						</Link>
						<span className="shrink-0 text-gray-300" aria-hidden="true">
							&rsaquo;
						</span>
						<span
							className="truncate font-medium text-gray-900"
							aria-current="page"
						>
							{t(activeTab.labelKey)}
						</span>
					</nav>
				</div>
			</div>

			<main className="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-6 lg:px-8">
				<nav aria-label={t("orgApp.tabsLabel")} className="mb-6 sm:mb-8">
					<div className="flex gap-6 border-b border-gray-200">
						{TABS.map((tab) => (
							<Link
								key={tab.key}
								to={`/app/${organizationId}/${tab.key}`}
								aria-current={activeTabKey === tab.key ? "page" : undefined}
								className={`border-b-2 pb-3 pt-3 text-sm font-medium transition-colors ${
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
