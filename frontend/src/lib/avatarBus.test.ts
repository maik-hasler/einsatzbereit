import { describe, it, expect, vi } from "vitest";
import { subscribeAvatarChanged, notifyAvatarChanged } from "./avatarBus";

describe("avatarBus", () => {
	it("does not throw when notifying with no listeners", () => {
		expect(() => notifyAvatarChanged()).not.toThrow();
	});

	it("calls a subscribed listener when notified", () => {
		const listener = vi.fn();
		const unsubscribe = subscribeAvatarChanged(listener);
		notifyAvatarChanged();
		expect(listener).toHaveBeenCalledTimes(1);
		unsubscribe();
	});

	it("stops calling a listener after it unsubscribes", () => {
		const listener = vi.fn();
		const unsubscribe = subscribeAvatarChanged(listener);
		unsubscribe();
		notifyAvatarChanged();
		expect(listener).not.toHaveBeenCalled();
	});
});
