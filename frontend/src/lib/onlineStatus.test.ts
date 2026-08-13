import { describe, it, expect, vi, afterEach } from "vitest";
import { getOnlineStatus, subscribeOnlineStatus } from "./onlineStatus";

function setNavigatorOnLine(value: boolean) {
	// navigator.onLine is a read-only getter in jsdom, so it has to be
	// redefined rather than assigned.
	Object.defineProperty(navigator, "onLine", {
		configurable: true,
		get: () => value,
	});
}

describe("getOnlineStatus", () => {
	afterEach(() => {
		setNavigatorOnLine(true);
	});

	it("reports what navigator.onLine says", () => {
		setNavigatorOnLine(false);
		expect(getOnlineStatus()).toBe(false);
		setNavigatorOnLine(true);
		expect(getOnlineStatus()).toBe(true);
	});
});

describe("subscribeOnlineStatus", () => {
	afterEach(() => {
		vi.restoreAllMocks();
	});

	it("notifies on both the online and the offline event", () => {
		const listener = vi.fn();
		const unsubscribe = subscribeOnlineStatus(listener);

		window.dispatchEvent(new Event("offline"));
		expect(listener).toHaveBeenCalledTimes(1);

		window.dispatchEvent(new Event("online"));
		expect(listener).toHaveBeenCalledTimes(2);

		unsubscribe();
	});

	it("stops notifying after the returned cleanup runs", () => {
		const listener = vi.fn();
		subscribeOnlineStatus(listener)();

		window.dispatchEvent(new Event("offline"));
		window.dispatchEvent(new Event("online"));

		expect(listener).not.toHaveBeenCalled();
	});

	it("removes exactly the listener it added, leaving other subscribers alone", () => {
		const kept = vi.fn();
		const dropped = vi.fn();
		const unsubscribeKept = subscribeOnlineStatus(kept);
		subscribeOnlineStatus(dropped)();

		window.dispatchEvent(new Event("offline"));

		expect(kept).toHaveBeenCalledTimes(1);
		expect(dropped).not.toHaveBeenCalled();

		unsubscribeKept();
	});
});
