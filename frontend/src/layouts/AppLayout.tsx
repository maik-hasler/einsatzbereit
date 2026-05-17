import { Outlet } from "react-router";
import Header from "../components/Header";
import Footer from "../components/Footer";
import Breadcrumb from "../components/Breadcrumb";
import { ToolbarProvider, useToolbarConfig } from "../contexts/ToolbarContext";

function ToolbarStrip() {
	const config = useToolbarConfig();
	if (!config) return null;
	return (
		<div className="border-b border-gray-100 bg-white">
			<div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
				<div className="flex items-center h-10 min-w-0">
					<Breadcrumb items={config.breadcrumbs} />
				</div>
			</div>
		</div>
	);
}

function AppLayoutInner() {
	return (
		<div className="min-h-screen flex flex-col">
			<Header />
			<ToolbarStrip />
			<main className="mx-auto max-w-7xl px-4 py-16 sm:px-6 lg:px-8 flex-1 w-full">
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
