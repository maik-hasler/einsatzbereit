import { Suspense } from "react";
import { Outlet, useLocation } from "react-router";
import { useTranslation } from "react-i18next";
import Header from "../components/Header/Header";
import Footer from "../components/Footer";
import Spinner from "../components/Spinner";
import SkipLink from "../components/SkipLink";
import ErrorBoundary from "../components/ErrorBoundary";
import { useAchievementNotifier } from "../hooks/useAchievementNotifier";
import { ToolbarProvider, useToolbarConfig } from "../contexts/ToolbarContext";
import {
	QuickActionsProvider,
	useQuickActionsList,
} from "../contexts/QuickActionsContext";

function AppLayoutInner() {
	useAchievementNotifier();
	const { t } = useTranslation();
	const location = useLocation();
	const toolbarConfig = useToolbarConfig();
	const quickActions = useQuickActionsList();
	const breadcrumb =
		toolbarConfig && toolbarConfig.breadcrumbs.length > 0
			? {
					homeHref: "/",
					items: toolbarConfig.breadcrumbs,
					actions: quickActions,
				}
			: undefined;
	return (
		<div className="flex min-h-screen flex-col">
			<SkipLink />
			<Header breadcrumb={breadcrumb} />
			<main
				id="main-content"
				tabIndex={-1}
				className="mx-auto w-full max-w-7xl flex-1 scroll-mt-24 px-4 pt-[var(--main-top-padding)] pb-16 focus:outline-none sm:px-6 lg:px-8"
			>
				{/* Scoped to this route (remounts, clearing any caught error, whenever
				the location changes) so a render crash in a single page replaces
				just the content below Header/Footer instead of the whole app - see
				the top-level ErrorBoundary in main.tsx for the last-resort fallback
				this can't catch (e.g. a crash in Header itself). */}
				<ErrorBoundary key={location.pathname}>
					<Suspense
						fallback={
							<div className="flex justify-center py-16">
								<Spinner label={t("common.pageLoading")} size="lg" />
							</div>
						}
					>
						<Outlet />
					</Suspense>
				</ErrorBoundary>
			</main>
			<Footer />
		</div>
	);
}

export default function AppLayout() {
	return (
		<ToolbarProvider>
			<QuickActionsProvider>
				<AppLayoutInner />
			</QuickActionsProvider>
		</ToolbarProvider>
	);
}
