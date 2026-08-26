import { lazy } from "react";
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
import { useSilentSsoProbe } from "./hooks/useSilentSsoProbe";
import {
	AuthStatusProvider,
	useAuthRecoveryFailedFlag,
} from "./contexts/AuthStatusContext";
import { signinLocaleArgs } from "./lib/authLocale";
import { clearAuthRecoveryAttempts } from "./lib/authRecovery";
import ErrorBanner from "./components/ErrorBanner";
import Button from "./components/Button";
import AppLayout from "./layouts/AppLayout";
import ProtectedRoute from "./layouts/ProtectedRoute";
import OrgAppLayout, { type OrgAppContext } from "./layouts/OrgAppLayout";
import HomePage from "./pages/HomePage";

import NotFoundPage from "./pages/NotFoundPage";

const OpportunitiesPage = lazy(() => import("./pages/OpportunitiesPage"));
const PrivacyPolicyPage = lazy(() => import("./pages/PrivacyPolicyPage"));
const ImprintPage = lazy(() => import("./pages/ImprintPage"));
const TermsOfUsePage = lazy(() => import("./pages/TermsOfUsePage"));
const ContactPage = lazy(() => import("./pages/ContactPage"));
const HelpPage = lazy(() => import("./pages/HelpPage"));
const UnsubscribePage = lazy(() => import("./pages/UnsubscribePage"));
const UnsubscribeConfirmPage = lazy(
	() => import("./pages/UnsubscribeConfirmPage"),
);
const VolunteerOpportunityDetailPage = lazy(
	() => import("./pages/VolunteerOpportunityDetailPage"),
);
const EngagementManagementPage = lazy(
	() => import("./pages/EngagementManagementPage"),
);
const ProfileOverviewPage = lazy(() => import("./pages/ProfileOverviewPage"));
const ProfileSettingsPage = lazy(() => import("./pages/ProfileSettingsPage"));
const MyEngagementsPage = lazy(() => import("./pages/MyEngagementsPage"));
const OrganizationProfilePage = lazy(
	() => import("./pages/OrganizationProfilePage"),
);
const OrganizationsPage = lazy(() => import("./pages/OrganizationsPage"));
const AdministrationPage = lazy(() => import("./pages/AdministrationPage"));
const AdminOrganizationsPage = lazy(async () => ({
	default: (await import("./pages/AdministrationPage")).AdminOrganizationsPage,
}));
const AdminUsersPage = lazy(async () => ({
	default: (await import("./pages/AdministrationPage")).AdminUsersPage,
}));
const AdminReportsPage = lazy(async () => ({
	default: (await import("./pages/AdministrationPage")).AdminReportsPage,
}));
const AdminAuditLogPage = lazy(async () => ({
	default: (await import("./pages/AdministrationPage")).AdminAuditLogPage,
}));
const UserProfilePage = lazy(() => import("./pages/UserProfilePage"));
const OrgDashboardPage = lazy(() => import("./pages/app/OrgDashboardPage"));
const OrgOpportunitiesPage = lazy(
	() => import("./pages/app/OrgOpportunitiesPage"),
);
const OrgEngagementsPage = lazy(() => import("./pages/app/OrgEngagementsPage"));
const OrgMembersPage = lazy(() => import("./pages/app/OrgMembersPage"));
const OrgSettingsPage = lazy(() => import("./pages/app/OrgSettingsPage"));

function OrgAppOutletRelay() {
	const context = useOutletContext<OrgAppContext>();
	return <Outlet context={context} />;
}

function CallbackPage() {
	const auth = useAuth();
	const { t } = useTranslation();

	if (auth.error) {
		return (
			<main className="flex min-h-screen flex-col items-center justify-center gap-4 px-4 text-center">
				<h1 className="text-xl font-semibold text-gray-900">
					{t("error.boundaryTitle")}
				</h1>
				<ErrorBanner
					message={t("auth.authError", { message: auth.error.message })}
				/>
				<div className="flex gap-3">
					<Button
						variant="secondary"
						onClick={() => void auth.signinRedirect(signinLocaleArgs())}
					>
						{t("orgApp.retry")}
					</Button>
					<Button to="/">{t("orgApp.backToSite")}</Button>
				</div>
			</main>
		);
	}
	return (
		<main className="flex min-h-screen items-center justify-center">
			<h1 className="text-gray-500">{t("auth.completing")}</h1>
		</main>
	);
}

// The bounded-redirect-loop terminal state (#2208): useSessionExpiryHandler
// gives up after a second consecutive session expiry with no successful API
// call in between (e.g. a backend ValidIssuers mismatch that no amount of
// re-authenticating can fix), rather than keep bouncing the visitor through
// Keycloak. Rendered in place of the whole route tree - a page-scoped state
// wouldn't be reachable if the failure happens before that page's own data
// ever loads, and the user needs a guaranteed way out regardless of route.
function AuthRecoveryFailedPage() {
	const auth = useAuth();
	const { t } = useTranslation();

	function handleSignOut() {
		clearAuthRecoveryAttempts();
		auth.signoutRedirect();
	}

	return (
		<main className="flex min-h-screen flex-col items-center justify-center gap-4 px-4 text-center">
			<h1 className="text-xl font-semibold text-gray-900">
				{t("auth.recoveryFailedTitle")}
			</h1>
			<ErrorBanner message={t("auth.recoveryFailedMessage")} />
			<Button onClick={handleSignOut}>{t("nav.signOut")}</Button>
		</main>
	);
}

// Exported for App.test.tsx: rendering this directly (instead of <App/>)
// avoids App's own <AuthStatusProvider> below shadowing the one
// renderWithProviders sets up for the test.
export function AppRoutes() {
	useSessionExpiryHandler();
	useSilentSsoProbe();
	const authRecoveryFailed = useAuthRecoveryFailedFlag();

	if (authRecoveryFailed) return <AuthRecoveryFailedPage />;

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

				<Route path="dashboard" element={<OrgAppOutletRelay />}>
					<Route index element={<OrgDashboardPage />} />
					<Route path="opportunities" element={<OrgOpportunitiesPage />} />
					<Route
						path="opportunities/:opportunityId/engagements"
						element={<EngagementManagementPage />}
					/>
					<Route path="engagements" element={<OrgEngagementsPage />} />
					<Route path="members" element={<OrgMembersPage />} />
					<Route path="settings" element={<OrgSettingsPage />} />
				</Route>
			</Route>
			<Route element={<AppLayout />}>
				<Route path="/" element={<HomePage />} />

				<Route path="/opportunities" element={<OpportunitiesPage />} />
				<Route path="/privacy-policy" element={<PrivacyPolicyPage />} />
				<Route path="/imprint" element={<ImprintPage />} />
				<Route path="/terms-of-use" element={<TermsOfUsePage />} />
				<Route path="/contact" element={<ContactPage />} />
				<Route path="/help" element={<HelpPage />} />
				<Route path="/unsubscribe" element={<UnsubscribeConfirmPage />} />
				<Route path="/unsubscribed" element={<UnsubscribePage />} />
				<Route
					path="/volunteer-opportunities/:opportunityId"
					element={<VolunteerOpportunityDetailPage />}
				/>
				<Route path="/organizations" element={<OrganizationsPage />} />
				<Route
					path="/organizations/:organizationId"
					element={<OrganizationProfilePage />}
				/>

				<Route
					path="/my-signups"
					element={
						<ProtectedRoute>
							<MyEngagementsPage />
						</ProtectedRoute>
					}
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
					path="/profile/settings"
					element={
						<ProtectedRoute>
							<ProfileSettingsPage />
						</ProtectedRoute>
					}
				/>
				<Route path="/users/:userId" element={<UserProfilePage />} />

				<Route
					path="/administration"
					element={
						<ProtectedRoute requiredRole="admin">
							<AdministrationPage />
						</ProtectedRoute>
					}
				>
					<Route index element={<Navigate to="organizations" replace />} />
					<Route path="organizations" element={<AdminOrganizationsPage />} />
					<Route path="users" element={<AdminUsersPage />} />
					<Route path="reports" element={<AdminReportsPage />} />
					<Route path="audit-log" element={<AdminAuditLogPage />} />
				</Route>
				<Route path="*" element={<NotFoundPage />} />
			</Route>
		</Routes>
	);
}

export default function App() {
	return (
		<AuthStatusProvider>
			<AppRoutes />
		</AuthStatusProvider>
	);
}
