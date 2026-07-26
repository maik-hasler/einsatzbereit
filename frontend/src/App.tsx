import {
	Routes,
	Route,
	Navigate,
	Outlet,
	useOutletContext,
} from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useSessionExpiryHandler } from "./hooks/useSessionExpiryHandler";
import ErrorBanner from "./components/ErrorBanner";
import AppLayout from "./layouts/AppLayout";
import ProtectedRoute from "./layouts/ProtectedRoute";
import OrgAppLayout, { type OrgAppContext } from "./layouts/OrgAppLayout";
import HomePage from "./pages/HomePage";
import PrivacyPolicyPage from "./pages/PrivacyPolicyPage";
import ImprintPage from "./pages/ImprintPage";
import VolunteerOpportunityDetailPage from "./pages/VolunteerOpportunityDetailPage";
import EngagementManagementPage from "./pages/EngagementManagementPage";
import ProfileOverviewPage from "./pages/ProfileOverviewPage";
import NotFoundPage from "./pages/NotFoundPage";
import OrganizationProfilePage from "./pages/OrganizationProfilePage";
import OrganizationsPage from "./pages/OrganizationsPage";
import UserAchievementsPage from "./pages/UserAchievementsPage";
import AdministrationPage from "./pages/AdministrationPage";
import UserProfilePage from "./pages/UserProfilePage";
import OrgDashboardPage from "./pages/app/OrgDashboardPage";
import OrgOpportunitiesPage from "./pages/app/OrgOpportunitiesPage";
import OrgMembersPage from "./pages/app/OrgMembersPage";
import OrgSettingsPage from "./pages/app/OrgSettingsPage";

// A bare <Outlet /> element (no `context` prop) starts a brand new outlet
// context of its own - it does NOT transparently forward whatever an
// ancestor <Outlet context={...}> (OrgAppLayout's) already provided. Without
// this relay, every route nested under the pathless "dashboard" parent below
// would have useOutletContext<OrgAppContext>() resolve to undefined instead
// of OrgAppLayout's {org, reloadOrg}, crashing on the very first destructure.
function OrgAppOutletRelay() {
	const context = useOutletContext<OrgAppContext>();
	return <Outlet context={context} />;
}

function CallbackPage() {
	const auth = useAuth();
	const { t } = useTranslation();
	if (auth.error) {
		return (
			<div className="flex min-h-screen items-center justify-center">
				<ErrorBanner
					message={t("auth.authError", { message: auth.error.message })}
				/>
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
	useSessionExpiryHandler();
	return (
		<Routes>
			<Route path="/callback" element={<CallbackPage />} />
			<Route
				path="/app/:organizationId"
				element={
					<ProtectedRoute>
						<OrgAppLayout />
					</ProtectedRoute>
				}
			>
				<Route index element={<Navigate to="dashboard" replace />} />
				{/* Pathless parent (#9): opportunities/members/settings are now
				nested under /dashboard/... in the URL - see OrgAppLayout's
				orgTabPath - while staying siblings in the render tree (this
				Route's own element renders no extra chrome, just relays
				OrgAppLayout's outlet context - see OrgAppOutletRelay), so
				OrgAppLayout's single Outlet keeps rendering whichever page
				unchanged. */}
				<Route path="dashboard" element={<OrgAppOutletRelay />}>
					<Route index element={<OrgDashboardPage />} />
					<Route path="opportunities" element={<OrgOpportunitiesPage />} />
					<Route
						path="opportunities/:opportunityId/engagements"
						element={<EngagementManagementPage />}
					/>
					<Route path="members" element={<OrgMembersPage />} />
					<Route path="settings" element={<OrgSettingsPage />} />
				</Route>
			</Route>
			<Route element={<AppLayout />}>
				<Route path="/" element={<HomePage />} />
				<Route path="/privacy-policy" element={<PrivacyPolicyPage />} />
				<Route path="/imprint" element={<ImprintPage />} />
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
					path="/profile"
					element={
						<ProtectedRoute>
							<ProfileOverviewPage />
						</ProtectedRoute>
					}
				/>
				<Route
					path="/users/:userId/achievements"
					element={<UserAchievementsPage />}
				/>
				<Route path="/users/:userId" element={<UserProfilePage />} />
				<Route
					path="/administration"
					element={
						<ProtectedRoute>
							<AdministrationPage />
						</ProtectedRoute>
					}
				/>
				<Route path="/organizations" element={<OrganizationsPage />} />
				<Route path="*" element={<NotFoundPage />} />
			</Route>
		</Routes>
	);
}
