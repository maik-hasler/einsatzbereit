import {
	Routes,
	Route,
	Navigate,
	useParams,
	useSearchParams,
} from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import AppLayout from "./layouts/AppLayout";
import ProtectedRoute from "./layouts/ProtectedRoute";
import OrgAppLayout from "./layouts/OrgAppLayout";
import HomePage from "./pages/HomePage";
import DatenschutzPage from "./pages/DatenschutzPage";
import ImpressumPage from "./pages/ImpressumPage";
import VolunteerOpportunityDetailPage from "./pages/VolunteerOpportunityDetailPage";
import EngagementManagementPage from "./pages/EngagementManagementPage";
import ProfileOverviewPage from "./pages/ProfileOverviewPage";
import NotFoundPage from "./pages/NotFoundPage";
import OrganizationProfilePage from "./pages/OrganizationProfilePage";
import OrganizationOverviewPage from "./pages/OrganizationOverviewPage";
import UserAchievementsPage from "./pages/UserAchievementsPage";
import AdminOrganizationsPage from "./pages/AdminOrganizationsPage";
import UserProfilePage from "./pages/UserProfilePage";

// Pre-#691 URLs (bookmarks, sent links) redirect into the organizer app.
// The old dashboard page read its tab from ?tab=, so an old
// .../dashboard?tab=members link is honored here rather than always
// landing on the default tab.
const DEEP_LINK_TAB_SEGMENTS: Record<string, string> = {
	engagements: "engagements",
	members: "members",
	settings: "settings",
};

function RedirectToOrgApp({ tab }: { tab: string }) {
	const { organizationId } = useParams<{ organizationId: string }>();
	const [searchParams] = useSearchParams();
	const queryTab = searchParams.get("tab");
	const resolvedTab = (queryTab && DEEP_LINK_TAB_SEGMENTS[queryTab]) || tab;
	return <Navigate to={`/app/${organizationId}/${resolvedTab}`} replace />;
}

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
					element={<Navigate to="/profile?tab=engagements" replace />}
				/>
				<Route
					path="/volunteer-opportunities/:opportunityId/engagements"
					element={
						<ProtectedRoute>
							<EngagementManagementPage />
						</ProtectedRoute>
					}
				/>
				<Route path="/account" element={<Navigate to="/profile" replace />} />
				<Route
					path="/profile"
					element={
						<ProtectedRoute>
							<ProfileOverviewPage />
						</ProtectedRoute>
					}
				/>
				<Route
					path="/organizations/:organizationId/settings"
					element={<RedirectToOrgApp tab="settings" />}
				/>
				<Route
					path="/organizations/:organizationId/dashboard"
					element={<RedirectToOrgApp tab="dashboard" />}
				/>
				<Route
					path="/organizations/:organizationId/engagements"
					element={<RedirectToOrgApp tab="engagements" />}
				/>
				<Route
					path="/app/:organizationId"
					element={
						<ProtectedRoute>
							<OrgAppLayout />
						</ProtectedRoute>
					}
				>
					<Route index element={<Navigate to="dashboard" replace />} />
					<Route path=":tab" element={<OrganizationOverviewPage />} />
				</Route>
				<Route
					path="/achievements"
					element={<Navigate to="/profile?tab=achievements" replace />}
				/>
				<Route
					path="/users/:userId/achievements"
					element={<UserAchievementsPage />}
				/>
				<Route path="/users/:userId" element={<UserProfilePage />} />
				<Route
					path="/admin/organizations"
					element={
						<ProtectedRoute>
							<AdminOrganizationsPage />
						</ProtectedRoute>
					}
				/>
				<Route
					path="/opportunities"
					element={<Navigate to="/#opportunities" replace />}
				/>
				<Route path="/organizations" element={<Navigate to="/" replace />} />
				<Route path="*" element={<NotFoundPage />} />
			</Route>
		</Routes>
	);
}
