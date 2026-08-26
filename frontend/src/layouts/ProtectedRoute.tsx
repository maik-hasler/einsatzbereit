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

const ROLE_COPY = {
	admin: {
		titleKey: "routeState.adminOnly.title",
		messageKey: "routeState.adminOnly.message",
	},
} as const;

interface Props {
	children: ReactNode;

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

	// isAuthenticated alone gates the spinner, deliberately ignoring isLoading
	// once true - isLoading also flips true while an already-authenticated
	// user has a background token refresh in flight (e.g. signinSilent()
	// after a role grant, #2206), and treating that the same as "not yet
	// signed in" would unmount the whole page under an in-progress
	// interaction. Same class of bug #2263 fixed in useAuthDisplayStatus.
	if (!auth.isAuthenticated) {
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
