import {
	createContext,
	useCallback,
	useContext,
	useEffect,
	useState,
} from "react";
import { useTranslation } from "react-i18next";
import { type ToastEvent, subscribeToasts } from "../lib/toastBus";

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
			setToasts((prev) => [...prev, event]);
			setTimeout(() => {
				setToasts((prev) => prev.filter((t) => t.id !== event.id));
			}, 5000);
		});
		return unsub;
	}, []);

	const dismiss = useCallback((id: string) => {
		setToasts((prev) => prev.filter((t) => t.id !== id));
	}, []);

	return (
		<ToastContext.Provider value={{ toasts, dismiss }}>
			{children}
			<ToastList />
		</ToastContext.Provider>
	);
}

function ToastList() {
	const { toasts, dismiss } = useContext(ToastContext);
	const { t } = useTranslation();

	if (toasts.length === 0) return null;

	return (
		<div className="fixed bottom-4 right-4 z-[9999] flex flex-col gap-2">
			{toasts.map((toast) => (
				<div
					key={toast.id}
					role="alert"
					className={`flex items-start gap-3 rounded-lg px-4 py-3 text-sm text-white shadow-lg ${
						toast.level === "error"
							? "bg-red-600"
							: toast.level === "warning"
								? "bg-yellow-500"
								: toast.level === "success"
									? "bg-green-600"
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
