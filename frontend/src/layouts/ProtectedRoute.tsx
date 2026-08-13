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

// Copy for each guardable role, keyed by the Keycloak role name. Only "admin"
// is guarded today (App.tsx's /administration), and it gets wording that names
// admin rights rather than echoing a raw role identifier at the user. This map
// - not the set of known roles - is what `requiredRole` accepts below, so
// guarding a second route on a new role is a compile error until that role has
// something to say for itself.
const ROLE_COPY = {
	admin: {
		titleKey: "routeState.adminOnly.title",
		messageKey: "routeState.adminOnly.message",
	},
} as const;

interface Props {
	children: ReactNode;
	// Role a signed-in user must have to render `children` (see the flat
	// roles array shape documented in frontend/AGENTS.md's Role Checks
	// section). Omit for routes that only require being signed in.
	//
	// The 403 state below renders no landmark of its own, which is correct
	// only because every route using this prop today sits under AppLayout and
	// inherits its <main>. This same component also wraps OrgAppLayout (see
	// App.tsx), which bypasses AppLayout entirely - guarding *that* route on a
	// role would put an <h1> outside any landmark and fail the
	// landmark-one-main / page-has-heading-one rules AccessibilityTests
	// escalates to CI-blocking. Wrap the state in a <main> the way
	// OrgAppLayout's own stateScreen does before doing that.
	requiredRole?: keyof typeof ROLE_COPY;
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
			const copy = ROLE_COPY[requiredRole];
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
