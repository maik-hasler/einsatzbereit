import { createEventBus } from "./createEventBus";

const bus = createEventBus();

export const subscribeAvatarChanged = bus.subscribe;

export function notifyAvatarChanged(): void {
	bus.publish();
}
