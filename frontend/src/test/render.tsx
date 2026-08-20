import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router";
import { AuthContext } from "react-oidc-context";
import { render, type RenderResult } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import type { AuthContextProps } from "react-oidc-context";
import { createTestI18n } from "./i18n";

/**
 * The slice of react-oidc-context a component test needs. Everything a
 * component actually reads off `useAuth()` in this app is here (see
 * frontend/AGENTS.md, "Role Checks"); the rest of AuthContextProps is filled
 * with no-ops so the cast below stays honest about what is stubbed.
 */
export interface TestAuth {
	isAuthenticated?: boolean;
	roles?: string[];
	sub?: string;
	name?: string;
	email?: string;
	accessToken?: string;
}

function buildAuthValue(auth: TestAuth): AuthContextProps {
	const {
		isAuthenticated = false,
		roles = [],
		sub = "test-user",
		name = "Test User",
		email = "test.user@example.test",
		accessToken = "test-token",
	} = auth;

	return {
		isAuthenticated,
		isLoading: false,
		activeNavigator: undefined,
		error: undefined,
		settings: {},
		events: {},
		user: isAuthenticated
			? {
					access_token: accessToken,
					profile: { sub, name, email, roles },
				}
			: undefined,
		removeUser: async () => {},
		signinRedirect: async () => {},
		signinPopup: async () => {},
		signinSilent: async () => null,
		signinResourceOwnerCredentials: async () => {},
		signoutRedirect: async () => {},
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
 * Renders `ui` inside the three providers every non-trivial component in this
 * app assumes: i18n (accessible names are translation strings), a router
 * (Button/EmptyState render <Link> when given `to`), and an auth context
 * (useApiClient reads the access token off it).
 */
export function renderWithProviders(
	ui: ReactElement,
	{ lng = "en", route = "/", auth = {} }: RenderOptions = {},
): RenderResult {
	const i18n = createTestI18n(lng);
	const authValue = buildAuthValue(auth);

	function Wrapper({ children }: { children: ReactNode }) {
		return (
			<AuthContext.Provider value={authValue}>
				<I18nextProvider i18n={i18n}>
					<MemoryRouter initialEntries={[route]}>{children}</MemoryRouter>
				</I18nextProvider>
			</AuthContext.Provider>
		);
	}

	return render(ui, { wrapper: Wrapper });
}
