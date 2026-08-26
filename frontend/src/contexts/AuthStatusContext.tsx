import { createContext, useContext, useMemo, useState } from "react";
import type { ReactNode } from "react";

interface AuthStatusValue {
	sessionExpired: boolean;
	setSessionExpired: (expired: boolean) => void;
}

const AuthStatusContext = createContext<AuthStatusValue>({
	sessionExpired: false,
	setSessionExpired: () => undefined,
});

// Bridges a session-expiry detected inside useSessionExpiryHandler (mounted
// once at the app root) to every Header instance, so the account area can
// show an explicit expired state instead of silently falling back to the
// signed-out UI while the redirect to Keycloak is still pending (#2224).
export function AuthStatusProvider({
	children,
	initialSessionExpired = false,
}: {
	children: ReactNode;
	initialSessionExpired?: boolean;
}) {
	const [sessionExpired, setSessionExpired] = useState(initialSessionExpired);

	const value = useMemo(
		() => ({ sessionExpired, setSessionExpired }),
		[sessionExpired],
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
