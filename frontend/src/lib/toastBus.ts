export type ToastLevel = "error" | "warning" | "success" | "info";

export interface ToastEvent {
	id: string;
	level: ToastLevel;
	message: string;
}

type Listener = (event: ToastEvent) => void;

const listeners: Listener[] = [];

export function subscribeToasts(listener: Listener): () => void {
	listeners.push(listener);
	return () => {
		const idx = listeners.indexOf(listener);
		if (idx !== -1) listeners.splice(idx, 1);
	};
}

export function dispatchToast(level: ToastLevel, message: string): void {
	const event: ToastEvent = { id: crypto.randomUUID(), level, message };
	listeners.forEach((l) => l(event));
}
