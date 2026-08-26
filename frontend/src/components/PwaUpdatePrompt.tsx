import { useTranslation } from "react-i18next";
// workbox-window is in package.json's dependencies (not just transitively
// pulled in by vite-plugin-pwa) because this virtual module's generated
// runtime code imports it directly - Vite resolves that import against this
// app's own node_modules, not vite-plugin-pwa's.
import { useRegisterSW } from "virtual:pwa-register/react";
import Button from "./Button";

export default function PwaUpdatePrompt() {
	const { t } = useTranslation();
	const { needRefresh, updateServiceWorker } = useRegisterSW();
	const [isUpdateAvailable] = needRefresh;

	if (!isUpdateAvailable) return null;

	return (
		<div
			role="status"
			aria-live="polite"
			className="fixed bottom-4 left-4 z-9999 flex max-w-sm items-center gap-3 rounded-lg bg-gray-700 px-4 py-3 text-sm text-white shadow-lg"
		>
			<span className="flex-1">{t("pwaUpdate.message")}</span>
			{/* aria-label, not just the visible "Reload" text: this banner is an
			unconditional sibling of ConfigGate/ErrorBoundary, so their own
			same-labeled Reload button can be on screen at the same time,
			doing a plain page reload rather than applying this update. */}
			<Button
				size="sm"
				aria-label={t("pwaUpdate.reloadLabel")}
				onClick={() => updateServiceWorker(true)}
			>
				{t("error.reload")}
			</Button>
		</div>
	);
}
