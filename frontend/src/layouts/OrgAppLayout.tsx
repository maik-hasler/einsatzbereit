import { useEffect, useRef, useState } from "react";
import { Link, Outlet, useLocation, useParams } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import type { OrganizationDetailsResponse } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import OrganizationSwitcher from "../components/OrganizationSwitcher";
import LanguageSelector from "../components/LanguageSelector";

export interface OrgAppContext {
	org: OrganizationDetailsResponse;
	reloadOrg: () => void;
}

const TABS = [
	{ key: "dashboard", labelKey: "orgOverview.tabCalendar" },
	{ key: "engagements", labelKey: "orgOverview.tabEngagements" },
	{ key: "members", labelKey: "orgOverview.tabMembers" },
	{ key: "settings", labelKey: "orgOverview.tabSettings" },
] as const;

function getInitial(name: string): string {
	return name.trim().charAt(0).toUpperCase() || "?";
}

export default function OrgAppLayout() {
	const { orgSlug } = useParams<{ orgSlug: string }>();
	const api = useApiClient();
	const auth = useAuth();
	const { t } = useTranslation();
	const location = useLocation();

	const [org, setOrg] = useState<OrganizationDetailsResponse | null>(null);
	const [loading, setLoading] = useState(true);
	const [forbidden, setForbidden] = useState(false);
	const [userMenuOpen, setUserMenuOpen] = useState(false);
	const userMenuRef = useRef<HTMLDivElement>(null);

	function load() {
		if (!orgSlug) return;
		setLoading(true);
		setForbidden(false);
		api
			.getOrganizationDetails(orgSlug)
			.then(setOrg)
			.catch(() => setForbidden(true))
			.finally(() => setLoading(false));
	}

	useEffect(() => {
		load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [orgSlug]);

	useEffect(() => {
		const handler = (e: MouseEvent) => {
			if (
				userMenuRef.current &&
				!userMenuRef.current.contains(e.target as Node)
			) {
				setUserMenuOpen(false);
			}
		};
		document.addEventListener("click", handler);
		return () => document.removeEventListener("click", handler);
	}, []);

	usePageTitle(org?.name ?? t("orgDashboard.title"));

	const activeTabKey =
		TABS.find((tab) => location.pathname.endsWith(`/${tab.key}`))?.key ??
		"dashboard";

	const displayName = (auth.user?.profile?.name ??
		auth.user?.profile?.preferred_username ??
		"") as string;

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
					<Link
						to="/"
						className="flex shrink-0 items-center gap-2 text-sm font-medium text-gray-500 transition-colors hover:text-brand-700"
					>
						<svg
							className="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="2"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M10.5 19.5 3 12m0 0 7.5-7.5M3 12h18"
							/>
						</svg>
						<span className="hidden sm:inline">{t("orgApp.backToSite")}</span>
					</Link>

					<div className="min-w-0 flex-1 sm:flex-none">
						<OrganizationSwitcher
							currentOrgId={org.id}
							currentTab={activeTabKey}
						/>
					</div>

					<div className="flex shrink-0 items-center gap-3">
						<LanguageSelector />

						<div className="relative" ref={userMenuRef}>
							<button
								type="button"
								onClick={() => setUserMenuOpen((o) => !o)}
								aria-label={t("nav.userMenu")}
								aria-expanded={userMenuOpen}
								className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-700 text-sm font-semibold text-white hover:ring-2 hover:ring-brand-200"
							>
								{getInitial(displayName)}
							</button>

							{userMenuOpen && (
								<div className="absolute right-0 top-full z-50 mt-2 w-48 rounded-lg border border-gray-200 bg-white shadow-lg">
									<div className="py-1">
										<Link
											to="/profile"
											className="block px-4 py-2.5 text-sm text-gray-700 hover:bg-brand-50 hover:text-brand-700"
											onClick={() => setUserMenuOpen(false)}
										>
											{t("nav.myProfile")}
										</Link>
										<button
											type="button"
											onClick={() => auth.signoutRedirect()}
											className="flex w-full items-center px-4 py-2.5 text-left text-sm text-red-600 hover:bg-red-50 hover:text-red-700"
										>
											{t("nav.signOut")}
										</button>
									</div>
								</div>
							)}
						</div>
					</div>
				</div>

				<nav
					className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8"
					aria-label={org.name}
				>
					<div className="flex gap-6 border-t border-gray-100">
						{TABS.map((tab) => (
							<Link
								key={tab.key}
								to={`/app/${orgSlug}/${tab.key}`}
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
			</header>

			<main className="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-6 lg:px-8">
				<Outlet
					context={{ org, reloadOrg: load } satisfies OrgAppContext}
					// Outlet re-mounts children on org identity change so per-tab state resets cleanly
					key={org.id}
				/>
			</main>

			<footer className="border-t border-gray-200 bg-white py-4 text-center text-xs text-gray-400">
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
