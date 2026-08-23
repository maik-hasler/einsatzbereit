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

export interface TestAuth {
	isAuthenticated?: boolean;
	roles?: string[];
	sub?: string;
	name?: string;
	email?: string;
	accessToken?: string;

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
	lng?: "de" | "en";

	route?: string;
	auth?: TestAuth;
}

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
