import { createEventBus } from "./createEventBus";

export type ToastLevel = "error" | "warning" | "success" | "info";

export interface ToastEvent {
	id: string;
	level: ToastLevel;
	message: string;
}

const bus = createEventBus<ToastEvent>();

export const subscribeToasts = bus.subscribe;

export function dispatchToast(level: ToastLevel, message: string): void {
	bus.publish({ id: crypto.randomUUID(), level, message });
}
