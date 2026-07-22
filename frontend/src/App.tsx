import { Routes, Route, Navigate, Outlet, useParams } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import AppLayout from "./layouts/AppLayout";
import ProtectedRoute from "./layouts/ProtectedRoute";
import OrgAppLayout from "./layouts/OrgAppLayout";
import HomePage from "./pages/HomePage";
import PrivacyPolicyPage from "./pages/PrivacyPolicyPage";
import ImprintPage from "./pages/ImprintPage";
import VolunteerOpportunityDetailPage from "./pages/VolunteerOpportunityDetailPage";
import EngagementManagementPage from "./pages/EngagementManagementPage";
import ProfileOverviewPage from "./pages/ProfileOverviewPage";
import NotFoundPage from "./pages/NotFoundPage";
import OrganizationProfilePage from "./pages/OrganizationProfilePage";
import UserAchievementsPage from "./pages/UserAchievementsPage";
import AdministrationPage from "./pages/AdministrationPage";
import UserProfilePage from "./pages/UserProfilePage";
import OrgDashboardPage from "./pages/app/OrgDashboardPage";
import OrgOpportunitiesPage from "./pages/app/OrgOpportunitiesPage";
import OrgMembersPage from "./pages/app/OrgMembersPage";
import OrgSettingsPage from "./pages/app/OrgSettingsPage";

function OrgAppRedirect({ tab }: { tab: string }) {
	const { organizationId } = useParams<{ organizationId: string }>();
	return <Navigate to={`/app/${organizationId}/${tab}`} replace />;
}

// Pre-#9 bookmarks to a specific opportunity's engagement management page
// need their :opportunityId preserved, unlike OrgAppRedirect's fixed tab path.
function OrgAppEngagementsRedirect() {
	const { organizationId, opportunityId } = useParams<{
		organizationId: string;
		opportunityId: string;
	}>();
	return (
		<Navigate
			to={`/app/${organizationId}/dashboard/opportunities/${opportunityId}/engagements`}
			replace
		/>
	);
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
				Route's own element is just an Outlet, no extra chrome), so
				OrgAppLayout's single Outlet keeps rendering whichever page
				unchanged. */}
				<Route path="dashboard" element={<Outlet />}>
					<Route index element={<OrgDashboardPage />} />
					<Route path="opportunities" element={<OrgOpportunitiesPage />} />
					<Route
						path="opportunities/:opportunityId/engagements"
						element={<EngagementManagementPage />}
					/>
					<Route path="members" element={<OrgMembersPage />} />
					<Route path="settings" element={<OrgSettingsPage />} />
				</Route>
				{/* Old tab key: the "Engagements" tab is now the Opportunities hub - keep old bookmarks working. */}
				<Route
					path="engagements"
					element={<OrgAppRedirect tab="dashboard/opportunities" />}
				/>
				{/* Pre-#9 bookmarks: these three lived flat under /app/:organizationId/... before opportunities/members/settings were nested under /dashboard. */}
				<Route
					path="opportunities"
					element={<OrgAppRedirect tab="dashboard/opportunities" />}
				/>
				<Route
					path="opportunities/:opportunityId/engagements"
					element={<OrgAppEngagementsRedirect />}
				/>
				<Route
					path="members"
					element={<OrgAppRedirect tab="dashboard/members" />}
				/>
				<Route
					path="settings"
					element={<OrgAppRedirect tab="dashboard/settings" />}
				/>
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
				<Route path="/account" element={<Navigate to="/profile" replace />} />
				<Route
					path="/profile"
					element={
						<ProtectedRoute>
							<ProfileOverviewPage />
						</ProtectedRoute>
					}
				/>
				{/* Pre-restructure bookmarks: org dashboard/settings/engagements used to live here, nested in the main site shell - now their own app context. */}
				<Route
					path="/organizations/:organizationId/dashboard"
					element={<OrgAppRedirect tab="dashboard" />}
				/>
				<Route
					path="/organizations/:organizationId/settings"
					element={<OrgAppRedirect tab="dashboard/settings" />}
				/>
				<Route
					path="/organizations/:organizationId/engagements"
					element={<OrgAppRedirect tab="dashboard/opportunities" />}
				/>
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
					path="/administration"
					element={
						<ProtectedRoute>
							<AdministrationPage />
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
