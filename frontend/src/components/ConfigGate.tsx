import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { runtimeConfig } from "../lib/runtimeConfig";
import { getOnlineStatus, subscribeOnlineStatus } from "../lib/onlineStatus";
import { usePageTitle } from "../hooks/usePageTitle";
import { statusTitleClass } from "../lib/headingClasses";
import Button from "./Button";
import RouteState from "./RouteState";

interface Props {
	children: ReactNode;
}

// Refuses to render the app at all when runtimeConfig could not resolve a
// real API/Keycloak origin (#2207) - e.g. an offline PWA cold start that
// never reached /config.js and fell back to the image's now-empty build-time
// defaults. Rendering the app anyway would silently point every API call and
// the Keycloak login at nothing. While unconfigured, coming back online
// re-reads /config.js the only way that reliably takes effect across the
// whole app (oidcConfig in main.tsx is built once, at module scope) - a full
// reload - so a transient offline cold start recovers on its own.
//
// Which of the two screens below that state gets is decided by the browser's
// own connectivity, not by the failure itself: a missing config with no
// connection is the visitor's network, and telling them to contact the
// deployment's operator about it is both wrong and un-actionable, so they get
// the app's own offline state instead (#2317). The operator-facing message is
// kept for the case it actually describes - a reachable network that still
// did not yield a usable config.js.
export default function ConfigGate({ children }: Props) {
	const { t } = useTranslation();
	const [isOnline, setIsOnline] = useState(getOnlineStatus);

	const showConfigError = !runtimeConfig.isConfigured && isOnline;

	usePageTitle(showConfigError ? t("config.unavailableTitle") : null);

	useEffect(() => {
		if (runtimeConfig.isConfigured) return;

		return subscribeOnlineStatus(() => {
			const online = getOnlineStatus();
			setIsOnline(online);
			if (online) window.location.reload();
		});
	}, []);

	if (runtimeConfig.isConfigured) return children;

	if (!isOnline) {
		return (
			<div className="flex min-h-screen flex-col justify-center">
				<RouteState
					variant="offline"
					title={t("routeState.offline.title")}
					message={t("routeState.offline.message")}
					onRetry={() => window.location.reload()}
				/>
			</div>
		);
	}

	return (
		<div className="flex min-h-screen flex-col items-center justify-center gap-6 px-4 text-center">
			<h1 className={`text-brand-700 ${statusTitleClass}`}>
				{t("config.unavailableTitle")}
			</h1>
			<p className="max-w-md text-gray-500">{t("config.unavailableMessage")}</p>
			<Button onClick={() => window.location.reload()}>
				{t("error.reload")}
			</Button>
		</div>
	);
}
