import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router";
import { AuthContext } from "react-oidc-context";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, type RenderResult } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import type { AuthContextProps } from "react-oidc-context";
import { ToastProvider } from "../contexts/ToastContext";
import { QuickActionsProvider } from "../contexts/QuickActionsContext";
import { HeaderOverlayProvider } from "../contexts/HeaderOverlayContext";
import { OrgBreadcrumbProvider } from "../contexts/OrgBreadcrumbContext";
import { AuthStatusProvider } from "../contexts/AuthStatusContext";
import { createTestI18n } from "./i18n";

export interface TestAuth {
	isAuthenticated?: boolean;
	isLoading?: boolean;
	roles?: string[];
	sub?: string;
	name?: string;
	email?: string;
	accessToken?: string;
	error?: Error;

	signinRedirect?: () => Promise<void>;
	signinSilent?: () => Promise<unknown>;
	signoutRedirect?: () => Promise<void>;
	removeUser?: () => Promise<void>;
}

function buildAuthValue(auth: TestAuth): AuthContextProps {
	const {
		isAuthenticated = false,
		isLoading = false,
		roles = [],
		sub = "test-user",
		name = "Test User",
		email = "test.user@example.test",
		accessToken = "test-token",
		error = undefined,
		signinRedirect = async () => {},
		signinSilent = async () => null,
		signoutRedirect = async () => {},
		removeUser = async () => {},
	} = auth;

	return {
		isAuthenticated,
		isLoading,
		activeNavigator: undefined,
		error,
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
		signinSilent,
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

/**
 * A cache with every default that helps a real user turned off.
 *
 * Retries turn one rejected mock into three and a failing assertion into a
 * timeout that says nothing. A shared cache leaks one test's data into the
 * next. A refetch on focus fires when jsdom's window takes focus, which is
 * never a thing the test asked for.
 *
 * A fresh instance per render keeps tests independent without any cleanup.
 */
function createTestQueryClient(): QueryClient {
	return new QueryClient({
		defaultOptions: {
			queries: {
				retry: false,
				gcTime: Infinity,
				staleTime: 0,
				refetchOnWindowFocus: false,
				refetchOnReconnect: false,
				networkMode: "always",
			},
			mutations: { retry: false, networkMode: "always" },
		},
	});
}

export interface RenderOptions {
	lng?: "de" | "en";

	route?: string;
	auth?: TestAuth;
	sessionExpired?: boolean;
	authRecoveryFailed?: boolean;
}

export function renderWithProviders(
	ui: ReactElement,
	{
		lng = "en",
		route = "/",
		auth = {},
		sessionExpired = false,
		authRecoveryFailed = false,
	}: RenderOptions = {},
): RenderResult {
	const i18n = createTestI18n(lng);
	const authValue = buildAuthValue(auth);
	const queryClient = createTestQueryClient();

	function Wrapper({ children }: { children: ReactNode }) {
		return (
			<ToastProvider>
				<AuthContext.Provider value={authValue}>
					<QueryClientProvider client={queryClient}>
						<I18nextProvider i18n={i18n}>
							<MemoryRouter initialEntries={[route]}>
								<AuthStatusProvider
									initialSessionExpired={sessionExpired}
									initialAuthRecoveryFailed={authRecoveryFailed}
								>
									<QuickActionsProvider>
										<HeaderOverlayProvider>
											<OrgBreadcrumbProvider>{children}</OrgBreadcrumbProvider>
										</HeaderOverlayProvider>
									</QuickActionsProvider>
								</AuthStatusProvider>
							</MemoryRouter>
						</I18nextProvider>
					</QueryClientProvider>
				</AuthContext.Provider>
			</ToastProvider>
		);
	}

	return render(ui, { wrapper: Wrapper });
}
