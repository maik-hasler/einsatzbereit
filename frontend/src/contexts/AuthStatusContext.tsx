import { createContext, useContext, useMemo, useState } from "react";
import type { ReactNode } from "react";

interface AuthStatusValue {
	sessionExpired: boolean;
	setSessionExpired: (expired: boolean) => void;
	authRecoveryFailed: boolean;
	setAuthRecoveryFailed: (failed: boolean) => void;
}

const AuthStatusContext = createContext<AuthStatusValue>({
	sessionExpired: false,
	setSessionExpired: () => undefined,
	authRecoveryFailed: false,
	setAuthRecoveryFailed: () => undefined,
});

// Bridges a session-expiry detected inside useSessionExpiryHandler (mounted
// once at the app root) to every Header instance, so the account area can
// show an explicit expired state instead of silently falling back to the
// signed-out UI while the redirect to Keycloak is still pending (#2224).
// authRecoveryFailed is the terminal counterpart: useSessionExpiryHandler
// flips it once the bounded redirect loop gives up, and App.tsx replaces
// the whole route tree with a "sign in isn't working" page while it's set
// (#2208) - unlike sessionExpired, nothing else reads this one.
export function AuthStatusProvider({
	children,
	initialSessionExpired = false,
	initialAuthRecoveryFailed = false,
}: {
	children: ReactNode;
	initialSessionExpired?: boolean;
	initialAuthRecoveryFailed?: boolean;
}) {
	const [sessionExpired, setSessionExpired] = useState(initialSessionExpired);
	const [authRecoveryFailed, setAuthRecoveryFailed] = useState(
		initialAuthRecoveryFailed,
	);

	const value = useMemo(
		() => ({
			sessionExpired,
			setSessionExpired,
			authRecoveryFailed,
			setAuthRecoveryFailed,
		}),
		[sessionExpired, authRecoveryFailed],
	);

	return (
		<AuthStatusContext.Provider value={value}>
			{children}
		</AuthStatusContext.Provider>
	);
}

export function useSessionExpiredFlag(): boolean {
	return useContext(AuthStatusContext).sessionExpired;
}

export function useSetSessionExpiredFlag(): (expired: boolean) => void {
	return useContext(AuthStatusContext).setSessionExpired;
}

export function useAuthRecoveryFailedFlag(): boolean {
	return useContext(AuthStatusContext).authRecoveryFailed;
}

export function useSetAuthRecoveryFailedFlag(): (failed: boolean) => void {
	return useContext(AuthStatusContext).setAuthRecoveryFailed;
}
