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
import { CloseIcon } from "../components/icons";

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
						<CloseIcon className="h-4 w-4" />
					</button>
				</div>
			))}
		</div>
	);
}
