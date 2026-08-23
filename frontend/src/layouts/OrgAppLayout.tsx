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

	isOrganizer: boolean;
}

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
	useAchievementNotifier();
	const { t } = useTranslation();
	const location = useLocation();
	const extra = useOrgBreadcrumbExtra();
	const extraLang = useOrgBreadcrumbExtraLang();

	const isOrganizer = org.requestingUserRole === "Organizer";

	const back =
		extra && organizationId
			? {
					href: orgTabPath(organizationId, activeTabKey),
					label: activeTabLabel,
				}
			: null;

	const pageTitle = extra ?? activeTabLabel;

	const pageTitleLang = extra ? (extraLang ?? undefined) : undefined;

	return (
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
				<OrgPageHeader
					organizationId={org.id}
					orgName={org.name}
					title={pageTitle}
					titleLang={pageTitleLang}
					activeTabKey={activeTabKey}
					back={back}
				/>

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

	const [errorIsNetworkFailure, setErrorIsNetworkFailure] = useState(false);

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

				const httpStatus = getApiErrorStatus(err);
				if (httpStatus === 403) {
					setStatus("forbidden");
				} else if (httpStatus === 404 || httpStatus === 400) {
					setStatus("notFound");
				} else {
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

	useEffect(() => {
		if (online && status === "error") load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [online]);

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

	const backToSite = { to: "/", label: t("orgApp.backToSite") };
	function stateScreen(node: ReactNode) {
		return (
			<main className="flex min-h-screen flex-col justify-center bg-gray-50">
				{node}
			</main>
		);
	}

	if (status === "notFound") {
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
