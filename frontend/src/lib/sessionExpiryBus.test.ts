import { describe, it, expect, vi } from "vitest";
import {
	subscribeSessionExpired,
	notifySessionExpired,
} from "./sessionExpiryBus";

describe("sessionExpiryBus", () => {
	it("does not throw when notifying with no listeners", () => {
		expect(() => notifySessionExpired()).not.toThrow();
	});

	it("calls a subscribed listener when notified", () => {
		const listener = vi.fn();
		const unsubscribe = subscribeSessionExpired(listener);
		notifySessionExpired();
		expect(listener).toHaveBeenCalledTimes(1);
		unsubscribe();
	});

	it("calls every subscribed listener", () => {
		const first = vi.fn();
		const second = vi.fn();
		const unsubscribeFirst = subscribeSessionExpired(first);
		const unsubscribeSecond = subscribeSessionExpired(second);
		notifySessionExpired();
		expect(first).toHaveBeenCalledTimes(1);
		expect(second).toHaveBeenCalledTimes(1);
		unsubscribeFirst();
		unsubscribeSecond();
	});

	it("stops calling a listener after it unsubscribes", () => {
		const listener = vi.fn();
		const unsubscribe = subscribeSessionExpired(listener);
		unsubscribe();
		notifySessionExpired();
		expect(listener).not.toHaveBeenCalled();
	});

	it("leaves other listeners intact when one unsubscribes", () => {
		const first = vi.fn();
		const second = vi.fn();
		const unsubscribeFirst = subscribeSessionExpired(first);
		const unsubscribeSecond = subscribeSessionExpired(second);
		unsubscribeFirst();
		notifySessionExpired();
		expect(first).not.toHaveBeenCalled();
		expect(second).toHaveBeenCalledTimes(1);
		unsubscribeSecond();
	});

	it("is safe to call unsubscribe more than once", () => {
		const listener = vi.fn();
		const unsubscribe = subscribeSessionExpired(listener);
		unsubscribe();
		expect(() => unsubscribe()).not.toThrow();
		notifySessionExpired();
		expect(listener).not.toHaveBeenCalled();
	});
});
