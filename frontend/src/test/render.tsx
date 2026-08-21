import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router";
import { AuthContext } from "react-oidc-context";
import { render, type RenderResult } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import type { AuthContextProps } from "react-oidc-context";
import { ToastProvider } from "../contexts/ToastContext";
import { QuickActionsProvider } from "../contexts/QuickActionsContext";
import { HeaderOverlayProvider } from "../contexts/HeaderOverlayContext";
import { OrgBreadcrumbProvider } from "../contexts/OrgBreadcrumbContext";
import { createTestI18n } from "./i18n";

/**
 * The slice of react-oidc-context a component test needs - everything this app
 * actually reads off `useAuth()` (see frontend/AGENTS.md, "Role Checks").
 *
 * The rest of `AuthContextProps` is filled with no-ops rather than left off:
 * `signoutRedirect`/`signinRedirect` and friends are called straight from
 * click handlers (Header, MobileMenu), and an undefined one turns a rendered
 * sign-out button into a crash the moment a test clicks it.
 */
export interface TestAuth {
	isAuthenticated?: boolean;
	roles?: string[];
	sub?: string;
	name?: string;
	email?: string;
	accessToken?: string;
	/** Override one of the no-op auth actions, to assert it was called. */
	signinRedirect?: () => Promise<void>;
	signoutRedirect?: () => Promise<void>;
	removeUser?: () => Promise<void>;
}

function buildAuthValue(auth: TestAuth): AuthContextProps {
	const {
		isAuthenticated = false,
		roles = [],
		sub = "test-user",
		name = "Test User",
		email = "test.user@example.test",
		accessToken = "test-token",
		signinRedirect = async () => {},
		signoutRedirect = async () => {},
		removeUser = async () => {},
	} = auth;

	return {
		isAuthenticated,
		isLoading: false,
		activeNavigator: undefined,
		error: undefined,
		settings: {},
		// oidc-client-ts's event registry: each add* returns its own
		// unsubscribe. useSessionExpiryHandler subscribes to
		// addSilentRenewError on mount and calls the returned function on
		// cleanup, so both halves have to exist.
		events: {
			addUserLoaded: () => () => {},
			addUserUnloaded: () => () => {},
			addSilentRenewError: () => () => {},
			addUserSignedIn: () => () => {},
			addUserSignedOut: () => () => {},
			addUserSessionChanged: () => () => {},
			addAccessTokenExpiring: () => () => {},
			addAccessTokenExpired: () => () => {},
		},
		user: isAuthenticated
			? {
					access_token: accessToken,
					profile: { sub, name, email, roles },
				}
			: undefined,
		removeUser,
		signinRedirect,
		signinPopup: async () => {},
		signinSilent: async () => null,
		signinResourceOwnerCredentials: async () => {},
		signoutRedirect,
		signoutPopup: async () => {},
		signoutSilent: async () => {},
		querySessionStatus: async () => null,
		revokeTokens: async () => {},
		startSilentRenew: () => {},
		stopSilentRenew: () => {},
		clearStaleState: async () => {},
	} as unknown as AuthContextProps;
}

export interface RenderOptions {
	/**
	 * Defaults to English: the Playwright suite these tests take over from runs
	 * against a CI browser that resolves to "en", and this repo's source
	 * strings are English (root AGENTS.md).
	 */
	lng?: "de" | "en";
	/** Initial history entry, for components containing <Link>/useLocation. */
	route?: string;
	auth?: TestAuth;
}

/**
 * Renders `ui` inside the same provider stack the running app puts every page
 * and component inside (main.tsx plus AppLayout/OrgAppLayout): i18n, because
 * accessible names are translation strings; a router, because Button and
 * EmptyState render a <Link> when given `to`; auth, because useApiClient
 * reads the access token off it; and the four app contexts, because a page
 * that calls useQuickActions or useOrgBreadcrumb throws outright without
 * them. Rendering inside the real stack rather than a narrower one is what
 * lets a page be tested here at all.
 */
export function renderWithProviders(
	ui: ReactElement,
	{ lng = "en", route = "/", auth = {} }: RenderOptions = {},
): RenderResult {
	const i18n = createTestI18n(lng);
	const authValue = buildAuthValue(auth);

	function Wrapper({ children }: { children: ReactNode }) {
		return (
			<ToastProvider>
				<AuthContext.Provider value={authValue}>
					<I18nextProvider i18n={i18n}>
						<MemoryRouter initialEntries={[route]}>
							<QuickActionsProvider>
								<HeaderOverlayProvider>
									<OrgBreadcrumbProvider>{children}</OrgBreadcrumbProvider>
								</HeaderOverlayProvider>
							</QuickActionsProvider>
						</MemoryRouter>
					</I18nextProvider>
				</AuthContext.Provider>
			</ToastProvider>
		);
	}

	return render(ui, { wrapper: Wrapper });
}
