import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { Navigate, useLocation } from "react-router";
import type { ReactNode } from "react";
import { signinLocaleArgs } from "../lib/authLocale";
import Spinner from "../components/Spinner";

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

	if (auth.isLoading) {
		return (
			<div className="flex min-h-screen items-center justify-center">
				<Spinner label={t("auth.loading")} size="lg" />
			</div>
		);
	}

	if (!auth.isAuthenticated) {
		auth.signinRedirect({
			...signinLocaleArgs(),
			state: { returnTo: location.pathname + location.search },
		});
		return null;
	}

	if (requiredRole) {
		const roles = (
			Array.isArray(auth.user?.profile?.roles) ? auth.user?.profile?.roles : []
		) as string[];
		if (!roles.includes(requiredRole)) {
			return <Navigate to="/" replace />;
		}
	}

	return <>{children}</>;
}
