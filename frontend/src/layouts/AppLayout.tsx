import { Outlet } from "react-router";
import Header from "../components/Header";
import Footer from "../components/Footer";
import Breadcrumb from "../components/Breadcrumb";
import { useAchievementNotifier } from "../hooks/useAchievementNotifier";
import { ToolbarProvider, useToolbarConfig } from "../contexts/ToolbarContext";

function ToolbarBreadcrumb() {
	const config = useToolbarConfig();
	if (!config || config.breadcrumbs.length === 0) return null;
	return (
		<div className="mb-4">
			<Breadcrumb items={config.breadcrumbs} />
		</div>
	);
}

function AppLayoutInner() {
	useAchievementNotifier();
	return (
		<div className="min-h-screen flex flex-col">
			<Header />
			<main className="mx-auto max-w-7xl px-4 pb-16 pt-6 flex-1 w-full sm:px-6 sm:pt-10 lg:px-8 lg:pt-12">
				<ToolbarBreadcrumb />
				<Outlet />
			</main>
			<Footer />
		</div>
	);
}

export default function AppLayout() {
	return (
		<ToolbarProvider>
			<AppLayoutInner />
		</ToolbarProvider>
	);
}
