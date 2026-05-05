import { Routes, Route } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import AppLayout from "./layouts/AppLayout";
import ProtectedRoute from "./layouts/ProtectedRoute";
import HomePage from "./pages/HomePage";
import DatenschutzPage from "./pages/DatenschutzPage";
import ImpressumPage from "./pages/ImpressumPage";
import OrganizationSettingsPage from "./pages/OrganizationSettingsPage";
import VolunteerOpportunityDetailPage from "./pages/VolunteerOpportunityDetailPage";
import MyEngagementsPage from "./pages/MyEngagementsPage";
import EngagementManagementPage from "./pages/EngagementManagementPage";
import AccountPage from "./pages/AccountPage";
import NotFoundPage from "./pages/NotFoundPage";
import OrganizationProfilePage from "./pages/OrganizationProfilePage";

function CallbackPage() {
	const auth = useAuth();
	const { t } = useTranslation();
	if (auth.error) {
		return (
			<div className="flex min-h-screen items-center justify-center">
				<span className="text-red-600">
					{t("auth.authError", { message: auth.error.message })}
				</span>
			</div>
		);
	}
	return (
		<div className="flex min-h-screen items-center justify-center">
			<span className="text-gray-500">{t("auth.completing")}</span>
		</div>
	);
}

export default function App() {
	return (
		<Routes>
			<Route path="/callback" element={<CallbackPage />} />
			<Route element={<AppLayout />}>
				<Route path="/" element={<HomePage />} />
				<Route path="/datenschutz" element={<DatenschutzPage />} />
				<Route path="/impressum" element={<ImpressumPage />} />
				<Route
					path="/volunteer-opportunities/:opportunityId"
					element={<VolunteerOpportunityDetailPage />}
				/>
				<Route
					path="/organizations/:organizationId"
					element={<OrganizationProfilePage />}
				/>
				<Route
					path="/my-engagements"
					element={
						<ProtectedRoute>
							<MyEngagementsPage />
						</ProtectedRoute>
					}
				/>
				<Route
					path="/volunteer-opportunities/:opportunityId/engagements"
					element={
						<ProtectedRoute>
							<EngagementManagementPage />
						</ProtectedRoute>
					}
				/>
				<Route
					path="/account"
					element={
						<ProtectedRoute>
							<AccountPage />
						</ProtectedRoute>
					}
				/>
				<Route
					path="/organizations/:organizationId/settings"
					element={
						<ProtectedRoute>
							<OrganizationSettingsPage />
						</ProtectedRoute>
					}
				/>
				<Route path="*" element={<NotFoundPage />} />
			</Route>
		</Routes>
	);
}
