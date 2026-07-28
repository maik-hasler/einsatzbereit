import { describe, it, expect, vi } from "vitest";
import { subscribeToasts, dispatchToast, type ToastEvent } from "./toastBus";

describe("toastBus", () => {
	it("does not throw when dispatching with no listeners", () => {
		expect(() => dispatchToast("info", "hello")).not.toThrow();
	});

	it("delivers the level and message to a subscribed listener", () => {
		const listener = vi.fn();
		const unsubscribe = subscribeToasts(listener);
		dispatchToast("error", "Something failed");
		expect(listener).toHaveBeenCalledTimes(1);
		const event = listener.mock.calls[0][0] as ToastEvent;
		expect(event.level).toBe("error");
		expect(event.message).toBe("Something failed");
		expect(typeof event.id).toBe("string");
		expect(event.id.length).toBeGreaterThan(0);
		unsubscribe();
	});

	it("assigns a distinct id per dispatched toast", () => {
		const seen: string[] = [];
		const unsubscribe = subscribeToasts((event) => seen.push(event.id));
		dispatchToast("success", "first");
		dispatchToast("success", "second");
		expect(seen[0]).not.toBe(seen[1]);
		unsubscribe();
	});

	it("notifies every subscribed listener", () => {
		const first = vi.fn();
		const second = vi.fn();
		const unsubscribeFirst = subscribeToasts(first);
		const unsubscribeSecond = subscribeToasts(second);
		dispatchToast("warning", "careful");
		expect(first).toHaveBeenCalledTimes(1);
		expect(second).toHaveBeenCalledTimes(1);
		unsubscribeFirst();
		unsubscribeSecond();
	});

	it("stops notifying a listener after it unsubscribes", () => {
		const listener = vi.fn();
		const unsubscribe = subscribeToasts(listener);
		unsubscribe();
		dispatchToast("info", "ignored");
		expect(listener).not.toHaveBeenCalled();
	});
});
