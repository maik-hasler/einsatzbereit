import { createEventBus } from "./createEventBus";

const bus = createEventBus();

export const subscribeSessionExpired = bus.subscribe;

export function notifySessionExpired(): void {
	bus.publish();
}
