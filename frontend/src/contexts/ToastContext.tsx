import {
	createContext,
	useCallback,
	useContext,
	useEffect,
	useState,
} from "react";
import { useTranslation } from "react-i18next";
import { type ToastEvent, subscribeToasts } from "../lib/toastBus";
import { runtimeConfig } from "../lib/runtimeConfig";

interface ToastContextValue {
	toasts: ToastEvent[];
	dismiss: (id: string) => void;
}

const ToastContext = createContext<ToastContextValue>({
	toasts: [],
	dismiss: () => {},
});

export function ToastProvider({ children }: { children: React.ReactNode }) {
	const [toasts, setToasts] = useState<ToastEvent[]>([]);

	useEffect(() => {
		const unsub = subscribeToasts((event) => {
			setToasts((prev) => {
				const isDuplicate = prev.some(
					(t) => t.level === event.level && t.message === event.message,
				);
				return isDuplicate ? prev : [...prev, event];
			});
			// 0 (test builds only - see runtimeConfig.ts) disables auto-dismiss
			// entirely, leaving the toast up until manually closed.
			if (runtimeConfig.toastLifetimeMs > 0) {
				setTimeout(() => {
					setToasts((prev) => prev.filter((t) => t.id !== event.id));
				}, runtimeConfig.toastLifetimeMs);
			}
		});
		return unsub;
	}, []);

	const dismiss = useCallback((id: string) => {
		setToasts((prev) => prev.filter((t) => t.id !== id));
	}, []);

	return (
		<ToastContext.Provider value={{ toasts, dismiss }}>
			{children}
			{/* Always-mounted empty live region so a screen reader has an aria-live */}
			{/* region on page load, before the first toast's own role="alert" exists. */}
			{/* No role="status" here - that role is already used app-wide for actual */}
			{/* loading/status indicators, and several tests locate those by a bare */}
			{/* [role='status'] query; a global always-present one would shadow them. */}
			{/* Kept as a sibling (not a wrapper) of the toasts below - nesting a */}
			{/* "polite" region around each toast's own "assertive" alert is unreliable. */}
			<div
				aria-live="polite"
				className="sr-only"
				data-testid="toast-live-region"
			/>
			<ToastList />
		</ToastContext.Provider>
	);
}

function ToastList() {
	const { toasts, dismiss } = useContext(ToastContext);
	const { t } = useTranslation();

	return (
		// role="region": ToastProvider mounts this at the app root (main.tsx),
		// as a sibling of the routed page rather than inside AppLayout's <main>
		// - without an explicit landmark here, a visible toast's text sits
		// outside every landmark on the page, which is exactly what axe's
		// "region" rule (escalated to CI-blocking, see AccessibilityTests.cs)
		// flags. aria-label since a generic "region" needs one to be exposed
		// as a distinct landmark rather than an anonymous one.
		<div
			role="region"
			aria-label={t("error.toastRegionLabel")}
			className="fixed right-4 bottom-4 z-9999 flex flex-col gap-2"
		>
			{toasts.map((toast) => (
				<div
					key={toast.id}
					role="alert"
					className={`flex items-start gap-3 rounded-lg px-4 py-3 text-sm text-white shadow-lg ${
						toast.level === "error"
							? "bg-red-600"
							: toast.level === "warning"
								? "bg-yellow-700"
								: toast.level === "success"
									? "bg-green-700"
									: "bg-gray-700"
					}`}
				>
					<span className="flex-1">{toast.message}</span>
					<button
						type="button"
						aria-label={t("error.dismiss")}
						onClick={() => dismiss(toast.id)}
						className="ml-2 text-white/80 hover:text-white"
					>
						<svg
							aria-hidden="true"
							className="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							stroke="currentColor"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								strokeWidth={2}
								d="M6 18L18 6M6 6l12 12"
							/>
						</svg>
					</button>
				</div>
			))}
		</div>
	);
}
