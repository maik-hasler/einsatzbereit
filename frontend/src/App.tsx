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
import { signinLocaleArgs } from "./lib/authLocale";
import ErrorBanner from "./components/ErrorBanner";
import Button from "./components/Button";
import AppLayout from "./layouts/AppLayout";
import ProtectedRoute from "./layouts/ProtectedRoute";
import OrgAppLayout, { type OrgAppContext } from "./layouts/OrgAppLayout";
import HomePage from "./pages/HomePage";
// Eager, not lazy: OrgAppLayout (eager, above) and EngagementManagementPage
// both statically import this too, so it always lands in the entry chunk
// regardless - a lazy() wrapper here just contradicted that and tripped
// Vite's INEFFECTIVE_DYNAMIC_IMPORT warning on every build.
import NotFoundPage from "./pages/NotFoundPage";

// Route pages are lazy-loaded so each one becomes its own build chunk instead
// of all being bundled (and precached by the PWA service worker) as a single
// monolithic entry chunk - see vite.config.ts's manualChunks/workbox comments
// for the other half of this. AppLayout/OrgAppLayout stay eager since they
// render on (almost) every route as the app chrome. HomePage ("/") also
// stays eager (imported above, not lazy): it's the app's default landing
// route anyway (small - not the precache-size problem #1403 targets), and
// it shares an in-flight-request dedup with Header for GET /v1/organizations
// (useSharedOrgFetch, #1396) that depends on both mounting in the same
// synchronous commit - a lazy HomePage's chunk-load delay pushes its mount
// past Header's request already having settled, so the "shared" fetch
// fires twice instead of once.
const PrivacyPolicyPage = lazy(() => import("./pages/PrivacyPolicyPage"));
const ImprintPage = lazy(() => import("./pages/ImprintPage"));
const TermsOfUsePage = lazy(() => import("./pages/TermsOfUsePage"));
const ContactPage = lazy(() => import("./pages/ContactPage"));
const HelpPage = lazy(() => import("./pages/HelpPage"));
const UnsubscribePage = lazy(() => import("./pages/UnsubscribePage"));
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
const UserProfilePage = lazy(() => import("./pages/UserProfilePage"));
const OrgDashboardPage = lazy(() => import("./pages/app/OrgDashboardPage"));
const OrgOpportunitiesPage = lazy(
	() => import("./pages/app/OrgOpportunitiesPage"),
);
const OrgEngagementsPage = lazy(() => import("./pages/app/OrgEngagementsPage"));
const OrgMembersPage = lazy(() => import("./pages/app/OrgMembersPage"));
const OrgSettingsPage = lazy(() => import("./pages/app/OrgSettingsPage"));

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
	// /callback is a bare top-level route (see below) with no AppLayout
	// wrapper, so unlike every other page it has to supply its own <main>/
	// <h1> rather than relying on a shared layout for them - previously had
	// neither.
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
					<Route path="engagements" element={<OrgEngagementsPage />} />
					<Route path="members" element={<OrgMembersPage />} />
					<Route path="settings" element={<OrgSettingsPage />} />
				</Route>
			</Route>
			<Route element={<AppLayout />}>
				<Route path="/" element={<HomePage />} />
				<Route path="/privacy-policy" element={<PrivacyPolicyPage />} />
				<Route path="/imprint" element={<ImprintPage />} />
				<Route path="/terms-of-use" element={<TermsOfUsePage />} />
				<Route path="/contact" element={<ContactPage />} />
				<Route path="/help" element={<HelpPage />} />
				<Route path="/unsubscribed" element={<UnsubscribePage />} />
				<Route
					path="/volunteer-opportunities/:opportunityId"
					element={<VolunteerOpportunityDetailPage />}
				/>
				<Route
					path="/organizations/:organizationId"
					element={<OrganizationProfilePage />}
				/>
				{/* #1684: previously just redirected into /profile?tab=engagements -
				now the real destination for engagement notifications and the
				header's notification-bell fallback, which already pointed here. */}
				<Route
					path="/my-engagements"
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
				/>
				<Route path="/organizations" element={<OrganizationsPage />} />
				<Route path="*" element={<NotFoundPage />} />
			</Route>
		</Routes>
	);
}
