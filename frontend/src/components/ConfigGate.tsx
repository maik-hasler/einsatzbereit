import type { ReactNode } from "react";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { runtimeConfig } from "../lib/runtimeConfig";
import { getOnlineStatus, subscribeOnlineStatus } from "../lib/onlineStatus";
import { usePageTitle } from "../hooks/usePageTitle";
import { statusTitleClass } from "../lib/headingClasses";
import Button from "./Button";

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
export default function ConfigGate({ children }: Props) {
	const { t } = useTranslation();

	usePageTitle(
		runtimeConfig.isConfigured ? null : t("config.unavailableTitle"),
	);

	useEffect(() => {
		if (runtimeConfig.isConfigured) return;

		return subscribeOnlineStatus(() => {
			if (getOnlineStatus()) window.location.reload();
		});
	}, []);

	if (runtimeConfig.isConfigured) return children;

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
