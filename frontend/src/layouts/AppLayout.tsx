import { Outlet } from "react-router";
import Header from "../components/Header/Header";
import Footer from "../components/Footer";
import { useAchievementNotifier } from "../hooks/useAchievementNotifier";
import { ToolbarProvider, useToolbarConfig } from "../contexts/ToolbarContext";
import {
	QuickActionsProvider,
	useQuickActionsList,
} from "../contexts/QuickActionsContext";

function AppLayoutInner() {
	useAchievementNotifier();
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
		<div className="min-h-screen flex flex-col">
			<Header breadcrumb={breadcrumb} />
			<main className="mx-auto max-w-7xl px-4 pb-16 pt-6 flex-1 w-full sm:px-6 sm:pt-10 lg:px-8 lg:pt-12">
				<Outlet />
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
