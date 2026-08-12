import { useEffect, useRef, useState } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useLocation } from "react-router";
import type { ReactNode } from "react";
import { signinLocaleArgs } from "../lib/authLocale";
import Spinner from "../components/Spinner";
import ErrorBanner from "../components/ErrorBanner";
import Button from "../components/Button";
import RouteState from "../components/RouteState";

// Copy for the roles a route actually guards. Only "admin" does today
// (App.tsx's /administration), and it gets wording that names admin rights
// rather than echoing a raw Keycloak role identifier at the user; a role with
// no entry here falls back to the generic 403 wording rather than rendering
// a missing translation key.
const ROLE_COPY: Record<string, { titleKey: string; messageKey: string }> = {
	admin: {
		titleKey: "routeState.adminOnly.title",
		messageKey: "routeState.adminOnly.message",
	},
};

const GENERIC_ROLE_COPY = {
	titleKey: "routeState.forbidden.title",
	messageKey: "error.forbidden",
};

interface Props {
	children: ReactNode;
	// Role a signed-in user must have to render `children` (see the flat
	// roles array shape documented in frontend/AGENTS.md's Role Checks
	// section). Omit for routes that only require being signed in.
	requiredRole?: string;
}

export default function ProtectedRoute({ children, requiredRole }: Props) {
	const auth = useAuth();
	const { t } = useTranslation();
	const location = useLocation();
	// Rendering must stay side-effect free - calling signinRedirect() directly
	// in the render body (as this used to) fires a fresh redirect on every
	// re-render before the browser actually navigates away, and leaves its
	// promise neither awaited nor caught (#1235). `attemptedRef` guards against
	// re-firing while one is already in flight (including React's dev-only
	// double effect invocation); `retryToken` is the only way to intentionally
	// retry after a failure.
	const attemptedRef = useRef(false);
	const [retryToken, setRetryToken] = useState(0);
	const [redirectError, setRedirectError] = useState<string | null>(null);

	useEffect(() => {
		if (auth.isLoading || auth.isAuthenticated || attemptedRef.current) return;
		attemptedRef.current = true;
		setRedirectError(null);
		auth
			.signinRedirect({
				...signinLocaleArgs(),
				state: { returnTo: location.pathname + location.search },
			})
			.catch((err: unknown) => {
				attemptedRef.current = false;
				console.error("[ProtectedRoute] signinRedirect failed:", err);
				setRedirectError(t("error.serverError"));
			});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [auth.isLoading, auth.isAuthenticated, retryToken]);

	if (redirectError) {
		return (
			<div className="flex min-h-screen flex-col items-center justify-center gap-4 px-4 text-center">
				<ErrorBanner message={redirectError} className="max-w-md" />
				<Button onClick={() => setRetryToken((n) => n + 1)}>
					{t("common.retry")}
				</Button>
			</div>
		);
	}

	if (auth.isLoading || !auth.isAuthenticated) {
		return (
			<div className="flex min-h-screen items-center justify-center">
				<Spinner label={t("auth.loading")} size="lg" />
			</div>
		);
	}

	if (requiredRole) {
		const roles = (
			Array.isArray(auth.user?.profile?.roles) ? auth.user?.profile?.roles : []
		) as string[];
		if (!roles.includes(requiredRole)) {
			// #1774: this used to be <Navigate to="/" replace />, which dumped a
			// visitor following a bookmarked or shared /administration link onto
			// the landing page with no explanation - nothing distinguished "you
			// may not go there" from "that link is dead" or "you got signed out".
			// Staying on the requested URL and saying why is both honest and
			// keeps the address bar meaningful, so the link can be handed to
			// someone whose account can open it.
			const copy = ROLE_COPY[requiredRole] ?? GENERIC_ROLE_COPY;
			return (
				<RouteState
					variant="forbidden"
					title={t(copy.titleKey)}
					message={t(copy.messageKey)}
					action={{ to: "/", label: t("notFound.backHome") }}
				/>
			);
		}
	}

	return <>{children}</>;
}
